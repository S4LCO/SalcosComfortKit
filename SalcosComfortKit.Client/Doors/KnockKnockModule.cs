using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace SalcosComfortKit.Client.Doors;

internal sealed class KnockKnockModule : ClientModule
{
    private readonly Harmony _harmony = new(ComfortKitPlugin.PluginGuid + ".shotgundoors");
    private static readonly Dictionary<int, Door> DoorsByCollider = new();
    private static readonly Dictionary<int, float> RecentBreaches = new();

    private int _indexedWorldId;
    private float _nextIndexAttempt;
    private bool _indexReady;

    protected override string Name => "Knock Knock";

    protected override void Enable()
    {
        Patch(_harmony, typeof(ShotImpactPatch));
    }

    internal void Update()
    {
        if (!Singleton<GameWorld>.Instantiated || Singleton<GameWorld>.Instance == null)
        {
            ResetDoorIndex();
            return;
        }

        var worldId = Singleton<GameWorld>.Instance.GetInstanceID();
        if (worldId != _indexedWorldId)
        {
            DoorsByCollider.Clear();
            RecentBreaches.Clear();
            _indexedWorldId = worldId;
            _indexReady = false;
            _nextIndexAttempt = Time.realtimeSinceStartup + 3f;
            return;
        }

        if (_indexReady || Time.realtimeSinceStartup < _nextIndexAttempt)
        {
            return;
        }

        _indexReady = BuildDoorIndex();
        if (!_indexReady)
        {
            _nextIndexAttempt = Time.realtimeSinceStartup + 2f;
        }
    }

    internal void Shutdown()
    {
        ResetDoorIndex();
    }

    private void ResetDoorIndex()
    {
        if (_indexedWorldId == 0 && DoorsByCollider.Count == 0 && RecentBreaches.Count == 0)
        {
            return;
        }

        _indexedWorldId = 0;
        _nextIndexAttempt = 0f;
        _indexReady = false;
        DoorsByCollider.Clear();
        RecentBreaches.Clear();
    }

    private static bool BuildDoorIndex()
    {
        DoorsByCollider.Clear();
        var doors = UnityEngine.Object.FindObjectsOfType<Door>();
        foreach (var door in doors)
        {
            if (!IsSupportedDoorType(door))
            {
                continue;
            }

            foreach (var collider in door.GetComponentsInChildren<Collider>(true))
            {
                AddDoorCollider(collider, door);
            }

            if (door.CollisionColliders == null)
            {
                continue;
            }

            foreach (var collider in door.CollisionColliders)
            {
                AddDoorCollider(collider, door);
            }
        }

        return doors.Length > 0;
    }

    private static void AddDoorCollider(Collider collider, Door door)
    {
        if (collider != null)
        {
            DoorsByCollider[collider.GetInstanceID()] = door;
        }
    }

    private static bool IsSupportedDoorType(Door door)
    {
        return door != null
            && door is not KeycardDoor
            && door is not SlidingDoor
            && door is not DoorSwitch;
    }

    [HarmonyPatch(typeof(ClientGameWorld), nameof(ClientGameWorld.ShotDelegate))]
    private static class ShotImpactPatch
    {
        private static bool _warningLogged;

        [HarmonyPostfix]
        private static void Postfix(Shot shotResult)
        {
            if (!ComfortKitPlugin.Settings.EnableKnockKnock.Value)
            {
                return;
            }

            try
            {
                TryBreach(shotResult);
            }
            catch (Exception exception)
            {
                if (_warningLogged)
                {
                    return;
                }

                _warningLogged = true;
                ComfortKitPlugin.Log.LogWarning(
                    $"Knock Knock ignored an unexpected hit: {exception.Message}"
                );
            }
        }

        private static void TryBreach(Shot shot)
        {
            if (shot is null)
            {
                return;
            }

            var shooter = shot.Player?.iPlayer;
            if (shooter is null || !shooter.IsYourPlayer)
            {
                return;
            }

            if (shot.Weapon is not Weapon weapon
                || !string.Equals(weapon.WeapClass, "shotgun", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var hitPoint = shot.HitPoint;
            var door = FindDoor(shot.HitCollider);
            if (!IsSuitableDoor(door))
            {
                return;
            }

            var maxDistance = ComfortKitPlugin.Settings.KnockKnockMaxDistance.Value;
            if ((shooter.Position - hitPoint).sqrMagnitude > maxDistance * maxDistance)
            {
                return;
            }

            if (door.LockHandle != null)
            {
                var lockRadius = ComfortKitPlugin.Settings.KnockKnockLockRadius.Value;
                if ((door.LockHandle.transform.position - hitPoint).sqrMagnitude
                    > lockRadius * lockRadius)
                {
                    return;
                }
            }

            var doorId = door.GetInstanceID();
            var now = Time.realtimeSinceStartup;
            if (RecentBreaches.TryGetValue(doorId, out var previousHit) && now - previousHit < 2f)
            {
                return;
            }

            RecentBreaches[doorId] = now;
            door.KickOpen(shooter.Position, true);
        }

        private static Door FindDoor(Collider hitCollider)
        {
            if (hitCollider == null)
            {
                return null;
            }

            var directMatch = hitCollider.GetComponentInParent<Door>();
            if (IsSupportedDoorType(directMatch))
            {
                return directMatch;
            }

            var rigidbodyMatch = hitCollider.attachedRigidbody != null
                ? hitCollider.attachedRigidbody.GetComponentInParent<Door>()
                : null;
            if (IsSupportedDoorType(rigidbodyMatch))
            {
                return rigidbodyMatch;
            }

            if (DoorsByCollider.TryGetValue(hitCollider.GetInstanceID(), out var indexedDoor)
                && IsSupportedDoorType(indexedDoor))
            {
                return indexedDoor;
            }

            return null;
        }

        private static bool IsSuitableDoor(Door door)
        {
            return IsSupportedDoorType(door)
                && door.DoorState == EDoorState.Locked
                && door.Operatable
                && !door.NoInteractionsAllowed
                && !door.IsBroken;
        }
    }
}
