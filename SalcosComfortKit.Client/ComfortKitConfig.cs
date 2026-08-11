using BepInEx.Configuration;
using UnityEngine;

namespace SalcosComfortKit.Client;

internal sealed class ComfortKitConfig
{
    internal ConfigEntry<bool> EnableTimeWeather { get; }
    internal ConfigEntry<bool> EnableSkipper { get; }
    internal ConfigEntry<bool> EnableOldMovement { get; }
    internal ConfigEntry<bool> EnablePause { get; }
    internal ConfigEntry<bool> EnableEnvironmentVolume { get; }
    internal ConfigEntry<bool> EnableUseItemsAnywhere { get; }
    internal ConfigEntry<bool> EnableTraderScrolling { get; }
    internal ConfigEntry<bool> KeepQuickReloadMagazines { get; }
    internal ConfigEntry<bool> EnableKnockKnock { get; }
    internal ConfigEntry<bool> EnableKeepThatOnScreen { get; }

    internal ConfigEntry<KeyboardShortcut> TimeWeatherShortcut { get; }
    internal ConfigEntry<KeyboardShortcut> SkipperShortcut { get; }
    internal ConfigEntry<KeyboardShortcut> PauseShortcut { get; }

    internal ConfigEntry<bool> NoInertia { get; }
    internal ConfigEntry<bool> QuickTilting { get; }
    internal ConfigEntry<bool> DoBushesSlowYouDown { get; }
    internal ConfigEntry<bool> BotsUseOldMovement { get; }
    internal ConfigEntry<bool> DoesAimingSlowYouDown { get; }
    internal ConfigEntry<bool> RemoveJitteryRotation { get; }
    internal ConfigEntry<float> MovementResponseTime { get; }

    internal ConfigEntry<float> RainVolume { get; }
    internal ConfigEntry<float> BtrVolume { get; }
    internal ConfigEntry<float> AirdropVolume { get; }
    internal ConfigEntry<float> TraderScrollSpeed { get; }
    internal ConfigEntry<float> KnockKnockMaxDistance { get; }
    internal ConfigEntry<float> KnockKnockLockRadius { get; }
    internal ConfigEntry<float> ObjectiveTrackerScale { get; }

    internal ComfortKitConfig(ConfigFile config)
    {
        EnableTimeWeather = config.Bind(
            "01 - Modules",
            "Enable time and weather changer",
            true,
            "Enables the in-raid time and weather control window."
        );
        EnableSkipper = config.Bind(
            "01 - Modules",
            "Enable quest skipper",
            true,
            "Shows a Skip button beside quest conditions while the skipper key is held."
        );
        EnableOldMovement = config.Bind(
            "01 - Modules",
            "Enable old Tarkov movement",
            true,
            "Enables the inertia-free movement module. A game restart is recommended after changing this setting."
        );
        EnablePause = config.Bind(
            "01 - Modules",
            "Enable raid pause",
            true,
            "Enables true solo-raid pausing."
        );
        EnableEnvironmentVolume = config.Bind(
            "01 - Modules",
            "Enable environment volume control",
            true,
            "Enables rain, BTR and airdrop volume multipliers."
        );
        EnableUseItemsAnywhere = config.Bind(
            "01 - Modules",
            "Enable use items anywhere",
            true,
            "Allows usable and bindable items in your backpack or secure container to remain within quick reach."
        );
        EnableTraderScrolling = config.Bind(
            "01 - Modules",
            "Enable trader scrolling",
            true,
            "Lets the mouse wheel move sideways through the trader row."
        );
        EnableKnockKnock = config.Bind(
            "01 - Modules",
            "Enable Knock Knock",
            true,
            "Allows the local player to shoot open suitable locked doors with a shotgun."
        );
        EnableKeepThatOnScreen = config.Bind(
            "01 - Modules",
            "Enable Keep that on screen",
            true,
            "Lets you pin up to three quest objectives and keep them visible during a raid."
        );
        TimeWeatherShortcut = config.Bind(
            "02 - Controls",
            "Time and weather window",
            new KeyboardShortcut(KeyCode.UpArrow),
            "Opens or closes the time and weather window while in a raid."
        );
        SkipperShortcut = config.Bind(
            "02 - Controls",
            "Show quest Skip buttons",
            new KeyboardShortcut(KeyCode.LeftControl),
            "Hold this key to reveal Skip buttons on quest objectives."
        );
        PauseShortcut = config.Bind(
            "02 - Controls",
            "Pause raid",
            new KeyboardShortcut(KeyCode.DownArrow),
            "Pauses or resumes a solo raid."
        );

        NoInertia = config.Bind(
            "03 - Old Tarkov Movement",
            "No inertia",
            true,
            "Removes acceleration, braking and directional inertia from movement."
        );
        QuickTilting = config.Bind(
            "03 - Old Tarkov Movement",
            "Quick tilting",
            true,
            "Uses the direct pre-inertia leaning response."
        );
        DoBushesSlowYouDown = config.Bind(
            "03 - Old Tarkov Movement",
            "Bushes slow movement",
            false,
            "When disabled, swamp-type bush colliders do not apply a speed restriction."
        );
        BotsUseOldMovement = config.Bind(
            "03 - Old Tarkov Movement",
            "Bots use old movement",
            true,
            "Applies the same inertia behavior to AI movement contexts."
        );
        DoesAimingSlowYouDown = config.Bind(
            "03 - Old Tarkov Movement",
            "Aiming slows movement",
            true,
            "Keeps the normal aiming movement penalty."
        );
        RemoveJitteryRotation = config.Bind(
            "03 - Old Tarkov Movement",
            "Reduce jittery rotation",
            false,
            "Smooths tiny animator rotation changes. This option is experimental."
        );
        MovementResponseTime = config.Bind(
            "03 - Old Tarkov Movement",
            "Movement response time",
            0.08f,
            new ConfigDescription(
                "Legacy-style movement transition time in seconds. Lower values feel sharper; higher values feel softer.",
                new AcceptableValueRange<float>(0.03f, 0.18f)
            )
        );

        var volumeRange = new AcceptableValueRange<float>(0f, 1f);
        RainVolume = config.Bind(
            "04 - Environment Volume",
            "Rain volume",
            0.65f,
            new ConfigDescription("Rain volume multiplier.", volumeRange)
        );
        BtrVolume = config.Bind(
            "04 - Environment Volume",
            "BTR volume",
            0.65f,
            new ConfigDescription("BTR volume multiplier.", volumeRange)
        );
        AirdropVolume = config.Bind(
            "04 - Environment Volume",
            "Airdrop volume",
            0.65f,
            new ConfigDescription("Airdrop crate and airplane volume multiplier.", volumeRange)
        );

        TraderScrollSpeed = config.Bind(
            "05 - Trader Scrolling",
            "Scroll speed",
            140f,
            new ConfigDescription(
                "Distance moved for each mouse-wheel step over the trader row.",
                new AcceptableValueRange<float>(40f, 320f)
            )
        );

        KeepQuickReloadMagazines = config.Bind(
            "06 - Quick Reload",
            "Keep magazines during quick reload",
            true,
            "Returns the old magazine to a free inventory grid during a fast reload. If no space is available, it still drops to the ground."
        );

        KnockKnockMaxDistance = config.Bind(
            "07 - Knock Knock",
            "Maximum distance",
            5f,
            new ConfigDescription(
                "Maximum distance in metres between you and the point where the shotgun hits the door.",
                new AcceptableValueRange<float>(1f, 15f)
            )
        );
        KnockKnockLockRadius = config.Bind(
            "07 - Knock Knock",
            "Lock hit radius",
            0.75f,
            new ConfigDescription(
                "How close the shotgun impact must be to the door handle or lock. Doors without a usable handle accept any direct hit.",
                new AcceptableValueRange<float>(0.2f, 1.5f)
            )
        );

        ObjectiveTrackerScale = config.Bind(
            "08 - Keep that on screen",
            "Display scale",
            1f,
            new ConfigDescription(
                "Size of the pinned quest objective display.",
                new AcceptableValueRange<float>(0.75f, 1.5f)
            )
        );

    }
}
