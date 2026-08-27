using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace JeroBackpack;

public sealed record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.jero.jerobackpack";
    public string Name { get; init; } = "jerobackpack";
    public string Author { get; init; } = "jero";
    public List<string>? Contributors { get; init; }
    public Version Version { get; init; } = new("3.0.1");
    public Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
}
