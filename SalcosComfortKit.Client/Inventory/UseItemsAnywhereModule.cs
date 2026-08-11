using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using EFT.InventoryLogic;
using HarmonyLib;

namespace SalcosComfortKit.Client.Inventory;

internal sealed class UseItemsAnywhereModule : ClientModule
{
    private static readonly FieldInfo FastAccessSlotsField = AccessTools.Field(
        typeof(EFT.InventoryLogic.Inventory),
        "FastAccessSlots"
    );

    private readonly Harmony _harmony = new(ComfortKitPlugin.PluginGuid + ".itemsanywhere");

    private EquipmentSlot[] _originalSlots;
    private EquipmentSlot[] _expandedSlots;
    private bool _applied;
    private bool _ready;

    protected override string Name => "Use Items Anywhere";

    protected override void Enable()
    {
        _originalSlots = FastAccessSlotsField?.GetValue(null) as EquipmentSlot[]
            ?? throw new InvalidOperationException("The fast-access equipment list was not found.");

        try
        {
            Patch(_harmony, typeof(NestedBindableItemPatch));
            Patch(_harmony, typeof(NestedConsumableReachabilityPatch));
        }
        catch
        {
            _harmony.UnpatchSelf();
            throw;
        }

        _expandedSlots = _originalSlots
            .Concat([EquipmentSlot.Backpack, EquipmentSlot.SecuredContainer])
            .Distinct()
            .ToArray();

        Refresh();
        _ready = true;
    }

    internal void Update()
    {
        if (_ready)
        {
            Refresh();
        }
    }

    internal void Shutdown()
    {
        if (!_ready)
        {
            return;
        }

        if (_applied && ReferenceEquals(FastAccessSlotsField.GetValue(null), _expandedSlots))
        {
            FastAccessSlotsField.SetValue(null, _originalSlots);
        }

        _harmony.UnpatchSelf();
        _applied = false;
        _ready = false;
    }

    private void Refresh()
    {
        var shouldBeEnabled = ComfortKitPlugin.Settings.EnableUseItemsAnywhere.Value;
        if (shouldBeEnabled == _applied)
        {
            return;
        }

        if (shouldBeEnabled)
        {
            FastAccessSlotsField.SetValue(null, _expandedSlots);
            _applied = true;
            return;
        }

        if (ReferenceEquals(FastAccessSlotsField.GetValue(null), _expandedSlots))
        {
            FastAccessSlotsField.SetValue(null, _originalSlots);
        }

        _applied = false;
    }

    private static bool IsFeatureEnabled()
    {
        return ComfortKitPlugin.Settings?.EnableUseItemsAnywhere?.Value == true;
    }

    private static ItemAddress FindEquipmentFacingAddress(Item parentItem)
    {
        if (!IsFeatureEnabled())
        {
            return parentItem?.CurrentAddress;
        }

        var current = parentItem;
        for (var depth = 0; current is not null && depth < 32; depth++)
        {
            var address = current.CurrentAddress;
            if (address?.Container is Slot)
            {
                return address;
            }

            var next = address?.Container?.ParentItem;
            if (next is null || ReferenceEquals(next, current))
            {
                break;
            }

            current = next;
        }

        return parentItem?.CurrentAddress;
    }

    private static bool IsNestedConsumableReachable(
        InventoryController controller,
        Item item)
    {
        if (!IsFeatureEnabled()
            || controller is null
            || item is not Meds && item is not FoodDrink
            || !controller.Examined(item))
        {
            return false;
        }

        var equipmentAddress = FindEquipmentFacingAddress(item);
        if (equipmentAddress?.Container is not Slot equipmentSlot)
        {
            return false;
        }

        var equipment = controller.Inventory?.Equipment;
        return equipment is not null
            && (ReferenceEquals(
                    equipmentSlot,
                    equipment.GetSlot(EquipmentSlot.Backpack))
                || ReferenceEquals(
                    equipmentSlot,
                    equipment.GetSlot(EquipmentSlot.SecuredContainer)));
    }

    [HarmonyPatch(typeof(InventoryController), nameof(InventoryController.IsAtBindablePlace))]
    private static class NestedBindableItemPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var parentItemGetter = AccessTools.PropertyGetter(
                typeof(IContainer),
                nameof(IContainer.ParentItem)
            );
            var currentAddressGetter = AccessTools.PropertyGetter(
                typeof(Item),
                nameof(Item.CurrentAddress)
            );
            var replacement = AccessTools.Method(
                typeof(UseItemsAnywhereModule),
                nameof(FindEquipmentFacingAddress)
            );

            for (var index = 0; index < codes.Count - 1; index++)
            {
                if (!codes[index].Calls(parentItemGetter)
                    || !codes[index + 1].Calls(currentAddressGetter))
                {
                    continue;
                }

                codes[index + 1].opcode = OpCodes.Call;
                codes[index + 1].operand = replacement;
                return codes;
            }

            throw new InvalidOperationException(
                "The bindable-item parent lookup could not be extended."
            );
        }
    }

    [HarmonyPatch(typeof(InventoryController), nameof(InventoryController.IsAtReachablePlace))]
    private static class NestedConsumableReachabilityPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(
            InventoryController __instance,
            Item item,
            ref bool __result)
        {
            if (!__result && IsNestedConsumableReachable(__instance, item))
            {
                __result = true;
            }
        }
    }
}
