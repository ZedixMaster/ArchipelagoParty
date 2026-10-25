using ApParty.Cli;
using ApParty.Core;
using Microsoft.Extensions.DependencyInjection;

using var services = new ServiceCollection()
    .AddApPartyCore(Path.Combine(AppContext.BaseDirectory, "kit"))
    .BuildServiceProvider(new ServiceProviderOptions
    {
        ValidateOnBuild = true,
        ValidateScopes = true,
    });

return CliApp.Run(args, services, Console.Out, Console.Error);
