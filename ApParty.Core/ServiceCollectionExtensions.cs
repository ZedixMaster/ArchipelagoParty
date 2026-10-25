using ApParty.Core.ApWorlds;
using ApParty.Core.Archipelago;
using ApParty.Core.Catalog;
using ApParty.Core.Health;
using ApParty.Core.Kit;
using ApParty.Core.Validation;
using ApParty.Core.Yaml;
using Microsoft.Extensions.DependencyInjection;

namespace ApParty.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApPartyCore(this IServiceCollection services, string kitDirectory)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(_ => KitManifest.Load(kitDirectory));
        services.AddSingleton<IGameCatalog>(_ => GameCatalog.Load(Path.Combine(kitDirectory, "games")));
        services.AddSingleton<IYamlBuilder, YamlBuilder>();
        services.AddSingleton<IPlayerValidator, PlayerValidator>();
        services.AddSingleton<IArchipelagoLocator, ArchipelagoLocator>();
        services.AddSingleton<IApWorldManager, ApWorldManager>();
        services.AddSingleton<IEnvironmentChecker, EnvironmentChecker>();
        return services;
    }
}
