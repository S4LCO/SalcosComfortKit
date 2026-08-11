using System;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.InputSystem;
using EFT.Weather;
using HarmonyLib;
using UnityEngine;

namespace SalcosComfortKit.Client.TimeWeather;

internal sealed class TimeWeatherModule : ClientModule
{
    private const int WindowId = 0x53434B01;
    private static readonly FieldInfo IgnoreInputField = AccessTools.Field(
        typeof(GamePlayerOwner),
        "bool_0"
    );

    private Rect _window = new Rect(40f, 80f, 370f, 410f);
    private bool _visible;
    private bool _cursorCaptured;
    private bool _inputCaptured;
    private bool _previousCursorVisible;
    private bool _previousIgnoreInput;
    private CursorLockMode _previousCursorLock;
    private WeatherController _customWeatherController;

    private float _hour = 12f;
    private float _cloudness;
    private float _rain;
    private float _fog;
    private float _wind;

    protected override string Name => "Time and weather changer";

    protected override void Enable()
    {
        // This module is driven by the plugin's Update and OnGUI callbacks.
    }

    internal void Update()
    {
        if (!ComfortKitPlugin.Settings.EnableTimeWeather.Value)
        {
            SetVisible(false);
            return;
        }

        if (ComfortKitPlugin.Settings.TimeWeatherShortcut.Value.IsDown())
        {
            if (!TryGetWorld(out _))
            {
                SetVisible(false);
                return;
            }

            SetVisible(!_visible);
            if (_visible)
            {
                ReadCurrentHour();
            }
        }

        if (_visible && !TryGetWorld(out _))
        {
            SetVisible(false);
        }
    }

    internal void OnGui()
    {
        if (!_visible || !ComfortKitPlugin.Settings.EnableTimeWeather.Value)
        {
            return;
        }

        _window = GUI.Window(WindowId, _window, DrawWindow, "Salco's Comfort Kit - Time & Weather");
    }

    internal void LateUpdate()
    {
        if (_visible)
        {
            GamePlayerOwner.SetIgnoreInput(true);
            ApplyCursorState(true, CursorLockMode.None);
        }
    }

    internal void Shutdown()
    {
        SetVisible(false);
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginVertical();

        GUILayout.Label($"Time: {FormatHour(_hour)}");
        _hour = GUILayout.HorizontalSlider(_hour, 0f, 23.9833f);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("- 1 hour"))
        {
            _hour = WrapHour(_hour - 1f);
        }
        if (GUILayout.Button("Use current"))
        {
            ReadCurrentHour();
        }
        if (GUILayout.Button("+ 1 hour"))
        {
            _hour = WrapHour(_hour + 1f);
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Apply time"))
        {
            ApplyTime();
        }

        GUILayout.Space(10f);
        GUILayout.Label($"Clouds: {_cloudness:0.00}");
        _cloudness = GUILayout.HorizontalSlider(_cloudness, -1f, 1f);
        GUILayout.Label($"Rain: {_rain:0.00}");
        _rain = GUILayout.HorizontalSlider(_rain, 0f, 1f);
        GUILayout.Label($"Fog: {_fog:0.000}");
        _fog = GUILayout.HorizontalSlider(_fog, 0f, 0.12f);
        GUILayout.Label($"Wind: {_wind:0.00}");
        _wind = GUILayout.HorizontalSlider(_wind, 0f, 1f);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear"))
        {
            SetPreset(-0.8f, 0f, 0.002f, 0.1f);
        }
        if (GUILayout.Button("Cloudy"))
        {
            SetPreset(0.55f, 0f, 0.015f, 0.35f);
        }
        if (GUILayout.Button("Rain"))
        {
            SetPreset(0.9f, 0.75f, 0.035f, 0.65f);
        }
        if (GUILayout.Button("Storm"))
        {
            SetPreset(1f, 1f, 0.065f, 1f);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply weather"))
        {
            ApplyWeather();
        }
        if (GUILayout.Button("Reset weather"))
        {
            ResetWeather();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        if (GUILayout.Button("Close"))
        {
            SetVisible(false);
        }

        GUILayout.EndVertical();
        GUI.DragWindow(new Rect(0f, 0f, _window.width, 28f));
    }

    private void ApplyTime()
    {
        if (!TryGetWorld(out var world) || world.GameDateTime == null)
        {
            ComfortKitPlugin.Log.LogWarning("Time change ignored because no raid clock is active.");
            return;
        }

        try
        {
            var current = world.GameDateTime.Calculate();
            var hours = Mathf.FloorToInt(_hour);
            var minutes = Mathf.Clamp(Mathf.RoundToInt((_hour - hours) * 60f), 0, 59);
            var target = new DateTime(
                current.Year,
                current.Month,
                current.Day,
                hours,
                minutes,
                0,
                current.Kind
            );

            world.GameDateTime.ResetForce(target);
            ComfortKitPlugin.Log.LogInfo($"Raid time changed to {target:HH:mm}.");
        }
        catch (Exception exception)
        {
            ComfortKitPlugin.Log.LogError($"Could not change raid time: {exception}");
        }
    }

    private void ApplyWeather()
    {
        var controller = WeatherController.Instance;
        if (controller == null)
        {
            ComfortKitPlugin.Log.LogWarning("Weather change ignored because no weather controller is active.");
            return;
        }

        try
        {
            var debugWeather = controller.WeatherDebug;
            if (debugWeather == null)
            {
                throw new InvalidOperationException("The active weather controller has no debug weather curve.");
            }

            if (!debugWeather.Enabled || _customWeatherController != controller)
            {
                debugWeather.CopyParams(controller.WeatherCurve);
            }

            debugWeather.CloudDensity = _cloudness;
            debugWeather.Rain = _rain;
            debugWeather.Fog = _fog;
            debugWeather.WindMagnitude = _wind;
            debugWeather.Enabled = true;
            _customWeatherController = controller;

            ComfortKitPlugin.Log.LogInfo("Custom raid weather applied.");
        }
        catch (Exception exception)
        {
            ComfortKitPlugin.Log.LogError($"Could not change raid weather: {exception}");
        }
    }

    private void ResetWeather()
    {
        var controller = WeatherController.Instance;
        if (controller?.WeatherDebug == null)
        {
            return;
        }

        controller.WeatherDebug.Enabled = false;
        _customWeatherController = null;
        ComfortKitPlugin.Log.LogInfo("Custom raid weather reset to the backend weather curve.");
    }

    private void ReadCurrentHour()
    {
        if (!TryGetWorld(out var world) || world.GameDateTime == null)
        {
            return;
        }

        var current = world.GameDateTime.Calculate();
        _hour = current.Hour + current.Minute / 60f;
    }

    private void SetPreset(float cloudness, float rain, float fog, float wind)
    {
        _cloudness = cloudness;
        _rain = rain;
        _fog = fog;
        _wind = wind;
    }

    private void SetVisible(bool visible)
    {
        if (_visible == visible)
        {
            return;
        }

        _visible = visible;
        if (visible)
        {
            _previousCursorVisible = Cursor.visible;
            _previousCursorLock = Cursor.lockState;
            _cursorCaptured = true;
            _previousIgnoreInput = IgnoreInputField != null
                && (bool)IgnoreInputField.GetValue(null);
            _inputCaptured = true;
            GamePlayerOwner.SetIgnoreInput(true);
            ApplyCursorState(true, CursorLockMode.None);
        }
        else
        {
            if (_inputCaptured)
            {
                GamePlayerOwner.SetIgnoreInput(_previousIgnoreInput);
                _inputCaptured = false;
            }

            if (_cursorCaptured)
            {
                ApplyCursorState(_previousCursorVisible, _previousCursorLock);
                _cursorCaptured = false;
            }
        }
    }

    private static void ApplyCursorState(bool visible, CursorLockMode lockMode)
    {
        Cursor.visible = visible;
        Cursor.lockState = lockMode;

        // EFT tracks its own effective lock state for remote mouse axes. Keeping
        // that state in sync prevents the raid input loop from recentering the
        // pointer while the IMGUI window is open.
        RemoteAxisUpdater.CursorLockModeChangedHandler();
    }

    private static bool TryGetWorld(out GameWorld world)
    {
        world = null;
        try
        {
            if (!Singleton<GameWorld>.Instantiated)
            {
                return false;
            }

            world = Singleton<GameWorld>.Instance;
            return world != null && world.MainPlayer != null;
        }
        catch
        {
            return false;
        }
    }

    private static float WrapHour(float hour)
    {
        while (hour < 0f)
        {
            hour += 24f;
        }
        while (hour >= 24f)
        {
            hour -= 24f;
        }
        return hour;
    }

    private static string FormatHour(float hour)
    {
        var normalized = WrapHour(hour);
        var h = Mathf.FloorToInt(normalized);
        var m = Mathf.Clamp(Mathf.RoundToInt((normalized - h) * 60f), 0, 59);
        return $"{h:00}:{m:00}";
    }
}
