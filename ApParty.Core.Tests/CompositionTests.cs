using ApParty.Core;
using Microsoft.Extensions.DependencyInjection;

namespace ApParty.Core.Tests;

public class CompositionTests
{
    [Fact]
    public void Core_service_graph_builds_with_validation()
    {
        var services = new ServiceCollection().AddApPartyCore();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        Assert.NotNull(provider);
    }
}
