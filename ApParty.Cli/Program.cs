using ApParty.Core;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddApPartyCore();

using var provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateOnBuild = true,
    ValidateScopes = true,
});

Console.WriteLine("apparty — Archipelago Party Launcher CLI");
Console.WriteLine("No commands yet.");
return 0;
