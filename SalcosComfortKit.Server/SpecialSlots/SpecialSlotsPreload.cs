using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;
using SalcosComfortKit.Server.Configuration;
using IOPath = System.IO.Path;

namespace SalcosComfortKit.Server.SpecialSlots;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Preload)]
public sealed class SpecialSlotsPreload(
    TemplateTable templates,
    ICloner cloner,
    ISptLogger<SpecialSlotsPreload> logger) : IOnLoad
{
    private const string ArmoryAssembly = "SalcosArmory";
    private const string SvmAssembly = "ServerValueModifier";

    private static readonly MongoId DefaultPmcPockets = new("627a4e6b255f7527fb05a0f6");
    private static readonly MongoId DefaultScavPockets = new("557ffd194bdc2d28148b457f");
    private static readonly MongoId SvmPmcPockets = new("a8edfb0bce53d103d3f62b9b");
    private static readonly MongoId SvmScavPockets = new("a8edfb0bce53d103d3f6219b");

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var settings = ComfortKitServerConfig.Load(logger);
        if (!settings.EnableExtendedSpecialSlots)
        {
            logger.Warning(
                $"{ComfortKitInfo.LogPrefix} SCK's Extended Special Slots are disabled. Empty slots 4-6 before turning this setting off to avoid orphaned items. Other installed mods may still provide their own slots."
            );
            return Task.CompletedTask;
        }

        var armory = FindLoadedAssembly(ArmoryAssembly);
        if (armory is not null && ArmoryProvidesExtendedSlots(armory))
        {
            logger.Info(
                $"{ComfortKitInfo.LogPrefix} Extended Special Slots delegated to SALCO's ARMORY."
            );
            return Task.CompletedTask;
        }

        if (armory is not null)
        {
            logger.Info(
                $"{ComfortKitInfo.LogPrefix} SALCO's ARMORY Special Slots are disabled; SCK will provide them."
            );
        }

        PrepareSvmPockets();

        var pocketCount = 0;
        var newSlotCount = 0;

        foreach (var pockets in templates.Items.Values.Where(IsPocketTemplate).ToArray())
        {
            pocketCount++;
            newSlotCount += AddMissingSpecialSlots(pockets);
        }

        if (pocketCount == 0)
        {
            logger.Warning(
                $"{ComfortKitInfo.LogPrefix} Extended Special Slots found no compatible Pockets templates."
            );
            return Task.CompletedTask;
        }

        var detail = newSlotCount == 0
            ? $"{pocketCount} compatible Pockets template(s) already had six slots."
            : $"Added {newSlotCount} slot(s) to {pocketCount} compatible Pockets template(s).";

        logger.Success($"{ComfortKitInfo.LogPrefix} Extended Special Slots: {detail}");
        return Task.CompletedTask;
    }

    private int AddMissingSpecialSlots(TemplateItem pockets)
    {
        var slots = pockets.Properties?.Slots?.Where(slot => slot is not null).ToList();
        if (slots is null)
        {
            return 0;
        }

        var occupiedNumbers = slots
            .Select(slot => ReadSpecialSlotNumber(slot.Name))
            .Where(number => number > 0)
            .ToHashSet();

        var templateSlot = slots.FirstOrDefault(slot =>
            string.Equals(slot.Name, "SpecialSlot3", StringComparison.OrdinalIgnoreCase));

        if (templateSlot is null)
        {
            return 0;
        }

        var added = 0;
        for (var number = 4; number <= 6; number++)
        {
            if (occupiedNumbers.Contains(number))
            {
                continue;
            }

            var slot = cloner.Clone(templateSlot)
                ?? throw new InvalidOperationException("The Special Slot template could not be cloned.");
            slot.Name = $"SpecialSlot{number}";
            slot.Id = MakeSlotId(pockets.Id, number);
            slot.Parent = pockets.Id;
            slots.Add(slot);
            added++;
        }

        pockets.Properties!.Slots = slots;
        return added;
    }

    private void PrepareSvmPockets()
    {
        var svm = FindLoadedAssembly(SvmAssembly);
        if (svm is null)
        {
            return;
        }

        SvmPocketChoice choice;
        try
        {
            if (!TryReadSvmPocketChoice(svm.Location, out choice))
            {
                return;
            }
        }
        catch (Exception exception)
        {
            logger.Warning(
                $"{ComfortKitInfo.LogPrefix} SVM Pockets compatibility was skipped: {exception.Message}"
            );
            return;
        }

        var prepared = 0;
        if (choice.UsePmcPockets)
        {
            prepared += AddPocketPlaceholder(DefaultPmcPockets, SvmPmcPockets) ? 1 : 0;
        }

        if (choice.UseScavPockets)
        {
            prepared += AddPocketPlaceholder(DefaultScavPockets, SvmScavPockets) ? 1 : 0;
        }

        if (prepared > 0)
        {
            logger.Info(
                $"{ComfortKitInfo.LogPrefix} Prepared {prepared} SVM Pockets template(s) before profile migration."
            );
        }
    }

    private bool AddPocketPlaceholder(MongoId sourceId, MongoId targetId)
    {
        if (templates.Items.ContainsKey(targetId)
            || !templates.Items.TryGetValue(sourceId, out var source))
        {
            return false;
        }

        var placeholder = cloner.Clone(source)
            ?? throw new InvalidOperationException($"Pockets template {sourceId} could not be cloned.");
        placeholder.Id = targetId;
        templates.Items[targetId] = placeholder;
        return true;
    }

    private static bool IsPocketTemplate(TemplateItem item)
    {
        if (!string.Equals(item.Properties?.Name, "Pockets", StringComparison.OrdinalIgnoreCase)
            || item.Properties?.Slots is null)
        {
            return false;
        }

        var numbers = item.Properties.Slots
            .Where(slot => slot is not null)
            .Select(slot => ReadSpecialSlotNumber(slot!.Name))
            .ToHashSet();

        return numbers.IsSupersetOf([1, 2, 3]);
    }

    private static int ReadSpecialSlotNumber(string? name)
    {
        const string prefix = "SpecialSlot";
        if (name is null || !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(name[prefix.Length..], out var number) ? number : 0;
    }

    private static MongoId MakeSlotId(MongoId pocketsId, int slotNumber)
    {
        var text = $"{ComfortKitInfo.Guid}|{pocketsId}|SpecialSlot{slotNumber}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return new MongoId(Convert.ToHexString(digest)[..24].ToLowerInvariant());
    }

    private static Assembly? FindLoadedAssembly(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
            string.Equals(assembly.GetName().Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private bool ArmoryProvidesExtendedSlots(Assembly armory)
    {
        var modDirectory = IOPath.GetDirectoryName(armory.Location);
        if (string.IsNullOrWhiteSpace(modDirectory))
        {
            return true;
        }

        var settingsFile = IOPath.Combine(modDirectory, "config", "settings.json");
        if (!File.Exists(settingsFile))
        {
            return true;
        }

        try
        {
            using var settings = ReadJson(settingsFile);
            if (TryGetProperty(settings.RootElement, "loadExtendedSpecialSlots", out var enabled)
                && (enabled.ValueKind == JsonValueKind.True
                    || enabled.ValueKind == JsonValueKind.False))
            {
                return enabled.GetBoolean();
            }

            logger.Warning(
                $"{ComfortKitInfo.LogPrefix} SALCO's ARMORY Special Slots setting is missing or invalid; keeping ownership with ARMORY."
            );
        }
        catch (Exception exception)
        {
            logger.Warning(
                $"{ComfortKitInfo.LogPrefix} Could not read SALCO's ARMORY Special Slots setting; keeping ownership with ARMORY: {exception.Message}"
            );
        }

        return true;
    }

    private static bool TryReadSvmPocketChoice(string assemblyPath, out SvmPocketChoice choice)
    {
        choice = default;

        var modDirectory = IOPath.GetDirectoryName(assemblyPath);
        if (string.IsNullOrWhiteSpace(modDirectory))
        {
            return false;
        }

        var loaderFile = IOPath.Combine(modDirectory, "Loader", "loader.json");
        if (!File.Exists(loaderFile))
        {
            return false;
        }

        using var loader = ReadJson(loaderFile);
        if (!TryGetProperty(loader.RootElement, "CurrentlySelectedPreset", out var selected)
            || selected.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var presetName = selected.GetString();
        if (string.IsNullOrWhiteSpace(presetName))
        {
            return false;
        }

        if (!presetName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            presetName += ".json";
        }

        if (!string.Equals(IOPath.GetFileName(presetName), presetName, StringComparison.Ordinal))
        {
            return false;
        }

        var presetFile = IOPath.Combine(modDirectory, "Presets", presetName);
        if (!File.Exists(presetFile))
        {
            return false;
        }

        using var preset = ReadJson(presetFile);
        choice = new SvmPocketChoice(
            SectionEnabled(preset.RootElement, "CSM", "EnableCSM", "CustomPocket"),
            SectionEnabled(preset.RootElement, "Scav", "EnableScav", "ScavCustomPockets")
        );
        return true;
    }

    private static JsonDocument ReadJson(string path)
    {
        return JsonDocument.Parse(
            File.ReadAllText(path),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            }
        );
    }

    private static bool SectionEnabled(JsonElement root, string sectionName, params string[] flags)
    {
        if (!TryGetProperty(root, sectionName, out var section)
            || section.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return flags.All(flag =>
            TryGetProperty(section, flag, out var value)
            && value.ValueKind == JsonValueKind.True);
    }

    private static bool TryGetProperty(JsonElement source, string name, out JsonElement value)
    {
        foreach (var property in source.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private readonly record struct SvmPocketChoice(bool UsePmcPockets, bool UseScavPockets);
}
