using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace SalcosComfortKit.Client.Tracking;

internal sealed class KeepThatOnScreenModule : ClientModule
{
    private readonly Harmony _harmony = new(ComfortKitPlugin.PluginGuid + ".keepthatonscreen");
    private GUIStyle _panelStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _objectiveStyle;
    private float _styleScale = -1f;

    protected override string Name => "Keep that on screen";

    protected override void Enable()
    {
        Patch(_harmony, typeof(QuestObjectivePatch));
    }

    internal void OnGui()
    {
        if (!ComfortKitPlugin.Settings.EnableKeepThatOnScreen.Value
            || PinnedObjectives.Count == 0
            || !Singleton<GameWorld>.Instantiated
            || Singleton<GameWorld>.Instance?.MainPlayer == null
            || !Singleton<AbstractGame>.Instantiated
            || Singleton<AbstractGame>.Instance.GameType == EGameType.Hideout
            || Singleton<AbstractGame>.Instance.GameType == EGameType.Narrate)
        {
            return;
        }

        var scale = ComfortKitPlugin.Settings.ObjectiveTrackerScale.Value;
        EnsureStyles(scale);

        var width = 390f * scale;
        var padding = 12f * scale;
        var titleHeight = 28f * scale;
        var contentHeight = 0f;
        var lines = new List<string>(PinnedObjectives.Count);

        foreach (var objective in PinnedObjectives.Entries)
        {
            var completed = objective.IsCompleted;
            var marker = completed ? "✓" : "•";
            var questName = objective.Quest?.Template?.Name ?? "Task";
            var description = string.IsNullOrWhiteSpace(objective.Description)
                ? "Objective"
                : objective.Description;
            var text = $"{marker} <b>{questName}</b>\n{description}";
            lines.Add(text);
            contentHeight += _objectiveStyle.CalcHeight(new GUIContent(text), width - padding * 2f)
                + 8f * scale;
        }

        var height = padding * 2f + titleHeight + contentHeight;
        var panel = new Rect(Screen.width - width - 24f * scale, 82f * scale, width, height);
        GUI.Box(panel, GUIContent.none, _panelStyle);

        var x = panel.x + padding;
        var y = panel.y + padding;
        GUI.Label(new Rect(x, y, width - padding * 2f, titleHeight), "Pinned objectives", _titleStyle);
        y += titleHeight;

        foreach (var line in lines)
        {
            var lineHeight = _objectiveStyle.CalcHeight(
                new GUIContent(line),
                width - padding * 2f
            );
            GUI.Label(
                new Rect(x, y, width - padding * 2f, lineHeight),
                line,
                _objectiveStyle
            );
            y += lineHeight + 8f * scale;
        }
    }

    internal void Shutdown()
    {
        PinnedObjectives.Clear();
    }

    private void EnsureStyles(float scale)
    {
        if (_panelStyle != null && Mathf.Abs(_styleScale - scale) < 0.001f)
        {
            return;
        }

        _styleScale = scale;
        _panelStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(
                Mathf.RoundToInt(12f * scale),
                Mathf.RoundToInt(12f * scale),
                Mathf.RoundToInt(10f * scale),
                Mathf.RoundToInt(10f * scale)
            )
        };
        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(18f * scale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft
        };
        _objectiveStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(15f * scale),
            wordWrap = true,
            richText = true,
            alignment = TextAnchor.UpperLeft
        };
    }

    [HarmonyPatch(typeof(QuestObjectiveView), nameof(QuestObjectiveView.Show))]
    private static class QuestObjectivePatch
    {
        [HarmonyPostfix]
        private static void Postfix(QuestObjectiveView __instance, Quest quest, Condition condition)
        {
            if (__instance == null || quest == null || condition == null)
            {
                return;
            }

            try
            {
                var controller = __instance.GetComponent<ObjectivePinButton>();
                if (controller == null)
                {
                    controller = __instance.gameObject.AddComponent<ObjectivePinButton>();
                }

                controller.Bind(__instance, quest, condition);
            }
            catch (Exception exception)
            {
                ComfortKitPlugin.Log.LogError($"Could not attach an objective pin button: {exception}");
            }
        }
    }
}

internal sealed class ObjectivePinButton : MonoBehaviour
{
    private DefaultUIButton _button;
    private Quest _quest;
    private Condition _condition;
    private string _description;

    internal void Bind(QuestObjectiveView view, Quest quest, Condition condition)
    {
        _quest = quest;
        _condition = condition;
        _description = view._description != null
            ? view._description.text
            : condition.FormattedDescription;

        if (_button == null)
        {
            CreateButton(view);
        }

        if (PinnedObjectives.IsPinned(_quest, _condition))
        {
            PinnedObjectives.Refresh(_quest, _condition, _description);
        }

        UpdateButton();
    }

    private void CreateButton(QuestObjectiveView view)
    {
        var template = view._handoverButton;
        if (template == null)
        {
            throw new InvalidOperationException("Quest objective view has no button template.");
        }

        _button = Instantiate(template, template.transform.parent);
        _button.name = "SCK_PinObjectiveButton";
        _button.OnClick.RemoveAllListeners();
        _button.OnClick.AddListener(TogglePin);
        _button.SetTooltips(
            "Pin or unpin this objective for the in-raid display.",
            "You can pin up to three quest objectives."
        );
        _button.transform.SetSiblingIndex(template.transform.parent.childCount - 1);
    }

    private void Update()
    {
        UpdateButton();
    }

    private void UpdateButton()
    {
        if (_button == null)
        {
            return;
        }

        var enabled = ComfortKitPlugin.Settings.EnableKeepThatOnScreen.Value;
        var pinned = PinnedObjectives.IsPinned(_quest, _condition);
        var full = !pinned && PinnedObjectives.Count >= PinnedObjectives.Maximum;

        _button.gameObject.SetActive(enabled);
        _button.Interactable = enabled && !full;
        _button.SetRawText(pinned ? "Unpin" : full ? "Full" : "Pin", 18);
    }

    private void TogglePin()
    {
        if (_quest == null || _condition == null)
        {
            return;
        }

        PinnedObjectives.Toggle(_quest, _condition, _description);
        UpdateButton();
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            Destroy(_button.gameObject);
            _button = null;
        }
    }
}

internal static class PinnedObjectives
{
    internal const int Maximum = 3;

    internal sealed class Entry
    {
        internal Quest Quest;
        internal Condition Condition;
        internal string Description;

        internal bool IsCompleted => Quest?.CompletedConditions != null
            && Condition != null
            && Quest.CompletedConditions.Contains(Condition.id);
    }

    private static readonly List<Entry> Pinned = new();

    internal static IReadOnlyList<Entry> Entries => Pinned;
    internal static int Count => Pinned.Count;

    internal static bool IsPinned(Quest quest, Condition condition)
    {
        return FindIndex(quest, condition) >= 0;
    }

    internal static void Toggle(Quest quest, Condition condition, string description)
    {
        var index = FindIndex(quest, condition);
        if (index >= 0)
        {
            Pinned.RemoveAt(index);
            return;
        }

        if (Pinned.Count >= Maximum)
        {
            return;
        }

        Pinned.Add(
            new Entry
            {
                Quest = quest,
                Condition = condition,
                Description = description
            }
        );
    }

    internal static void Refresh(Quest quest, Condition condition, string description)
    {
        var index = FindIndex(quest, condition);
        if (index < 0)
        {
            return;
        }

        Pinned[index].Quest = quest;
        Pinned[index].Condition = condition;
        Pinned[index].Description = description;
    }

    internal static void Clear()
    {
        Pinned.Clear();
    }

    private static int FindIndex(Quest quest, Condition condition)
    {
        if (quest?.Template == null || condition == null)
        {
            return -1;
        }

        var questId = quest.Template.Id;
        var conditionId = condition.id;
        for (var index = 0; index < Pinned.Count; index++)
        {
            var entry = Pinned[index];
            if (entry.Quest?.Template?.Id == questId && entry.Condition?.id == conditionId)
            {
                return index;
            }
        }

        return -1;
    }
}
