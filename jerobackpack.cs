using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;

namespace _jerobackpack;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.jero.jerobackpack";
    public override string Name { get; init; } = "jerobackpack";
    public override string Author { get; init; } = "jero";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("1.0.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; }
    public override string? License { get; init; } = "MIT";
}

public record GridSize(int Horizontal, int Vertical);

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class CustomItemServiceExample(
    ISptLogger<CustomItemServiceExample> logger,
    DatabaseServer databaseServer
    )
    : IOnLoad
{

    private Dictionary<MongoId, TemplateItem>? _itemsDb;

    private readonly Dictionary<string, GridSize> _backpacksToResize = new()
    {
        // IDs como Chave (Key), novos tamanhos como Valor (Value)

        // Mochilas grandes
        { "5df8a4d786f77412672a1e3b", new GridSize(6, 11) },  // 6Sh118 raid backpack 6x8
        { "5e4abc6786f77406812bd572", new GridSize(6, 11) },  // LBT-2670 Slim Field Med Pack 6x8
        { "5c0e774286f77468413cc5b2", new GridSize(6, 10) },  // Mystery Ranch Blackjack 50 6x7
        { "5ab8ebf186f7742d8b372e80", new GridSize(5, 10) },  // SSO Attack 2 raid backpack 5x7
        { "61b9e1aaef9a1b5d6a79899a", new GridSize(5, 10) },  // Santa's Bag 5x7
        { "59e763f286f7742ee57895da", new GridSize(5, 10) },  // Pilgrim tourist backpack 5x7
        { "639346cc1c8f182ad90c8972", new GridSize(5, 10) },  // Tasmanian Tiger Trooper 35 5x7

        // Médias
        { "66b5f22b78bbc0200425f904", new GridSize(5, 8) },   // Camelbak TriZip (Multicam) 5x6
        { "545cdae64bdc2d39198b4568", new GridSize(5, 8) },   // Camelbak TriZip (Foliage) 5x6
        { "628e1ffc83ec92260c0f437f", new GridSize(5, 8) },   // Gruppa 99 T30 5x6
        { "62a1b7fbc30cfa1d366af586", new GridSize(5, 8) },   // Gruppa 99 T30 (Multicam) 5x6
        { "5b44c6ae86f7742d1627baea", new GridSize(5, 8) },   // ANA Tactical Beta 2 5x6
        { "5f5e467b0bc58666c37e7821", new GridSize(5, 8) },   // Eberlestock F5 Switchblade 5x6
        { "6034d103ca006d2dca39b3f0", new GridSize(4, 9) },   // Hazard 4 Takedown (Black) 3x8
        { "6038d614d10cbf667352dd44", new GridSize(4, 9) },   // Hazard 4 Takedown (Multicam) 3x8
        { "618bb76513f5097c8d5aa2d5", new GridSize(5, 7) },   // Gruppa 99 T20 (Black/Duplicate) 5x5
        { "619cf0335771dd3c390269ae", new GridSize(5, 7) },   // Gruppa 99 T20 (Multicam) 5x5
        { "60a272cc93ef783291411d8e", new GridSize(5, 7) },   // Hazard 4 Drawbridge 5x5
        { "67458794e21e5d724e066976", new GridSize(5, 7) },   // LBT-1476A (Alpine) 5x5
        { "618cfae774bb2d036a049e7c", new GridSize(5, 7) },   // LBT-1476A (Woodland) 5x5
        { "5c0e805e86f774683f3dd637", new GridSize(5, 7) },   // 3V Gear Paratus 5x5
        { "5f5e46b96bdad616ad46d613", new GridSize(5, 6) },   // Eberlestock F4 Terminator 5x4
        { "66a9f98f3bd5a41b162030f4", new GridSize(5, 6) },   // Partisan's Bag 5x4
        { "5e997f0b86f7741ac73993e2", new GridSize(5, 6) },   // Sanitars bag 5x4
        { "60a2828e8689911a226117f9", new GridSize(5, 6) },   // Hazard 4 Pillbox 4x5
        { "5e9dcf5986f7746c417435b3", new GridSize(5, 6) },   // LBT-8005A Day Pack 4x5
        { "56e335e4d2720b6c058b456d", new GridSize(5, 6) },   // Scav backpack 4x5
        { "5ca20d5986f774331e7c9602", new GridSize(5, 6) },   // WARTECH Berkut BB102 4x5
        { "66b5f247af44ca0014063c02", new GridSize(4, 6) },   // Vertx Ready Pack (Red) 4x4
        { "628bc7fb408e2b2e9c0801b1", new GridSize(3, 7) },   // Mystery Ranch NICE COMM 3 2x7

        // Pequenas
        { "544a5cde4bdc2d39388b456b", new GridSize(4, 6) },   // Flyye MBSS backpack 4x4
        { "56e33634d2720bd8058b456b", new GridSize(4, 4) },   // Duffle bag	4x3
        { "5f5e45cc5021ce62144be7aa", new GridSize(4, 4) },   // LolKek 3F Transfer	3x4
        { "56e33680d2720be2748b4576", new GridSize(4, 3) },   // Transformer Bag 3x3
        { "5ab8ee7786f7742d8f33f0b9", new GridSize(4, 3) },   // VKBO army bag 4x2
        { "5ab8f04f86f774585f4237d8", new GridSize(3, 3) }    // Tactical sling bag 3x2

        // Nao alterar
        //{ "5d5d940f86f7742797262046", new GridSize(4, 4) },   // Oakley Mechanism
        //{ "6034d2d697633951dc245ea6", new GridSize(3, 5) },   // Eberlestock G2 Gunslinger
        //{ "656f198fb27298d6fd005466", new GridSize(2, 2) },   // Direct Action Dragon Egg Mark II
        //{ "674da9cf0cb4bcde7103c07b", new GridSize(2, 5) },   // Mystery Ranch Terraframe (Christmas)
        //{ "674da107c512807d1a0e7436", new GridSize(2, 5) },   // Mystery Ranch Terraframe (Olive)
        //{ "656e0436d44a1bb4220303a0", new GridSize(6, 2) },   // Mystery Ranch SATL Bridger
        //{ "67458730df3c1da90b0b052b", new GridSize(2, 2) }    // 5.11 Tactical RUSH 100
    };

    public Task OnLoad()
    {

        //logger.Info("Jero's Backpack Mod: Starting to resize multiple backpacks...");
        _itemsDb = databaseServer.GetTables().Templates.Items;
        int successCount = 0;

        // Itera sobre cada entrada no dicionário de mochilas
        foreach (var backpackEntry in _backpacksToResize)
        {
            string backpackId = backpackEntry.Key;
            GridSize newSize = backpackEntry.Value;

            if (_itemsDb.TryGetValue(backpackId, out var backpackToChange))
            {
                var mainGrid = backpackToChange.Properties.Grids.FirstOrDefault();
                if (mainGrid?.Properties != null)
                {
                    // Guarda o tamanho antigo para mostrar no log
                    int oldH = mainGrid.Properties.CellsH ?? 0;
                    int oldV = mainGrid.Properties.CellsV ?? 0;

                    // Aplica o novo tamanho
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

        //logger.Success($"Jero's Backpack Mod: Finished! Successfully modified {successCount} out of {_backpacksToResize.Count} backpacks.");
        return Task.CompletedTask;
    }
}