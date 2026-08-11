using System;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;

namespace SalcosComfortKit.Client.Reload;

internal sealed class QuickReloadModule : ClientModule
{
    private readonly Harmony _harmony = new(ComfortKitPlugin.PluginGuid + ".quickreload");

    protected override string Name => "Quick reload magazine retention";

    protected override void Enable()
    {
        Patch(_harmony, typeof(RetainMagazinePatch));
    }

    [HarmonyPatch]
    private static class RetainMagazinePatch
    {
        private static bool _warningLogged;

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(Player.FirearmController.ReloadExternalMagResult),
                nameof(Player.FirearmController.ReloadExternalMagResult.Run)
            );
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        private static void Prefix(
            ItemController itemController,
            Weapon weapon,
            bool quickReload,
            ref ItemAddress vestTargetAddress)
        {
            if (!ComfortKitPlugin.Settings.KeepQuickReloadMagazines.Value
                || !quickReload
                || vestTargetAddress is not null
                || itemController is not InventoryController inventoryController
                || weapon is null)
            {
                return;
            }

            var oldMagazine = weapon.GetCurrentMagazine();
            if (oldMagazine is null)
            {
                return;
            }

            try
            {
                var equipment = inventoryController.Inventory?.Equipment;
                if (equipment is null)
                {
                    return;
                }

                var grids = InventoryEquipmentExtension.GetPrioritizedGridsForUnloadedObject(
                    equipment,
                    false
                );
                vestTargetAddress = GridExtensions.FindLocationForItem(grids, oldMagazine);
            }
            catch (Exception exception)
            {
                if (_warningLogged)
                {
                    return;
                }

                _warningLogged = true;
                ComfortKitPlugin.Log.LogWarning(
                    $"Quick reload could not reserve an inventory slot: {exception.Message}"
                );
            }
        }
    }
}
