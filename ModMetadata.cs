using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Web;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace JeroBackpack;
    
public record ModMetadata : IModMetadata, IModBlazorMetadata
{
    public string ModGuid { get; init; } = "com.jero.jerobackpack";
    public string Name { get; init; } = "jerobackpack";
    public string Author { get; init; } = "jero";
    public List<string>? Contributors { get; init; }
    public Version Version { get; init; } = new("4.1.1");
    public Range SptVersion { get; init; } = new("~4.1.6");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; }

    // Null usa o nome do assembly como URL do wwwroot (/JeroBackpack/)
    public string? WWWRootUrl { get; init; }
    public string? HomePage { get; init; } = "/jerobackpack";
    public string? HomePageDescription { get; init; } = "Configure backpack grid sizes, overrides and blacklist.";
}