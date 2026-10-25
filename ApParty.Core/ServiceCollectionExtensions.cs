using ApParty.Core.Catalog;
using Microsoft.Extensions.DependencyInjection;

namespace ApParty.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApPartyCore(this IServiceCollection services, string kitDirectory)
    {
        services.AddSingleton<IGameCatalog>(_ => GameCatalog.Load(Path.Combine(kitDirectory, "games")));
        return services;
    }
}
