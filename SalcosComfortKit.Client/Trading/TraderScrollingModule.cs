using System;
using System.Reflection;
using EFT.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SalcosComfortKit.Client.Trading;

internal sealed class TraderScrollingModule : ClientModule
{
    private static readonly FieldInfo CardsContainerField = AccessTools.Field(
        typeof(TraderScreensGroup),
        "_traderCardsContainer"
    );

    private static readonly FieldInfo PlayerPanelField = AccessTools.Field(
        typeof(TraderScreensGroup),
        "_playerPanel"
    );

    private readonly Harmony _harmony = new(ComfortKitPlugin.PluginGuid + ".traderscrolling");

    protected override string Name => "Trader scrolling";

    protected override void Enable()
    {
        if (CardsContainerField is null)
        {
            throw new InvalidOperationException("The trader cards container was not found.");
        }

        Patch(_harmony, typeof(ShowTraderScreenPatch));
    }

    [HarmonyPatch(typeof(TraderScreensGroup), "Show")]
    private static class ShowTraderScreenPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TraderScreensGroup __instance)
        {
            var content = CardsContainerField.GetValue(__instance) as RectTransform;
            if (content is null)
            {
                ComfortKitPlugin.Log.LogWarning("Trader scrolling could not find the trader row.");
                return;
            }

            var scroller = __instance.GetComponent<TraderRowScroller>();
            if (scroller is null)
            {
                scroller = __instance.gameObject.AddComponent<TraderRowScroller>();
            }

            var playerPanel = PlayerPanelField?.GetValue(__instance) as Component;
            scroller.Bind(content, playerPanel?.transform as RectTransform);
        }
    }
}

internal sealed class TraderRowScroller : MonoBehaviour
{
    private const int DefaultVisibleCardCount = 9;
    private const float OverflowTolerance = 0.5f;

    private readonly Vector3[] _corners = new Vector3[4];

    private RectTransform _content;
    private RectTransform _viewport;
    private RectTransform _rightPanel;
    private Canvas _canvas;
    private Rect _visibleRect;
    private Bounds _cardBounds;
    private Vector2 _restingPosition;
    private float _minimumPosition;
    private float _maximumPosition;
    private int _refreshFrames;
    private int _cardCount;
    private bool _diagnosticPending;
    private bool _hasOverflow;
    private bool _wasEnabled;

    internal void Bind(RectTransform content, RectTransform rightPanel)
    {
        if (_content != content)
        {
            _content = content;
            _canvas = content.GetComponentInParent<Canvas>();
            _restingPosition = content.anchoredPosition;
            _wasEnabled = false;
        }

        _rightPanel = rightPanel;
        _refreshFrames = 4;
        _diagnosticPending = true;
        RefreshLayout();
    }

    private void Update()
    {
        if (_content == null)
        {
            return;
        }

        var enabled = ComfortKitPlugin.Settings.EnableTraderScrolling.Value;
        if (!enabled)
        {
            if (_wasEnabled)
            {
                _content.anchoredPosition = _restingPosition;
            }

            _wasEnabled = false;
            return;
        }

        _wasEnabled = true;

        var scroll = Input.mouseScrollDelta;
        if (Mathf.Approximately(scroll.x, 0f) && Mathf.Approximately(scroll.y, 0f))
        {
            return;
        }

        RefreshLayout();
        if (_viewport == null || !_hasOverflow)
        {
            return;
        }

        if (!IsPointerOverTraderRow())
        {
            return;
        }

        var axis = Mathf.Abs(scroll.x) > Mathf.Abs(scroll.y) ? scroll.x : scroll.y;
        MoveContent(axis * ComfortKitPlugin.Settings.TraderScrollSpeed.Value);
    }

    private void LateUpdate()
    {
        if (_content == null || _refreshFrames <= 0)
        {
            return;
        }

        RefreshLayout();
        _refreshFrames--;

        if (_refreshFrames == 0 && _diagnosticPending)
        {
            LogLayoutState();
        }
    }

    private void RefreshLayout()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);

        if (_viewport == null || _refreshFrames > 0)
        {
            _viewport = FindViewport();
        }

        if (_viewport == null
            || !TryGetCardBounds(_viewport, out var bounds, out _cardCount))
        {
            _hasOverflow = false;
            return;
        }

        var positionOffset = _content.anchoredPosition.x - _restingPosition.x;
        _visibleRect = FindVisibleRect(_viewport, positionOffset);
        _cardBounds = bounds;

        var restingMinimum = bounds.min.x - positionOffset;
        var restingMaximum = bounds.max.x - positionOffset;

        var overflowOnRight = Mathf.Max(0f, restingMaximum - _visibleRect.xMax);
        var overflowOnLeft = Mathf.Max(0f, _visibleRect.xMin - restingMinimum);

        _minimumPosition = _restingPosition.x - overflowOnRight;
        _maximumPosition = _restingPosition.x + overflowOnLeft;
        _hasOverflow = _maximumPosition - _minimumPosition > OverflowTolerance;

        if (_hasOverflow)
        {
            var position = _content.anchoredPosition;
            position.x = Mathf.Clamp(position.x, _minimumPosition, _maximumPosition);
            _content.anchoredPosition = position;
        }
    }

    private bool IsPointerOverTraderRow()
    {
        var camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _viewport,
                Input.mousePosition,
                camera,
                out var pointer))
        {
            return false;
        }

        return pointer.x >= _visibleRect.xMin
            && pointer.x <= _visibleRect.xMax
            && pointer.y >= _cardBounds.min.y
            && pointer.y <= _cardBounds.max.y;
    }

    private void MoveContent(float distance)
    {
        var position = _content.anchoredPosition;
        var nextPosition = Mathf.Clamp(
            position.x + distance,
            _minimumPosition,
            _maximumPosition
        );

        if (!Mathf.Approximately(position.x, nextPosition))
        {
            position.x = nextPosition;
            _content.anchoredPosition = position;
        }
    }

    private RectTransform FindViewport()
    {
        RectTransform firstParent = null;
        RectTransform smallestMaskedOverflow = null;
        RectTransform smallestOverflow = null;
        RectTransform smallestMasked = null;
        var maskedOverflowWidth = float.MaxValue;
        var overflowWidth = float.MaxValue;
        var maskedWidth = float.MaxValue;

        for (var current = _content.parent as RectTransform;
             current != null;
             current = current.parent as RectTransform)
        {
            if (current.rect.width <= 1f)
            {
                continue;
            }

            firstParent ??= current;

            var hasMask = current.GetComponent<RectMask2D>() != null
                || current.GetComponent<Mask>() != null;

            if (hasMask && current.rect.width < maskedWidth)
            {
                smallestMasked = current;
                maskedWidth = current.rect.width;
            }

            if (!TryGetCardBounds(current, out var bounds, out _))
            {
                continue;
            }

            var view = current.rect;
            var overflows = bounds.min.x < view.xMin - OverflowTolerance
                || bounds.max.x > view.xMax + OverflowTolerance;
            if (!overflows)
            {
                continue;
            }

            if (current.rect.width < overflowWidth)
            {
                smallestOverflow = current;
                overflowWidth = current.rect.width;
            }

            if (hasMask && current.rect.width < maskedOverflowWidth)
            {
                smallestMaskedOverflow = current;
                maskedOverflowWidth = current.rect.width;
            }
        }

        return smallestMaskedOverflow ?? smallestOverflow ?? smallestMasked ?? firstParent;
    }

    private Rect FindVisibleRect(RectTransform relativeTo, float positionOffset)
    {
        var visibleRect = relativeTo.rect;
        if (TryGetPanelBoundary(relativeTo, out var panelBoundary)
            && panelBoundary > visibleRect.xMin + 100f
            && panelBoundary < visibleRect.xMax - OverflowTolerance)
        {
            visibleRect.xMax = panelBoundary;
            return visibleRect;
        }

        if (_cardCount > DefaultVisibleCardCount
            && TryGetCardBounds(
                relativeTo,
                out var regularCards,
                out _,
                DefaultVisibleCardCount))
        {
            var regularRightEdge = regularCards.max.x - positionOffset;
            if (regularRightEdge > visibleRect.xMin + 100f)
            {
                visibleRect.xMax = Mathf.Min(visibleRect.xMax, regularRightEdge);
            }
        }

        return visibleRect;
    }

    private bool TryGetPanelBoundary(RectTransform relativeTo, out float boundary)
    {
        boundary = 0f;
        if (_rightPanel == null || !_rightPanel.gameObject.activeInHierarchy)
        {
            return false;
        }

        _rightPanel.GetWorldCorners(_corners);
        boundary = float.MaxValue;

        for (var index = 0; index < _corners.Length; index++)
        {
            var point = relativeTo.InverseTransformPoint(_corners[index]);
            boundary = Mathf.Min(boundary, point.x);
        }

        return boundary < float.MaxValue;
    }

    private bool TryGetCardBounds(
        RectTransform relativeTo,
        out Bounds bounds,
        out int cardCount,
        int maximumCards = int.MaxValue)
    {
        bounds = default;
        cardCount = 0;
        var hasPoint = false;

        for (var index = 0; index < _content.childCount; index++)
        {
            if (_content.GetChild(index) is not RectTransform card
                || !card.gameObject.activeInHierarchy
                || card.rect.width <= 1f)
            {
                continue;
            }

            card.GetWorldCorners(_corners);
            cardCount++;

            for (var cornerIndex = 0; cornerIndex < _corners.Length; cornerIndex++)
            {
                var point = relativeTo.InverseTransformPoint(_corners[cornerIndex]);
                if (!hasPoint)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasPoint = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }

            if (cardCount >= maximumCards)
            {
                break;
            }
        }

        return hasPoint;
    }

    private void LogLayoutState()
    {
        _diagnosticPending = false;

        if (_viewport == null || _cardCount == 0)
        {
            ComfortKitPlugin.Log.LogWarning(
                "Trader scrolling could not identify the visible trader row."
            );
            return;
        }

        var travel = Mathf.Max(0f, _maximumPosition - _minimumPosition);
        if (_hasOverflow)
        {
            ComfortKitPlugin.Log.LogInfo(
                $"Trader scrolling ready for {_cardCount} trader cards "
                + $"({travel:0} horizontal pixels available)."
            );
            return;
        }

        ComfortKitPlugin.Log.LogInfo(
            $"Trader scrolling found {_cardCount} trader cards; no horizontal overflow."
        );
    }
}
