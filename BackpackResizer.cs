using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace JeroBackpack;

public record ResizeResult(int SuccessCount, int SkippedCount);

// Origem do tamanho final de cada mochila, exibida na página web
public enum BackpackSource
{
    Mapping,
    Override,
    Blacklist,
    MultipleGrids,
    NoMapping,
}

public record BackpackEntry(string ItemId, string? ItemName, int OriginalH, int OriginalV, int FinalH, int FinalV, BackpackSource Source);

[Injectable(InjectionType.Singleton)]
public class BackpackResizer(
    ISptLogger<BackpackResizer> logger,
    TemplateTable templateTable,
    BackpackConfigService configService
)
{
    public const string BACKPACK_PARENT_ID = "5448e53e4bdc2d60728b4567";

    // Tamanho original de cada grid alterada, para permitir reverter e reaplicar
    private readonly Dictionary<string, (int H, int V)> _originalSizes = [];

    private readonly List<BackpackEntry> _entries = [];

    public ResizeResult LastResult { get; private set; } = new(0, 0);
    public IReadOnlyList<BackpackEntry> Entries => _entries;

    public ResizeResult Apply()
    {
        // Sempre parte dos tamanhos originais, senão reaplicar aumentaria a grid de novo
        Revert();
        _entries.Clear();

        var sizeMappingConfig = configService.SizeMappingConfig;
        var itemCustomConfig = configService.ItemCustomConfig;
        var blacklistConfig = configService.BlacklistConfig;

        logger.Info("[JERO] JeroBackpack: Starting backpack resizing...");
        int successCount = 0;
        int skippedCount = 0;

        // Verificar se há mapeamento de tamanhos para o Parent ID de backpack
        if (sizeMappingConfig.SizeMappings == null || !sizeMappingConfig.SizeMappings.TryGetValue(BACKPACK_PARENT_ID, out var sizeMappings))
        {
            logger.Warning($"[JERO] JeroBackpack: No size mappings found for Parent ID {BACKPACK_PARENT_ID} in config.json.");
            LastResult = new ResizeResult(0, 0);
            return LastResult;
        }

        // Iterar sobre todos os itens no banco de dados
        foreach (var itemEntry in templateTable.Items)
        {
            var item = itemEntry.Value;
            string itemId = itemEntry.Key;

            // Verificar se é uma mochila (Parent ID = BACKPACK_PARENT_ID)
            if (item.Parent != BACKPACK_PARENT_ID)
            {
                continue;
            }

            // Verificar se está na blacklist
            if (blacklistConfig.Blacklist != null && blacklistConfig.Blacklist.ContainsKey(itemId))
            {
                var blacklistedGrid = item.Properties?.Grids?.FirstOrDefault()?.Properties;
                int h = blacklistedGrid?.CellsH ?? 0;
                int v = blacklistedGrid?.CellsV ?? 0;
                _entries.Add(new BackpackEntry(itemId, item.Name, h, v, h, v, BackpackSource.Blacklist));
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
                _entries.Add(new BackpackEntry(itemId, item.Name, 0, 0, 0, 0, BackpackSource.MultipleGrids));
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

            int newH;
            int newV;
            BackpackSource source;

            // Verificar se tem customização específica no item.json
            if (itemCustomConfig.Backpacks != null && itemCustomConfig.Backpacks.TryGetValue(itemId, out var customSize))
            {
                newH = customSize.Horizontal;
                newV = customSize.Vertical;
                source = BackpackSource.Override;
            }
            else
            {
                // Usar mapeamento de tamanhos baseado no tamanho antigo
                string sizeKey = $"{oldH}x{oldV}";
                if (!sizeMappings.TryGetValue(sizeKey, out var sizeMapping))
                {
                    logger.Debug($"[JERO] JeroBackpack: No mapping found for size {sizeKey} of backpack '{item.Name}' (ID: {itemId}).");
                    _entries.Add(new BackpackEntry(itemId, item.Name, oldH, oldV, oldH, oldV, BackpackSource.NoMapping));
                    continue;
                }

                newH = sizeMapping.NewHorizontal;
                newV = sizeMapping.NewVertical;
                source = BackpackSource.Mapping;
            }

            _originalSizes[itemId] = (oldH, oldV);
            mainGrid.Properties.CellsH = newH;
            mainGrid.Properties.CellsV = newV;
            _entries.Add(new BackpackEntry(itemId, item.Name, oldH, oldV, newH, newV, source));
            successCount++;
        }

        logger.Success($"[JERO] JeroBackpack: Completed! {successCount} backpacks modified, {skippedCount} backpacks ignored (blacklist or multiple grids).");
        LastResult = new ResizeResult(successCount, skippedCount);
        return LastResult;
    }

    public void Revert()
    {
        foreach (var (itemId, (h, v)) in _originalSizes)
        {
            if (!templateTable.Items.TryGetValue(itemId, out var item))
            {
                continue;
            }

            var mainGrid = item.Properties?.Grids?.FirstOrDefault();
            if (mainGrid?.Properties == null)
            {
                continue;
            }

            mainGrid.Properties.CellsH = h;
            mainGrid.Properties.CellsV = v;
        }

        _originalSizes.Clear();
    }
}
