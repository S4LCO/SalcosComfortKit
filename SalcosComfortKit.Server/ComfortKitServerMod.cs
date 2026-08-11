using SPTarkov.DI.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.DI;

namespace SalcosComfortKit.Server;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad)]
public sealed class ComfortKitServerMod(ISptLogger<ComfortKitServerMod> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        logger.Success(
            $"{ComfortKitInfo.LogPrefix} {ComfortKitInfo.DisplayName} {ComfortKitInfo.Version} server component loaded."
        );

        return Task.CompletedTask;
    }
}
