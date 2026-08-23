using DotVVM.LanguageServer.Configuration;
using DotVVM.LanguageServer.Documents;
using DotVVM.LanguageServer.Handlers;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class CompletionHandlerCapabilityTests
{
    /// <summary>
    /// A client that does not ask for completion still has the registration options read from
    /// it, and dereferencing the absent capability threw - which killed `initialize` itself and
    /// left the server unable to start at all.
    /// </summary>
    [Fact]
    public void RegistrationSurvivesAClientWithoutCompletion()
    {
        var handler = new CompletionHandler(
            new DocumentStore(), ProjectConfigurationProvider.CreateDefault());

        var options = handler.GetRegistrationOptions(null!, new ClientCapabilities());

        Assert.NotNull(options.TriggerCharacters);
    }

    [Fact]
    public void RegistrationReadsSnippetSupportWhenItIsThere()
    {
        var handler = new CompletionHandler(
            new DocumentStore(), ProjectConfigurationProvider.CreateDefault());

        var capability = new CompletionCapability
        {
            CompletionItem = new CompletionItemCapabilityOptions { SnippetSupport = true }
        };

        Assert.NotNull(handler.GetRegistrationOptions(capability, new ClientCapabilities()));
    }
}
