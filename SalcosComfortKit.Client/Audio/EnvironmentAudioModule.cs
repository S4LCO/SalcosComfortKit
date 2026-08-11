using System.Reflection;
using Audio.AmbientSubsystem;
using Audio.Vehicles;
using EFT.Airdrop;
using HarmonyLib;
using UnityEngine;

namespace SalcosComfortKit.Client.Audio;

internal sealed class EnvironmentAudioModule : ClientModule
{
    private readonly Harmony _harmony = new Harmony(ComfortKitPlugin.PluginGuid + ".audio");

    protected override string Name => "Environment volume control";

    protected override void Enable()
    {
        Patch(_harmony, typeof(RainVolumePatch));
        Patch(_harmony, typeof(BtrVolumePatch));
        Patch(_harmony, typeof(AirdropVolumePatch));
        Patch(_harmony, typeof(AirplaneVolumePatch));
    }

    [HarmonyPatch(typeof(PrecipitationAmbientBlender), nameof(PrecipitationAmbientBlender.UpdateGlobalsVolumeMult))]
    private static class RainVolumePatch
    {
        private static readonly FieldInfo GlobalsVolumeField = AccessTools.Field(
            typeof(PrecipitationAmbientBlender),
            "_globalsVolumeMult"
        );

        [HarmonyPostfix]
        private static void Postfix(PrecipitationAmbientBlender __instance)
        {
            if (!ComfortKitPlugin.Settings.EnableEnvironmentVolume.Value || GlobalsVolumeField == null)
            {
                return;
            }

            var current = (float)GlobalsVolumeField.GetValue(__instance);
            GlobalsVolumeField.SetValue(
                __instance,
                Mathf.Clamp01(current * ComfortKitPlugin.Settings.RainVolume.Value)
            );
        }
    }

    [HarmonyPatch(typeof(VehicleMovementSoundContext), "get_MaxAllowedVolume")]
    private static class BtrVolumePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result)
        {
            if (ComfortKitPlugin.Settings.EnableEnvironmentVolume.Value)
            {
                __result *= ComfortKitPlugin.Settings.BtrVolume.Value;
            }
        }
    }

    [HarmonyPatch(typeof(ClientAirDrop), nameof(ClientAirDrop.PlaySound))]
    private static class AirdropVolumePatch
    {
        [HarmonyPrefix]
        private static void Prefix(TaggedClip clip, out float __state)
        {
            __state = clip?.Volume ?? 0f;
            if (
                clip != null
                && ComfortKitPlugin.Settings.EnableEnvironmentVolume.Value
            )
            {
                clip.Volume = __state * ComfortKitPlugin.Settings.AirdropVolume.Value;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(TaggedClip clip, float __state)
        {
            if (clip != null)
            {
                clip.Volume = __state;
            }
        }
    }

    [HarmonyPatch(typeof(ClientAirPlane), nameof(ClientAirPlane.PlaySound))]
    private static class AirplaneVolumePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ClientAirPlane __instance)
        {
            if (
                !ComfortKitPlugin.Settings.EnableEnvironmentVolume.Value
                || __instance?.SoundSource == null
            )
            {
                return;
            }

            __instance.SoundSource.SetBaseVolume(
                __instance.SoundSource.BaseVolume
                * ComfortKitPlugin.Settings.AirdropVolume.Value
            );
        }
    }
}

