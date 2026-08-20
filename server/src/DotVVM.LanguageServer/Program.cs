using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;

namespace DotVVM.LanguageServer;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var server = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(options => options
            .WithInput(Console.OpenStandardInput())
            .WithOutput(Console.OpenStandardOutput())
            .ConfigureLogging(logging => logging.AddConsole(o =>
            {
                // Veškeré logování musí jít na stderr — stdout patří LSP protokolu
                o.LogToStandardErrorThreshold = LogLevel.Trace;
            }))
            .WithServerInfo(new ServerInfo { Name = "dotvvm-language-server", Version = "0.1.0" })
        );

        await server.WaitForExit;
    }
}
