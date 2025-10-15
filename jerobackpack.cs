using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using System.Reflection;

namespace _jerobackpack;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.jero.jerobackpack";
    public override string Name { get; init; } = "jerobackpack";
    public override string Author { get; init; } = "jero";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("2.0.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; }
    public override string? License { get; init; } = "MIT";
}

public class ModConfig
{
    public Dictionary<string, GridSize> Backpacks { get; set; } = new();
}

//public record GridSize(int Horizontal, int Vertical);

public class GridSize
{
    // This property is for user reference in the config file.
    // Our mod's logic will not use it.
    public string ItemName { get; set; }

    public int Horizontal { get; set; }
    public int Vertical { get; set; }
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class CustomItemServiceExample(
    ISptLogger<CustomItemServiceExample> logger,
    DatabaseServer databaseServer,
    ModHelper modHelper
    )
    : IOnLoad
{
    private ModConfig _config;

    public Task OnLoad()
    {
        try
        {
            var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

            var configFolderPath = Path.Combine(modPath, "config");

            _config = modHelper.GetJsonDataFromFile<ModConfig>(configFolderPath, "config.json");

            if (_config == null)
            {
                throw new Exception("Config file could not be loaded or is empty.");
            }
        }
        catch (Exception e)
        {
            logger.Error($"Jero's Backpack Mod: CRITICAL ERROR loading config.json. The mod will not apply any changes. Please check your config file for errors. Details: {e.Message}");
            return Task.CompletedTask;
        }

        logger.Info("Jero's Backpack Mod: Starting to resize multiple backpacks from config...");
        var itemsDb = databaseServer.GetTables().Templates.Items;
        int successCount = 0;

        foreach (var backpackEntry in _config.Backpacks)
        {
            string backpackId = backpackEntry.Key;
            GridSize newSize = backpackEntry.Value;

            if (itemsDb.TryGetValue(backpackId, out var backpackToChange))
            {
                var mainGrid = backpackToChange.Properties.Grids.FirstOrDefault();
                if (mainGrid?.Properties != null)
                {
                    int oldH = mainGrid.Properties.CellsH ?? 0;
                    int oldV = mainGrid.Properties.CellsV ?? 0;

                    mainGrid.Properties.CellsH = newSize.Horizontal;
                    mainGrid.Properties.CellsV = newSize.Vertical;

                    //logger.Info($"Resized backpack '{backpackToChange.Name}' from {oldH}x{oldV} to {newSize.Horizontal}x{newSize.Vertical}.");
                    successCount++;
                }
                else
                {
                    logger.Warning($"Backpack '{backpackToChange.Name}' (ID: {backpackId}) was found, but it has no grid to modify.");
                }
            }
            else
            {
                logger.Warning($"Jero's Backpack Mod: Backpack with ID '{backpackId}' not found in database, skipping.");
            }
        }

        logger.Success($"Jero's Backpack Mod: Finished! Successfully modified {successCount} out of {_config.Backpacks.Count} backpacks from config.");
        return Task.CompletedTask;
    }
}