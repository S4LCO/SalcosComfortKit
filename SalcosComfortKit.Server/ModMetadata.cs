using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace SalcosComfortKit.Server;

public sealed record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = ComfortKitInfo.Guid;
    public string Name { get; init; } = ComfortKitInfo.DisplayName;
    public string Author { get; init; } = "Salco";
    public string License { get; init; } = "MIT";
    public Version Version { get; init; } = new(ComfortKitInfo.Version);
    public Range SptVersion { get; init; } = new("~4.1.0");
    public string? Url { get; init; } = null;
    public List<string>? Contributors { get; init; } = null;
    public Dictionary<string, Range>? ModDependencies { get; init; } = null;
    public bool HasPrepatcher { get; init; } = false;

    public List<string>? Incompatibilities { get; init; } =
    [
        "com.boogle.oldtarkovmovement"
    ];
}

