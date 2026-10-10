using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace JeroBackpack;

[Injectable(TypePriority = OnLoadOrder.PostLoad + 10)]
public class JeroBackpack(
    BackpackConfigService configService,
    BackpackResizer resizer
) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        configService.Load();
        resizer.Apply();
        return Task.CompletedTask;
    }
}
