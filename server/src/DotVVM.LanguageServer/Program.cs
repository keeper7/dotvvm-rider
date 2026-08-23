using DotVVM.LanguageServer.Compilation;
using DotVVM.LanguageServer.Configuration;
using DotVVM.LanguageServer.Documents;
using DotVVM.LanguageServer.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;

namespace DotVVM.LanguageServer;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var server = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(options => options
            .WithInput(Console.OpenStandardInput())
            .WithOutput(Console.OpenStandardOutput())
            .ConfigureLogging(logging => logging
                .AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace)
                .SetMinimumLevel(LogLevel.Warning))
            .WithServerInfo(new ServerInfo { Name = "dotvvm-language-server", Version = "0.3.0" })
            .WithServices(services =>
            {
                services.AddSingleton<DocumentStore>();
                services.AddSingleton(ProjectConfigurationProvider.CreateDefault());
                services.AddSingleton<LiveValidation>();
            })
            .WithHandler<DocumentSyncHandler>()
            .WithHandler<CompletionHandler>()
            .WithHandler<DefinitionHandler>()
            .WithHandler<HoverHandler>()
        );

        await server.WaitForExit;
    }
}
