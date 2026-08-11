using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace SalcosComfortKit.Client.SpecialSlots;

internal static class SpecialSlotFeatureSettings
{
    private const string ComfortKitSettingName = "enableExtendedSpecialSlots";
    private const string ArmorySettingName = "loadExtendedSpecialSlots";

    internal static bool Enabled
    {
        get
        {
            var gameRoot = BepInEx.Paths.GameRootPath;
            var path = Path.Combine(
                gameRoot,
                "SPT_Runtime",
                "user",
                "mods",
                "SalcosComfortKit",
                "config.json"
            );

            if (!File.Exists(path))
            {
                return true;
            }

            try
            {
                var document = JObject.Parse(File.ReadAllText(path));
                return document.Value<bool?>(ComfortKitSettingName) ?? true;
            }
            catch (Exception exception)
            {
                ComfortKitPlugin.Log.LogWarning(
                    $"Could not read the Special Slots setting; keeping it enabled: {exception.Message}"
                );
                return true;
            }
        }
    }

    internal static bool ArmoryProvidesExtendedSlots
    {
        get
        {
            var gameRoot = BepInEx.Paths.GameRootPath;
            var path = Path.Combine(
                gameRoot,
                "SPT_Runtime",
                "user",
                "mods",
                "SalcosArmory",
                "config",
                "settings.json"
            );

            if (!File.Exists(path))
            {
                return true;
            }

            try
            {
                var document = JObject.Parse(File.ReadAllText(path));
                return document.Value<bool?>(ArmorySettingName) ?? true;
            }
            catch (Exception exception)
            {
                ComfortKitPlugin.Log.LogWarning(
                    $"Could not read SALCO's ARMORY Special Slots setting; delegating to ARMORY: {exception.Message}"
                );
                return true;
            }
        }
    }
}
