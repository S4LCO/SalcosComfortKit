using System;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SalcosComfortKit.Client.SpecialSlots;

internal sealed class SpecialSlotLayoutModule : ClientModule
{
    private const int ColumnCount = 3;
    private static readonly FieldInfo SpecialSlotPanel = AccessTools.Field(
        typeof(SearchableSlotView),
        "_specSlotsPanel"
    );

    private readonly Harmony _harmony = new(ComfortKitPlugin.PluginGuid + ".specialslots");

    protected override string Name => "Extended Special Slots layout";

    protected override void Enable()
    {
        Patch(_harmony, typeof(CreateSlotsPatch));
    }

    [HarmonyPatch(typeof(SearchableSlotView), "CreateSlots", typeof(Item))]
    private static class CreateSlotsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(SearchableSlotView __instance, Item item)
        {
            if (!HasSixSpecialSlots(item))
            {
                return;
            }

            var panel = SpecialSlotPanel?.GetValue(__instance) as RectTransform;
            if (panel is null)
            {
                ComfortKitPlugin.Log.LogWarning("The Special Slots panel was not found.");
                return;
            }

            ArrangeAsGrid(panel);
        }
    }

    private static bool HasSixSpecialSlots(Item item)
    {
        return item is CompoundItem pockets
            && pockets.Slots.Count(slot =>
                slot?.Name?.StartsWith("SpecialSlot", StringComparison.OrdinalIgnoreCase) == true) >= 6;
    }

    private static void ArrangeAsGrid(RectTransform panel)
    {
        var grid = panel.GetComponent<GridLayoutGroup>();
        if (grid is null)
        {
            var oldLayout = panel.GetComponent<HorizontalLayoutGroup>();
            var oldPadding = oldLayout?.padding;
            var oldSpacing = oldLayout?.spacing ?? 0f;

            if (oldLayout is not null)
            {
                UnityEngine.Object.DestroyImmediate(oldLayout);
            }

            grid = panel.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = oldPadding is null
                ? new RectOffset()
                : new RectOffset(oldPadding.left, oldPadding.right, oldPadding.top, oldPadding.bottom);
            grid.spacing = new Vector2(oldSpacing, oldSpacing);
        }

        var cell = ItemViewFactory.GetCellPixelSize(new IntVec2(1, 1));
        grid.cellSize = new Vector2(cell.X, cell.Y);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = ColumnCount;

        var rowCount = Math.Max(1, (panel.childCount + ColumnCount - 1) / ColumnCount);
        var height = grid.padding.vertical
            + rowCount * grid.cellSize.y
            + Math.Max(0, rowCount - 1) * grid.spacing.y;

        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

        if (panel.parent is RectTransform parent)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        }
    }
}
