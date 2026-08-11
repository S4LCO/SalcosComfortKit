using System;
using BepInEx.Configuration;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace SalcosComfortKit.Client.Skipper;

internal sealed class SkipperModule : ClientModule
{
    private readonly Harmony _harmony = new Harmony(ComfortKitPlugin.PluginGuid + ".skipper");

    protected override string Name => "Quest skipper";

    protected override void Enable()
    {
        Patch(_harmony, typeof(QuestObjectiveShowPatch));
    }

    [HarmonyPatch(typeof(QuestObjectiveView), nameof(QuestObjectiveView.Show))]
    private static class QuestObjectiveShowPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            QuestObjectiveView __instance,
            Quest quest,
            Condition condition,
            QuestController questController
        )
        {
            if (!ComfortKitPlugin.Settings.EnableSkipper.Value || __instance == null || condition == null)
            {
                return;
            }

            try
            {
                var controller = __instance.GetComponent<SkipButtonController>();
                if (controller == null)
                {
                    controller = __instance.gameObject.AddComponent<SkipButtonController>();
                }

                controller.Bind(__instance, quest, condition, questController);
            }
            catch (Exception exception)
            {
                ComfortKitPlugin.Log.LogError($"Could not attach a quest Skip button: {exception}");
            }
        }
    }
}

internal sealed class SkipButtonController : MonoBehaviour
{
    private const float ShortcutReleaseGrace = 0.1f;

    private DefaultUIButton _button;
    private Quest _quest;
    private Condition _condition;
    private QuestController _questController;
    private bool _skipInProgress;
    private float _lastShortcutHeldAt = float.NegativeInfinity;

    internal void Bind(
        QuestObjectiveView view,
        Quest quest,
        Condition condition,
        QuestController questController
    )
    {
        _quest = quest;
        _condition = condition;
        _questController = questController;
        _skipInProgress = false;

        if (_button == null)
        {
            CreateButton(view);
        }

        UpdateVisibility();
    }

    private void CreateButton(QuestObjectiveView view)
    {
        var template = view._handoverButton;
        if (template == null)
        {
            throw new InvalidOperationException("Quest objective view has no button template.");
        }

        _button = Instantiate(template, template.transform.parent);
        _button.name = "SCK_SkipConditionButton";
        _button.OnClick.RemoveAllListeners();
        _button.OnClick.AddListener(SkipCurrentCondition);
        _button.SetRawText("Skip", 18);
        _button.SetTooltips(
            "Complete this quest condition locally.",
            "This quest condition cannot currently be skipped."
        );
        _button.Interactable = true;
        _button.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
        _button.gameObject.SetActive(false);
    }

    private void Update()
    {
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (_button == null)
        {
            return;
        }

        var shortcutHeld = IsShortcutHeld(ComfortKitPlugin.Settings.SkipperShortcut.Value);
        if (shortcutHeld)
        {
            _lastShortcutHeldAt = Time.unscaledTime;
        }

        var shortcutVisible = shortcutHeld
            || Time.unscaledTime - _lastShortcutHeldAt <= ShortcutReleaseGrace;

        var visible =
            ComfortKitPlugin.Settings.EnableSkipper.Value
            && !_skipInProgress
            && _quest != null
            && _condition != null
            && _questController != null
            && shortcutVisible;

        if (_button.gameObject.activeSelf != visible)
        {
            _button.gameObject.SetActive(visible);
        }
    }

    private static bool IsShortcutHeld(KeyboardShortcut shortcut)
    {
        if (shortcut.MainKey == KeyCode.None || !Input.GetKey(shortcut.MainKey))
        {
            return false;
        }

        foreach (var modifier in shortcut.Modifiers)
        {
            if (!Input.GetKey(modifier))
            {
                return false;
            }
        }

        return true;
    }

    private void SkipCurrentCondition()
    {
        if (_skipInProgress || _quest == null || _condition == null)
        {
            return;
        }

        _skipInProgress = true;
        UpdateVisibility();

        try
        {
            var clientController = _questController as QuestControllerClient;
            if (clientController == null || clientController.ConditionsConnectorsManagerClient == null)
            {
                throw new InvalidOperationException("The active quest controller is not a client quest controller.");
            }

            clientController.ConditionsConnectorsManagerClient.SetConditionCurrentValue(
                _quest,
                EQuestStatus.AvailableForFinish,
                _condition,
                _condition.value,
                true
            );

            ComfortKitPlugin.Log.LogInfo($"Skipped quest condition {_condition.id}.");
        }
        catch (Exception exception)
        {
            _skipInProgress = false;
            ComfortKitPlugin.Log.LogError($"Could not skip quest condition: {exception}");
        }
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
