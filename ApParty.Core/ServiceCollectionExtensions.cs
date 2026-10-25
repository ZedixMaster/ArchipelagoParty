using ApParty.Core.Catalog;
using ApParty.Core.Validation;
using ApParty.Core.Yaml;
using Microsoft.Extensions.DependencyInjection;

namespace ApParty.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApPartyCore(this IServiceCollection services, string kitDirectory)
    {
        services.AddSingleton<IGameCatalog>(_ => GameCatalog.Load(Path.Combine(kitDirectory, "games")));
        services.AddSingleton<IYamlBuilder, YamlBuilder>();
        services.AddSingleton<IPlayerValidator, PlayerValidator>();
        return services;
    }
}
