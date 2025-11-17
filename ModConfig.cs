namespace JeroBackpack;
public class ModConfig
{
    public Dictionary<string, Dictionary<string, SizeMapping>> SizeMappings { get; set; } = new();
}

public class SizeMapping
{
    public int OldHorizontal { get; set; }
    public int OldVertical { get; set; }
    public int NewHorizontal { get; set; }
    public int NewVertical { get; set; }
}

public class ItemCustomConfig
{
    public Dictionary<string, GridSize> Backpacks { get; set; } = new();
}

public class GridSize
{
    public string? ItemName { get; set; }
    public int Horizontal { get; set; }
    public int Vertical { get; set; }
}

public class BlacklistConfig
{
    public Dictionary<string, BlacklistItem> Blacklist { get; set; } = new();
}

public class BlacklistItem
{
    public string? ItemName { get; set; }
}