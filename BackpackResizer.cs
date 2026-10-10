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

public record BackpackEntry(string ItemId, string? ItemName, int GridCount, int OriginalH, int OriginalV, int FinalH, int FinalV, BackpackSource Source);

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

    // Lista substituída a cada Apply, para a página nunca ler uma lista pela metade
    private IReadOnlyList<BackpackEntry> _entries = [];

    // A página web pode reaplicar enquanto outra aba também salva
    private readonly Lock _sync = new();

    public ResizeResult LastResult { get; private set; } = new(0, 0);
    public IReadOnlyList<BackpackEntry> Entries => _entries;

    /// <summary>
    /// Decide o tamanho final de uma mochila. Usado tanto ao aplicar quanto na pré-visualização da página web.
    /// </summary>
    public static (int H, int V, BackpackSource Source) Resolve(
        string itemId,
        int gridCount,
        int originalH,
        int originalV,
        ModConfig sizeMappingConfig,
        ItemCustomConfig itemCustomConfig,
        BlacklistConfig blacklistConfig
    )
    {
        // Verificar se está na blacklist
        if (blacklistConfig.Blacklist != null && blacklistConfig.Blacklist.ContainsKey(itemId))
        {
            return (originalH, originalV, BackpackSource.Blacklist);
        }

        // Múltiplos grids não são suportados
        if (gridCount > 1)
        {
            return (originalH, originalV, BackpackSource.MultipleGrids);
        }

        // item.json vence config.json
        if (itemCustomConfig.Backpacks != null && itemCustomConfig.Backpacks.TryGetValue(itemId, out var customSize))
        {
            return (customSize.Horizontal, customSize.Vertical, BackpackSource.Override);
        }

        // Usar mapeamento de tamanhos baseado no tamanho antigo
        if (sizeMappingConfig.SizeMappings != null
            && sizeMappingConfig.SizeMappings.TryGetValue(BACKPACK_PARENT_ID, out var sizeMappings)
            && sizeMappings.TryGetValue($"{originalH}x{originalV}", out var sizeMapping))
        {
            return (sizeMapping.NewHorizontal, sizeMapping.NewVertical, BackpackSource.Mapping);
        }

        return (originalH, originalV, BackpackSource.NoMapping);
    }

    public ResizeResult Apply()
    {
        lock (_sync)
        {
            // Sempre parte dos tamanhos originais, senão reaplicar aumentaria a grid de novo
            RevertUnlocked();
            List<BackpackEntry> entries = [];

            var sizeMappingConfig = configService.SizeMappingConfig;
            var itemCustomConfig = configService.ItemCustomConfig;
            var blacklistConfig = configService.BlacklistConfig;

            logger.Info("[JERO] JeroBackpack: Starting backpack resizing...");
            int successCount = 0;
            int skippedCount = 0;

            // Sem mapeamento ainda aplicamos os overrides do item.json
            if (sizeMappingConfig.SizeMappings == null || !sizeMappingConfig.SizeMappings.ContainsKey(BACKPACK_PARENT_ID))
            {
                logger.Warning($"[JERO] JeroBackpack: No size mappings found for Parent ID {BACKPACK_PARENT_ID} in config.json.");
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

                var grids = item.Properties?.Grids?.ToList() ?? [];
                var mainGrid = grids.FirstOrDefault()?.Properties;
                int gridCount = grids.Count;
                int oldH = gridCount == 1 ? mainGrid?.CellsH ?? 0 : 0;
                int oldV = gridCount == 1 ? mainGrid?.CellsV ?? 0 : 0;

                var (newH, newV, source) = Resolve(itemId, gridCount, oldH, oldV, sizeMappingConfig, itemCustomConfig, blacklistConfig);

                switch (source)
                {
                    case BackpackSource.Blacklist:
                    case BackpackSource.MultipleGrids:
                        entries.Add(new BackpackEntry(itemId, item.Name, gridCount, oldH, oldV, oldH, oldV, source));
                        skippedCount++;
                        continue;
                }

                // Sem grid ou grid com tamanho 0: ignorar
                if (gridCount == 0 || mainGrid == null || oldH == 0 || oldV == 0)
                {
                    continue;
                }

                if (source == BackpackSource.NoMapping)
                {
                    logger.Debug($"[JERO] JeroBackpack: No mapping found for size {oldH}x{oldV} of backpack '{item.Name}' (ID: {itemId}).");
                    entries.Add(new BackpackEntry(itemId, item.Name, gridCount, oldH, oldV, oldH, oldV, source));
                    continue;
                }

                _originalSizes[itemId] = (oldH, oldV);
                mainGrid.CellsH = newH;
                mainGrid.CellsV = newV;
                entries.Add(new BackpackEntry(itemId, item.Name, gridCount, oldH, oldV, newH, newV, source));
                successCount++;
            }

            logger.Success($"[JERO] JeroBackpack: Completed! {successCount} backpacks modified, {skippedCount} backpacks ignored (blacklist or multiple grids).");
            _entries = entries;
            LastResult = new ResizeResult(successCount, skippedCount);
            return LastResult;
        }
    }

    public void Revert()
    {
        lock (_sync)
        {
            RevertUnlocked();
        }
    }

    private void RevertUnlocked()
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
