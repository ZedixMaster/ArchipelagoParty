using ApParty.Core.Catalog;
using ApParty.Core.Models;
using ApParty.Core.Validation;
using ApParty.Core.Yaml;
using Microsoft.Extensions.DependencyInjection;

namespace ApParty.Core.Tests;

public class CompositionTests
{
    private static readonly string ShippedKitDir = Path.Combine(AppContext.BaseDirectory, "kit");

    [Fact]
    public void Core_service_graph_builds_with_validation()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider);
    }

    [Fact]
    public void Game_catalog_resolves_as_a_singleton_loaded_from_the_kit()
    {
        using var provider = BuildProvider();

        var catalog = provider.GetRequiredService<IGameCatalog>();

        Assert.Same(catalog, provider.GetRequiredService<IGameCatalog>());
        Assert.Contains(catalog.Games, g => g.Id == "twilight_princess");
    }

    [Fact]
    public void Yaml_builder_and_validator_resolve_and_work_against_the_shipped_kit()
    {
        using var provider = BuildProvider();
        var player = new PlayerSlot("Alice", "twilight_princess", "streamlined");

        Assert.Empty(provider.GetRequiredService<IPlayerValidator>().Validate([player]));
        Assert.Contains("name: Alice", provider.GetRequiredService<IYamlBuilder>().Build(player));
    }

    private static ServiceProvider BuildProvider() =>
        new ServiceCollection()
            .AddApPartyCore(ShippedKitDir)
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
}
