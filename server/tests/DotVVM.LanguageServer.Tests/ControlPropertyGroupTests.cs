using DotVVM.LanguageServer.Analysis;
using DotVVM.LanguageServer.Configuration;
using DotVVM.LanguageServer.Model;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

/// <summary>
/// Property families — Class-active, Style-width, Param-Id. Measured over DotVVM 4.3.17: the
/// Class-, Style- and html: families sit on 34 of the framework's 56 controls, so leaving them
/// out cost the offer its most common entries after the properties themselves.
/// </summary>
public class ControlPropertyGroupTests : IDisposable
{
    private readonly string _dir;

    public ControlPropertyGroupTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "dotvvm-ls-groups-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "sample_serialized_config.json"),
            Path.Combine(_dir, SerializedConfigSource.FileName));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static ControlRegistry Registry(params ControlInfo[] controls) => new(
        new[] { new ControlRegistration("dot", "DotVVM.Framework.Controls", "DotVVM.Framework", null, null) },
        controls);

    private static ControlInfo Label(params ControlPropertyGroup[] groups) =>
        new("DotVVM.Framework.Controls.Label", null, null,
            new[] { new ControlProperty("Text") }, groups);

    private static IReadOnlyList<CompletionSuggestion> InLabel(ControlRegistry registry) =>
        ControlCompletion.Suggest(
            registry, new CompletionContext(CompletionTarget.AttributeName, "dot", "Label"));

    [Fact]
    public void OffersTheFamilyByItsPrefix()
    {
        var registry = Registry(Label(new ControlPropertyGroup("Class-", "CssClasses")));

        Assert.Contains(InLabel(registry), s => s.Label == "Class-");
    }

    /// <summary>
    /// The word after the prefix is the author's own, so the snippet stops there first and only
    /// then inside the value.
    /// </summary>
    [Fact]
    public void LeavesTheCaretWhereTheNameGoes()
    {
        var registry = Registry(Label(new ControlPropertyGroup("Class-", "CssClasses")));

        var item = InLabel(registry).Single(s => s.Label == "Class-");
        Assert.Equal("Class-$1=\"$0\"", item.InsertText);
        Assert.True(item.IsSnippet);
    }

    /// <summary>
    /// Class-active next to Class-invalid is the ordinary way to write them, so a family already
    /// used must stay in the offer - unlike a property, which can appear once.
    /// </summary>
    [Fact]
    public void OffersTheFamilyEvenWhenItIsAlreadyWritten()
    {
        var registry = Registry(Label(new ControlPropertyGroup("Class-", "CssClasses")));

        var suggestions = ControlCompletion.Suggest(registry, new CompletionContext(
            CompletionTarget.AttributeName, "dot", "Label", new[] { "Class-active" }));

        Assert.Contains(suggestions, s => s.Label == "Class-");
    }

    [Fact]
    public void LeavesOutAFamilyThatIsWrittenAsAChildElement()
    {
        var registry = Registry(Label(
            new ControlPropertyGroup("template-", "Templates", PropertyUsage.InnerElement)));

        Assert.DoesNotContain(InLabel(registry), s => s.Label == "template-");
    }

    /// <summary>
    /// The serialized configuration names only the type that declares the family. Label declares
    /// none: Class- comes from HtmlGenericControl, three classes above it.
    /// </summary>
    [Fact]
    public void InheritsTheFamiliesFromTheBaseType()
    {
        var registry = Registry(
            new ControlInfo("DotVVM.Framework.Controls.Label",
                            "DotVVM.Framework.Controls.HtmlGenericControl, DotVVM.Framework",
                            null, new[] { new ControlProperty("Text") }),
            new ControlInfo("DotVVM.Framework.Controls.HtmlGenericControl", null, null,
                            new[] { new ControlProperty("Visible") },
                            new[] { new ControlPropertyGroup("Class-", "CssClasses") }));

        var suggestions = InLabel(registry);

        Assert.Contains(suggestions, s => s.Label == "Class-");
        Assert.Contains(suggestions, s => s.Label == "Visible");
        Assert.Contains(suggestions, s => s.Label == "Text");
    }

    /// <summary>
    /// A registry merged from three sources could hold a type whose base points back at it. No
    /// real chain does, but one bad entry must not spin the walk for ever.
    /// </summary>
    [Fact]
    public void SurvivesACycleInTheBaseTypeChain()
    {
        var registry = Registry(
            new ControlInfo("DotVVM.Framework.Controls.A",
                            "DotVVM.Framework.Controls.B", null,
                            new[] { new ControlProperty("FromA") }),
            new ControlInfo("DotVVM.Framework.Controls.B",
                            "DotVVM.Framework.Controls.A", null,
                            new[] { new ControlProperty("FromB") }));

        var control = registry.GetControl("dot", "A");

        Assert.NotNull(control);
        Assert.Equal(new[] { "FromA", "FromB" }, control!.Properties.Select(p => p.Name));
    }

    /// <summary>
    /// The nearest declaration wins: an override closer to the tag is the one that applies.
    /// </summary>
    [Fact]
    public void KeepsTheNearestDeclarationOfAnOverriddenProperty()
    {
        var registry = Registry(
            new ControlInfo("DotVVM.Framework.Controls.Label",
                            "DotVVM.Framework.Controls.Base", null,
                            new[] { new ControlProperty("Text", Value: PropertyValue.BindingOnly) }),
            new ControlInfo("DotVVM.Framework.Controls.Base", null, null,
                            new[] { new ControlProperty("Text") }));

        var control = registry.GetControl("dot", "Label");

        var text = Assert.Single(control!.Properties, p => p.Name == "Text");
        Assert.Equal(PropertyValue.BindingOnly, text.Value);
    }

    /// <summary>
    /// An element written in a view compiles to HtmlGenericControl, so Class-required belongs on
    /// a plain &lt;label&gt; just as much as on a dot:Label - and that is where a real project
    /// writes it. The framework's own resolver accepts it.
    /// </summary>
    [Fact]
    public void OffersTheFamiliesOnAPlainHtmlElement()
    {
        var registry = Registry(
            new ControlInfo("DotVVM.Framework.Controls.HtmlGenericControl", null, null,
                            new[] { new ControlProperty("Visible") },
                            new[] { new ControlPropertyGroup("Class-", "CssClasses"),
                                    new ControlPropertyGroup("Style-", "CssStyles") }));

        var suggestions = ControlCompletion.Suggest(
            registry, new CompletionContext(CompletionTarget.AttributeName, null, "label"));

        Assert.Contains(suggestions, s => s.Label == "Class-");
        Assert.Contains(suggestions, s => s.Label == "Style-");
        // Its properties stay with the IDE's own HTML support; only what DotVVM adds is ours
        Assert.DoesNotContain(suggestions, s => s.Label == "Visible");
    }

    /// <summary>
    /// Tier 1 lists the controls a view writes by name, and HtmlGenericControl is not one of
    /// them - so before the project has been built there is nothing to say about a plain
    /// element, and saying nothing beats guessing.
    /// </summary>
    [Fact]
    public void SaysNothingOnAPlainElementWhenTheControlIsUnknown()
    {
        var suggestions = ControlCompletion.Suggest(
            Registry(), new CompletionContext(CompletionTarget.AttributeName, null, "label"));

        Assert.DoesNotContain(suggestions, s => s.Label.EndsWith("-"));
    }

    /// <summary>
    /// A family with an empty prefix means "any attribute goes here" — HtmlGenericControl
    /// declares one. There is nothing to offer for it, and a blank entry in the list would be
    /// worse than none.
    /// </summary>
    [Fact]
    public async Task ReadsNoEmptyPrefixFromTheSerializedConfiguration()
    {
        var registry = await new SerializedConfigSource().LoadAsync(_dir, default);

        var html = registry!.Controls.Single(
            c => c.FullTypeName == "DotVVM.Framework.Controls.HtmlGenericControl");

        Assert.Contains(html.Groups, g => g.Prefix == "html:");
        Assert.Contains(html.Groups, g => g.Prefix == "Class-");
        Assert.DoesNotContain(html.Groups, g => g.Prefix.Length == 0);
    }

    /// <summary>
    /// The declaring type carries the family, the tag inherits it: dot:Button reaches
    /// HtmlGenericControl through ButtonBase.
    /// </summary>
    [Fact]
    public async Task ReachesTheFamilyThroughTheWholeChain()
    {
        var registry = await new SerializedConfigSource().LoadAsync(_dir, default);

        var button = registry!.GetControl("dot", "Button");

        Assert.Contains(button!.Groups, g => g.Prefix == "Class-");
        Assert.Contains(button.Properties, p => p.Name == "Text");     // its own
        Assert.Contains(button.Properties, p => p.Name == "Click");    // ButtonBase
        Assert.Contains(button.Properties, p => p.Name == "Visible");  // HtmlGenericControl
    }
}
