using DotVVM.LanguageServer.Configuration;
using DotVVM.LanguageServer.Model;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class MarkupControlResolverTests
{
    private static ControlRegistry RegistryWithMarkupControl() => new(
        new[] { new ControlRegistration("cc", null, null, "Widget", "Controls/Widget.dotcontrol") },
        new[] { new ControlInfo("App.Controls.Widget", "DotvvmMarkupControl", null,
                                new[] { new ControlProperty("Data"),
                                        new ControlProperty("Visible") }) });

    private static Func<string, string?> FileAt(string path, string content) =>
        candidate => candidate.Replace('\\', '/') == path ? content : null;

    [Fact]
    public void ResolvesPropertiesThroughTheBaseTypeDirective()
    {
        var resolved = MarkupControlResolver.Resolve(
            RegistryWithMarkupControl(),
            projectRoot: "/project",
            readFile: FileAt("/project/Controls/Widget.dotcontrol",
                "@viewModel System.Object\n@baseType App.Controls.Widget, App\n<div />"));

        var control = resolved.GetControl("cc", "Widget");
        Assert.NotNull(control);
        Assert.Contains(control!.Properties, p => p.Name == "Data");
    }

    [Fact]
    public void LeavesRegistryAloneWhenTheFileIsMissing()
    {
        // An unbuilt project or a renamed file must not bring the whole registry down
        var resolved = MarkupControlResolver.Resolve(
            RegistryWithMarkupControl(), "/project", readFile: _ => null);

        Assert.Null(resolved.GetControl("cc", "Widget"));
        Assert.True(resolved.IsKnownTag("cc", "Widget"));    // the registration stays valid
    }

    [Fact]
    public void LeavesRegistryAloneWhenTheDirectiveIsMissing()
    {
        var resolved = MarkupControlResolver.Resolve(
            RegistryWithMarkupControl(), "/project",
            readFile: _ => "@viewModel System.Object\n<div />");

        Assert.Null(resolved.GetControl("cc", "Widget"));
    }

    /// <summary>
    /// The base type may well name a class no tier could see - an assembly that failed to load,
    /// or a project that has never been built. The registration must survive that.
    /// </summary>
    [Fact]
    public void KeepsTheRegistrationWhenNoTypeMatchesTheBaseType()
    {
        var registry = new ControlRegistry(
            new[] { new ControlRegistration("cc", null, null, "Widget", "Controls/Widget.dotcontrol") },
            Array.Empty<ControlInfo>());

        var resolved = MarkupControlResolver.Resolve(
            registry, "/project",
            readFile: _ => "@baseType App.Controls.Widget, App\n<div />");

        Assert.Null(resolved.GetControl("cc", "Widget"));
        Assert.True(resolved.IsKnownTag("cc", "Widget"));
    }

    /// <summary>
    /// Src uses forward slashes even on Windows, because that is how it is written in the
    /// configuration. Path.Combine must not be handed it as an absolute path.
    /// </summary>
    [Fact]
    public void ReadsTheFileRelativeToTheProjectRoot()
    {
        string? asked = null;

        MarkupControlResolver.Resolve(
            RegistryWithMarkupControl(), "/project",
            readFile: path => { asked = path; return null; });

        Assert.Equal("/project/Controls/Widget.dotcontrol", asked?.Replace('\\', '/'));
    }

    /// <summary>
    /// DotVVM registers two diagnostics controls of its own with an embedded:// Src. They are
    /// not files, so the resolver must not go looking for them below the project root.
    /// </summary>
    [Fact]
    public void SkipsAnEmbeddedSource()
    {
        var registry = new ControlRegistry(
            new[]
            {
                new ControlRegistration("dotvvm-internal", null, null, "CompilationDiagnostic",
                    "embedded://DotVVM.Framework/Diagnostics/CompilationDiagnostic.dotcontrol")
            },
            Array.Empty<ControlInfo>());

        var resolved = MarkupControlResolver.Resolve(
            registry, "/project", readFile: _ => throw new InvalidOperationException("must not read"));

        Assert.True(resolved.IsKnownTag("dotvvm-internal", "CompilationDiagnostic"));
    }

    /// <summary>
    /// The resolver rebuilds the registry, so everything it does not touch has to be carried
    /// over. The attached properties were lost that way once, and no unit test saw it: only the
    /// whole chain over a real project showed a registry with none of them left.
    /// </summary>
    [Fact]
    public void CarriesTheAttachedPropertiesOver()
    {
        var registry = new ControlRegistry(
            new[] { new ControlRegistration("cc", null, null, "Widget", "Controls/Widget.dotcontrol") },
            Array.Empty<ControlInfo>(),
            new[] { new ControlProperty("Validation.Enabled") });

        var resolved = MarkupControlResolver.Resolve(registry, "/project", readFile: _ => null);

        Assert.Contains(resolved.AttachedProperties, p => p.Name == "Validation.Enabled");
    }

    /// <summary>
    /// The same trap as the attached properties above: the resolver rebuilds the registry, so
    /// anything it does not touch has to be handed over by name.
    /// </summary>
    [Fact]
    public void CarriesTheProjectTypesOver()
    {
        var registry = new ControlRegistry(
            new[] { new ControlRegistration("cc", null, null, "Widget", "Controls/Widget.dotcontrol") },
            Array.Empty<ControlInfo>(),
            null,
            new ProjectTypes(new[] { "App.HomeViewModel" }, new[] { "App" }));

        var resolved = MarkupControlResolver.Resolve(registry, "/project", readFile: _ => null);

        Assert.Contains("App.HomeViewModel", resolved.Types.ViewModels);
        Assert.Contains("App", resolved.Types.Namespaces);
    }

    [Fact]
    public void LeavesTypedRegistrationsUntouched()
    {
        var registry = new ControlRegistry(
            new[] { new ControlRegistration("dot", "DotVVM.Framework.Controls", "DotVVM.Framework", null, null) },
            new[] { new ControlInfo("DotVVM.Framework.Controls.Repeater", null, null,
                                    new[] { new ControlProperty("Visible") }) });

        var resolved = MarkupControlResolver.Resolve(
            registry, "/project", readFile: _ => throw new InvalidOperationException("must not read"));

        Assert.NotNull(resolved.GetControl("dot", "Repeater"));
    }
}
