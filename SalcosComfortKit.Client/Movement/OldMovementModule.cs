using System;
using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using UnityEngine;

namespace SalcosComfortKit.Client.Movement;

internal sealed class OldMovementModule : ClientModule
{
    private readonly Harmony _harmony = new Harmony(ComfortKitPlugin.PluginGuid + ".movement");

    protected override string Name => "Old Tarkov movement";

    protected override void Enable()
    {
        Patch(_harmony, typeof(WalkInertiaPatch));
        Patch(_harmony, typeof(DirectionInertiaCoefficientPatch));
        Patch(_harmony, typeof(DirectionChangePatch));
        Patch(_harmony, typeof(DirectionSmoothingPatch));
        Patch(_harmony, typeof(QuickTiltPatch));
        Patch(_harmony, typeof(BushRestrictionPatch));
        Patch(_harmony, typeof(AimingSlowdownPatch));
        Patch(_harmony, typeof(RotationJitterPatch));
    }

    internal static bool AppliesTo(MovementContext context)
    {
        if (
            context == null
            || !ComfortKitPlugin.Settings.EnableOldMovement.Value
        )
        {
            return false;
        }

        return ComfortKitPlugin.Settings.BotsUseOldMovement.Value || !context.IsAI;
    }

    [HarmonyPatch]
    private static class WalkInertiaPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.WalkInertia));
        }

        [HarmonyPostfix]
        private static void Postfix(MovementContext __instance, ref float __result)
        {
            if (AppliesTo(__instance) && ComfortKitPlugin.Settings.NoInertia.Value)
            {
                __result = ComfortKitPlugin.Settings.MovementResponseTime.Value;
            }
        }
    }

    [HarmonyPatch]
    private static class DirectionInertiaCoefficientPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.PropertyGetter(
                typeof(MovementContext),
                nameof(MovementContext.MoveSideInertia)
            );
            yield return AccessTools.PropertyGetter(
                typeof(MovementContext),
                nameof(MovementContext.MoveDiagonalInertia)
            );
        }

        [HarmonyPostfix]
        private static void Postfix(MovementContext __instance, ref float __result)
        {
            if (AppliesTo(__instance) && ComfortKitPlugin.Settings.NoInertia.Value)
            {
                __result = 1f;
            }
        }
    }

    [HarmonyPatch(typeof(MovePlayerState), nameof(MovePlayerState.UpdateMovementDirection))]
    private static class DirectionChangePatch
    {
        [HarmonyPrefix]
        private static void Prefix(MovePlayerState __instance)
        {
            if (
                __instance == null
                || !AppliesTo(__instance.MovementContext)
                || !ComfortKitPlugin.Settings.NoInertia.Value
            )
            {
                return;
            }

            // Direction reversals have a separate stop timer in the current
            // movement state. Aligning the previous direction with the fresh
            // input removes that pause without changing sprint or lean data.
            var direction = __instance.Direction;
            var previousDirection = __instance.prevDirection;
            if (
                previousDirection.sqrMagnitude > 0.0001f
                && direction.sqrMagnitude > 0.0001f
                && Vector2.Dot(previousDirection.normalized, direction.normalized) < 0f
            )
            {
                __instance.TransitionCoef = Mathf.Max(__instance.TransitionCoef, 1f);
            }

            __instance.moveTime = 0f;
            __instance.nextDirection = direction;
            __instance.prevDirection = direction;
            __instance.smoothMovementDirectionTime = 0f;
        }
    }

    [HarmonyPatch(typeof(MovePlayerState), nameof(MovePlayerState.ProcessDirection))]
    private static class DirectionSmoothingPatch
    {
        [HarmonyPrefix]
        private static void Prefix(MovePlayerState __instance, ref float smoothTime)
        {
            if (
                __instance == null
                || !AppliesTo(__instance.MovementContext)
                || !ComfortKitPlugin.Settings.NoInertia.Value
            )
            {
                return;
            }

            // A short fixed response retains the direct pre-inertia feel while
            // avoiding an instantaneous, unnatural direction flip.
            smoothTime = ComfortKitPlugin.Settings.MovementResponseTime.Value;
            __instance.smoothMovementDirectionTime = 0f;
        }
    }

    [HarmonyPatch(typeof(MovementContext), nameof(MovementContext.InertiaSmoothTilt))]
    private static class QuickTiltPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MovementContext __instance, float smoothDiff, float deltaTime)
        {
            if (!AppliesTo(__instance) || !ComfortKitPlugin.Settings.QuickTilting.Value)
            {
                return true;
            }

            // EFT retains its pre-inertia leaning path as LegacySmoothTilt.
            // Selecting it here keeps Q/E functional and avoids duplicating
            // the game's animation and mounted-weapon handling.
            __instance.LegacySmoothTilt(smoothDiff, deltaTime);
            return false;
        }
    }

    [HarmonyPatch(typeof(MovementContext), nameof(MovementContext.OnEnterObstacle))]
    private static class BushRestrictionPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MovementContext __instance, ObstacleCollider obstacle)
        {
            if (
                !AppliesTo(__instance)
                || ComfortKitPlugin.Settings.DoBushesSlowYouDown.Value
                || obstacle == null
            )
            {
                return true;
            }

            return !obstacle.HasSwampSpeedLimit;
        }
    }

    [HarmonyPatch(typeof(MovementContext), nameof(MovementContext.SetAimingSlowdown))]
    private static class AimingSlowdownPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MovementContext __instance)
        {
            return !AppliesTo(__instance) || ComfortKitPlugin.Settings.DoesAimingSlowYouDown.Value;
        }
    }

    [HarmonyPatch(typeof(MovementContext), nameof(MovementContext.PlayerAnimatorSetDeltaRotation))]
    private static class RotationJitterPatch
    {
        [HarmonyPrefix]
        private static void Prefix(MovementContext __instance, ref float movementContextPitch)
        {
            if (
                AppliesTo(__instance)
                && ComfortKitPlugin.Settings.RemoveJitteryRotation.Value
                && Mathf.Abs(movementContextPitch) < 0.015f
            )
            {
                movementContextPitch = 0f;
            }
        }
    }
}
