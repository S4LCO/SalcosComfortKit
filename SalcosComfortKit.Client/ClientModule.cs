using System;
using HarmonyLib;

namespace SalcosComfortKit.Client;

internal abstract class ClientModule
{
    protected abstract string Name { get; }

    internal void EnableSafely()
    {
        try
        {
            Enable();
            ComfortKitPlugin.Log.LogInfo($"{Name}: enabled.");
        }
        catch (Exception exception)
        {
            ComfortKitPlugin.Log.LogError($"{Name}: disabled after initialization failed: {exception}");
        }
    }

    protected abstract void Enable();

    protected static void Patch(Harmony harmony, Type patchType)
    {
        harmony.CreateClassProcessor(patchType).Patch();
    }
}

