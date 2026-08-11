using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.UI.BattleTimer;
using HarmonyLib;
using UnityEngine;

namespace SalcosComfortKit.Client.Pause;

internal sealed class PauseModule : ClientModule
{
    private readonly Harmony _harmony = new Harmony(ComfortKitPlugin.PluginGuid + ".pause");

    protected override string Name => "Raid pause";

    protected override void Enable()
    {
        Patch(_harmony, typeof(GameTimerStartPatch));
        Patch(_harmony, typeof(WorldTickPatch));
        Patch(_harmony, typeof(OtherWorldTickPatch));
        Patch(_harmony, typeof(EndByTimerPatch));
        Patch(_harmony, typeof(MainTimerPanelPatch));
    }

    internal void Update()
    {
        if (!ComfortKitPlugin.Settings.EnablePause.Value)
        {
            RaidPauseController.Resume();
            return;
        }

        if (RaidPauseController.IsPaused && !RaidPauseController.IsRaidActive())
        {
            RaidPauseController.Resume();
            return;
        }

        if (ComfortKitPlugin.Settings.PauseShortcut.Value.IsDown())
        {
            RaidPauseController.Toggle();
        }
    }

    internal void OnGui()
    {
        RaidPauseController.DrawOverlay();
    }

    internal void Shutdown()
    {
        RaidPauseController.Resume();
    }

    [HarmonyPatch(typeof(GameTimer), nameof(GameTimer.Start))]
    private static class GameTimerStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameTimer __instance)
        {
            RaidPauseController.RegisterTimer(__instance);
        }
    }

    [HarmonyPatch(typeof(GameWorld), nameof(GameWorld.DoWorldTick))]
    private static class WorldTickPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !RaidPauseController.IsPaused;
        }
    }

    [HarmonyPatch(typeof(GameWorld), nameof(GameWorld.DoOtherWorldTick))]
    private static class OtherWorldTickPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !RaidPauseController.IsPaused;
        }
    }

    [HarmonyPatch(typeof(EndByTimerScenario), nameof(EndByTimerScenario.Update))]
    private static class EndByTimerPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !RaidPauseController.IsPaused;
        }
    }

    [HarmonyPatch(typeof(MainTimerPanel), nameof(MainTimerPanel.UpdateTimer))]
    private static class MainTimerPanelPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MainTimerPanel __instance)
        {
            if (!RaidPauseController.IsPaused)
            {
                return true;
            }

            if (__instance.TimerText != null)
            {
                __instance.TimerText.text = "PAUSED";
            }

            return false;
        }
    }
}

internal static class RaidPauseController
{
    private static readonly HashSet<GameTimer> Timers = new HashSet<GameTimer>();
    private static DateTime _pausedAt;
    private static float _previousTimeScale = 1f;
    private static GameDateTime _lockedGameDateTime;
    private static GUIStyle _overlayStyle;

    internal static bool IsPaused { get; private set; }

    internal static void RegisterTimer(GameTimer timer)
    {
        if (timer != null)
        {
            Timers.Add(timer);
        }
    }

    internal static void Toggle()
    {
        if (IsPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    internal static bool IsRaidActive()
    {
        try
        {
            return Singleton<GameWorld>.Instantiated
                && Singleton<GameWorld>.Instance != null
                && Singleton<GameWorld>.Instance.MainPlayer != null;
        }
        catch
        {
            return false;
        }
    }

    internal static void Resume()
    {
        if (!IsPaused)
        {
            return;
        }

        var pausedDuration = DateTime.Now - _pausedAt;
        ShiftTimers(pausedDuration);

        try
        {
            _lockedGameDateTime?.Unlock();
        }
        catch (Exception exception)
        {
            ComfortKitPlugin.Log.LogWarning($"Raid clock unlock failed: {exception.Message}");
        }

        _lockedGameDateTime = null;
        Time.timeScale = _previousTimeScale <= 0f ? 1f : _previousTimeScale;
        IsPaused = false;
        ComfortKitPlugin.Log.LogInfo($"Raid resumed after {pausedDuration.TotalSeconds:0.0} seconds.");
    }

    internal static void DrawOverlay()
    {
        if (!IsPaused)
        {
            return;
        }

        if (_overlayStyle == null)
        {
            _overlayStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
                fontStyle = FontStyle.Bold
            };
            _overlayStyle.normal.textColor = Color.white;
        }

        var rect = new Rect(0f, 32f, Screen.width, 52f);
        GUI.Label(rect, "PAUSED", _overlayStyle);
    }

    private static void Pause()
    {
        if (!IsRaidActive())
        {
            ComfortKitPlugin.Log.LogDebug("Pause key ignored outside a solo raid.");
            return;
        }

        _pausedAt = DateTime.Now;
        _previousTimeScale = Time.timeScale;

        try
        {
            var world = Singleton<GameWorld>.Instance;
            _lockedGameDateTime = world.GameDateTime;
            _lockedGameDateTime?.Lock();
        }
        catch (Exception exception)
        {
            ComfortKitPlugin.Log.LogWarning($"Raid clock lock failed: {exception.Message}");
        }

        IsPaused = true;
        Time.timeScale = 0f;
        ComfortKitPlugin.Log.LogInfo("Raid paused.");
    }

    private static void ShiftTimers(TimeSpan duration)
    {
        Timers.RemoveWhere(timer => timer == null);

        foreach (var timer in Timers)
        {
            if (timer._startDateTime.HasValue)
            {
                timer._startDateTime = timer._startDateTime.Value.Add(duration);
            }
            if (timer._escapeDateTime.HasValue)
            {
                timer._escapeDateTime = timer._escapeDateTime.Value.Add(duration);
            }
            if (timer.nullable_2.HasValue)
            {
                timer.nullable_2 = timer.nullable_2.Value.Add(duration);
            }
        }
    }
}

