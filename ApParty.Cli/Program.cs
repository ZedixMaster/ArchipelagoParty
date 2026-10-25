using ApParty.Core;
using ApParty.Core.Catalog;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddApPartyCore(Path.Combine(AppContext.BaseDirectory, "kit"));

using var provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateOnBuild = true,
    ValidateScopes = true,
});

var catalog = provider.GetRequiredService<IGameCatalog>();

Console.WriteLine("apparty — Archipelago Party Launcher CLI");
Console.WriteLine();
Console.WriteLine("Games:");
foreach (var game in catalog.Games)
{
    var presets = string.Join(", ", catalog.GetPresets(game.Id).Select(p => p.Id));
    Console.WriteLine($"  {game.Id,-20} {game.DisplayName}  [presets: {presets}]");
}

return 0;
