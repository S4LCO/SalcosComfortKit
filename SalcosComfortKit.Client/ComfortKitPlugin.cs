using System;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using SalcosComfortKit.Client.Audio;
using SalcosComfortKit.Client.Doors;
using SalcosComfortKit.Client.Inventory;
using SalcosComfortKit.Client.Movement;
using SalcosComfortKit.Client.Pause;
using SalcosComfortKit.Client.Reload;
using SalcosComfortKit.Client.Skipper;
using SalcosComfortKit.Client.SpecialSlots;
using SalcosComfortKit.Client.TimeWeather;
using SalcosComfortKit.Client.Trading;
using SalcosComfortKit.Client.Tracking;

namespace SalcosComfortKit.Client;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("com.SPT.core", "4.1.0")]
[BepInDependency(ArmoryPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class ComfortKitPlugin : BaseUnityPlugin
{
    private const string ArmoryPluginGuid = "com.salco.salcosarmory";
    internal const string PluginGuid = "com.salco.comfortkit";
    internal const string PluginName = "Salco's Comfort Kit";
    internal const string PluginVersion = "0.3.0";

    internal static ManualLogSource Log { get; private set; }
    internal static ComfortKitConfig Settings { get; private set; }

    private TimeWeatherModule _timeWeather;
    private PauseModule _pause;
    private UseItemsAnywhereModule _itemsAnywhere;
    private KnockKnockModule _knockKnock;
    private KeepThatOnScreenModule _objectiveTracker;

    private void Awake()
    {
        Log = Logger;
        Settings = new ComfortKitConfig(Config);

        _timeWeather = new TimeWeatherModule();
        _pause = new PauseModule();
        _itemsAnywhere = new UseItemsAnywhereModule();
        _knockKnock = new KnockKnockModule();
        _objectiveTracker = new KeepThatOnScreenModule();

        _timeWeather.EnableSafely();
        new SkipperModule().EnableSafely();
        new OldMovementModule().EnableSafely();
        _pause.EnableSafely();
        new EnvironmentAudioModule().EnableSafely();
        _itemsAnywhere.EnableSafely();
        new TraderScrollingModule().EnableSafely();
        new QuickReloadModule().EnableSafely();
        _knockKnock.EnableSafely();
        _objectiveTracker.EnableSafely();
        var armoryInstalled = Chainloader.PluginInfos.ContainsKey(ArmoryPluginGuid);
        var armoryProvidesSlots = armoryInstalled
            && SpecialSlotFeatureSettings.ArmoryProvidesExtendedSlots;

        if (armoryProvidesSlots)
        {
            Log.LogInfo("Extended Special Slots layout delegated to SALCO's ARMORY.");
        }
        else if (!SpecialSlotFeatureSettings.Enabled)
        {
            Log.LogInfo("Extended Special Slots are disabled in the server configuration.");
        }
        else
        {
            if (armoryInstalled)
            {
                Log.LogInfo(
                    "SALCO's ARMORY Special Slots are disabled; SCK will provide the layout."
                );
            }

            new SpecialSlotLayoutModule().EnableSafely();
        }

        Log.LogInfo($"{PluginName} {PluginVersion} loaded for SPT 4.1.x.");
    }

    private void Update()
    {
        _timeWeather?.Update();
        _pause?.Update();
        _itemsAnywhere?.Update();
        _knockKnock?.Update();
    }

    private void OnGUI()
    {
        _timeWeather?.OnGui();
        _pause?.OnGui();
        _objectiveTracker?.OnGui();
    }

    private void LateUpdate()
    {
        _timeWeather?.LateUpdate();
    }

    private void OnDestroy()
    {
        try
        {
            _timeWeather?.Shutdown();
            _pause?.Shutdown();
            _itemsAnywhere?.Shutdown();
            _knockKnock?.Shutdown();
            _objectiveTracker?.Shutdown();
        }
        catch (Exception exception)
        {
            Log?.LogWarning($"Shutdown cleanup reported an error: {exception}");
        }
    }
}
