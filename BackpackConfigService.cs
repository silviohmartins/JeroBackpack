using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using System.Reflection;

namespace JeroBackpack;

[Injectable(InjectionType.Singleton)]
public class BackpackConfigService(
    ISptLogger<BackpackConfigService> logger,
    ModHelper modHelper
)
{
    public ModConfig SizeMappingConfig { get; private set; } = new();
    public ItemCustomConfig ItemCustomConfig { get; private set; } = new();
    public BlacklistConfig BlacklistConfig { get; private set; } = new();

    public string ConfigFolderPath { get; } =
        Path.Combine(modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly()), "config");

    public void Load()
    {
        // Carregar config.json (mapeamento de tamanhos)
        try
        {
            var config = modHelper.GetJsonDataFromFile<ModConfig>(ConfigFolderPath, "config.json");
            if (config == null)
            {
                logger.Warning("[JERO] JeroBackpack: config.json not found or empty. Using default values.");
            }
            SizeMappingConfig = config ?? new ModConfig();
        }
        catch (Exception e)
        {
            logger.Error($"[JERO] JeroBackpack: ERROR loading config.json. Details: {e.Message}");
            SizeMappingConfig = new ModConfig();
        }

        // Carregar item.json (customizações específicas)
        try
        {
            var config = modHelper.GetJsonDataFromFile<ItemCustomConfig>(ConfigFolderPath, "item.json");
            if (config == null)
            {
                logger.Info("[JERO] JeroBackpack: item.json not found. No specific customizations will be applied.");
            }
            ItemCustomConfig = config ?? new ItemCustomConfig();
        }
        catch (Exception e)
        {
            logger.Warning($"[JERO] JeroBackpack: ERROR loading item.json. Details: {e.Message}");
            ItemCustomConfig = new ItemCustomConfig();
        }

        // Carregar blacklist.json
        try
        {
            var config = modHelper.GetJsonDataFromFile<BlacklistConfig>(ConfigFolderPath, "blacklist.json");
            if (config == null)
            {
                logger.Info("[JERO] JeroBackpack: blacklist.json not found. No backpacks will be blocked.");
            }
            BlacklistConfig = config ?? new BlacklistConfig();
        }
        catch (Exception e)
        {
            logger.Warning($"[JERO] JeroBackpack: ERROR loading blacklist.json. Details: {e.Message}");
            BlacklistConfig = new BlacklistConfig();
        }
    }
}
