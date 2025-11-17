using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using System.Reflection;

namespace JeroBackpack;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 10)]
public class JeroBackpack(
    ISptLogger<JeroBackpack> logger,
    DatabaseServer databaseServer,
    ModHelper modHelper
) : IOnLoad
{
    private const string BACKPACK_PARENT_ID = "5448e53e4bdc2d60728b4567";
    
    private ModConfig? _sizeMappingConfig;
    private ItemCustomConfig? _itemCustomConfig;
    private BlacklistConfig? _blacklistConfig;

    public Task OnLoad()
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var configFolderPath = Path.Combine(modPath, "config");

        // Carregar config.json (mapeamento de tamanhos)
        try
        {
            _sizeMappingConfig = modHelper.GetJsonDataFromFile<ModConfig>(configFolderPath, "config.json");
            if (_sizeMappingConfig == null)
            {
                logger.Warning("[JERO] JeroBackpack: config.json not found or empty. Using default values.");
                _sizeMappingConfig = new ModConfig();
            }
        }
        catch (Exception e)
        {
            logger.Error($"[JERO] JeroBackpack: ERROR loading config.json. Details: {e.Message}");
            _sizeMappingConfig = new ModConfig();
        }

        // Carregar item.json (customizações específicas)
        try
        {
            _itemCustomConfig = modHelper.GetJsonDataFromFile<ItemCustomConfig>(configFolderPath, "item.json");
            if (_itemCustomConfig == null)
            {
                logger.Info("[JERO] JeroBackpack: item.json not found. No specific customizations will be applied.");
                _itemCustomConfig = new ItemCustomConfig();
            }
        }
        catch (Exception e)
        {
            logger.Warning($"[JERO] JeroBackpack: ERROR loading item.json. Details: {e.Message}");
            _itemCustomConfig = new ItemCustomConfig();
        }

        // Carregar blacklist.json
        try
        {
            _blacklistConfig = modHelper.GetJsonDataFromFile<BlacklistConfig>(configFolderPath, "blacklist.json");
            if (_blacklistConfig == null)
            {
                logger.Info("[JERO] JeroBackpack: blacklist.json not found. No backpacks will be blocked.");
                _blacklistConfig = new BlacklistConfig();
            }
        }
        catch (Exception e)
        {
            logger.Warning($"[JERO] JeroBackpack: ERROR loading blacklist.json. Details: {e.Message}");
            _blacklistConfig = new BlacklistConfig();
        }

        logger.Info("[JERO] JeroBackpack: Starting backpack resizing...");
        var itemsDb = databaseServer.GetTables().Templates.Items;
        int successCount = 0;
        int skippedCount = 0;

        // Verificar se há mapeamento de tamanhos para o Parent ID de backpack
        if (_sizeMappingConfig?.SizeMappings == null || !_sizeMappingConfig.SizeMappings.TryGetValue(BACKPACK_PARENT_ID, out var sizeMappings))
        {
            logger.Warning($"[JERO] JeroBackpack: No size mappings found for Parent ID {BACKPACK_PARENT_ID} in config.json.");
            return Task.CompletedTask;
        }

        // Iterar sobre todos os itens no banco de dados
        foreach (var itemEntry in itemsDb)
        {
            var item = itemEntry.Value;
            string itemId = itemEntry.Key;

            // Verificar se é uma mochila (Parent ID = BACKPACK_PARENT_ID)
            if (item.Parent != BACKPACK_PARENT_ID)
            {
                continue;
            }

            // Verificar se está na blacklist
            if (_blacklistConfig?.Blacklist != null && _blacklistConfig.Blacklist.ContainsKey(itemId))
            {
                skippedCount++;
                continue;
            }

            // Verificar se tem múltiplos grids (não suportado)
            var grids = item.Properties?.Grids;
            if (grids == null)
            {
                continue;
            }

            var gridCount = grids.Count();
            if (gridCount == 0)
            {
                continue;
            }

            if (gridCount > 1)
            {
                skippedCount++;
                continue;
            }

            var mainGrid = grids.FirstOrDefault();
            if (mainGrid?.Properties == null)
            {
                continue;
            }

            int oldH = mainGrid.Properties.CellsH ?? 0;
            int oldV = mainGrid.Properties.CellsV ?? 0;

            if (oldH == 0 || oldV == 0)
            {
                continue;
            }

            // Verificar se tem customização específica no item.json
            if (_itemCustomConfig?.Backpacks != null && _itemCustomConfig.Backpacks.TryGetValue(itemId, out var customSize))
            {
                mainGrid.Properties.CellsH = customSize.Horizontal;
                mainGrid.Properties.CellsV = customSize.Vertical;
                successCount++;
            }
            else
            {
                // Usar mapeamento de tamanhos baseado no tamanho antigo
                string sizeKey = $"{oldH}x{oldV}";
                if (sizeMappings.TryGetValue(sizeKey, out var sizeMapping))
                {
                    mainGrid.Properties.CellsH = sizeMapping.NewHorizontal;
                    mainGrid.Properties.CellsV = sizeMapping.NewVertical;
                    successCount++;
                }
                else
                {
                    logger.Debug($"[JERO] JeroBackpack: No mapping found for size {sizeKey} of backpack '{item.Name}' (ID: {itemId}).");
                }
            }
        }

        logger.Success($"[JERO] JeroBackpack: Completed! {successCount} backpacks modified, {skippedCount} backpacks ignored (blacklist or multiple grids).");
        return Task.CompletedTask;
    }
}