using DotVVM.LanguageServer.Configuration;
using DotVVM.LanguageServer.Model;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class SerializedConfigSourceTests : IDisposable
{
    private readonly string _dir;

    public SerializedConfigSourceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "dotvvm-ls-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "sample_serialized_config.json"),
            Path.Combine(_dir, SerializedConfigSource.FileName));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task ReturnsNullWhenFileMissing()
    {
        var empty = Path.Combine(Path.GetTempPath(), "dotvvm-ls-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            Assert.Null(await new SerializedConfigSource().LoadAsync(empty, default));
        }
        finally { Directory.Delete(empty); }
    }

    [Fact]
    public async Task ReadsCustomPrefixes()
    {
        var registry = await new SerializedConfigSource().LoadAsync(_dir, default);
        Assert.NotNull(registry);
        Assert.True(registry!.IsKnownPrefix("cc"));
        Assert.True(registry.IsKnownPrefix("bp"));
    }

    [Fact]
    public async Task ReadsMarkupControlBySrc()
    {
        var registry = await new SerializedConfigSource().LoadAsync(_dir, default);
        Assert.True(registry!.IsKnownTag("cc", "Address"));
    }

    [Fact]
    public async Task ReadsTypedControl()
    {
        var registry = await new SerializedConfigSource().LoadAsync(_dir, default);
        Assert.True(registry!.IsKnownTag("cc", "Widget"));
    }

    [Fact]
    public async Task AssignsPropertiesToOwningControl()
    {
        var registry = await new SerializedConfigSource().LoadAsync(_dir, default);
        var button = registry!.GetControl("dot", "Button");
        Assert.NotNull(button);
        Assert.Contains(button!.Properties, p => p.Name == "Text");
        Assert.Contains(button.Properties, p => p.Name == "Click");
        Assert.DoesNotContain(button.Properties, p => p.Name == "Value");
    }

    [Fact]
    public async Task ReadsHowAPropertyMayBeWritten()
    {
        var registry = await new SerializedConfigSource().LoadAsync(_dir, default);
        var props = registry!.GetControl("dot", "Repeater")!.Properties;

        var template = props.Single(p => p.Name == "ItemTemplate");
        Assert.Equal(PropertyUsage.InnerElement, template.Usage);
        Assert.True(template.Required);

        Assert.Equal(PropertyValue.BindingOnly, props.Single(p => p.Name == "DataSource").Value);
        Assert.Equal(PropertyValue.HardCodedOnly, props.Single(p => p.Name == "WrapperTagName").Value);
        Assert.Equal("System.Object", props.Single(p => p.Name == "DataSource").TypeName);
    }

    /// <summary>
    /// MappingMode.Exclude means the property is never written in markup - ClientID and its
    /// kind. Dropping it here keeps every later stage from having to know about it.
    /// </summary>
    [Fact]
    public async Task LeavesOutThePropertiesThatAreNeverWritten()
    {
        var registry = await new SerializedConfigSource().LoadAsync(_dir, default);

        Assert.DoesNotContain(registry!.GetControl("dot", "Repeater")!.Properties,
                              p => p.Name == "ClientID");
    }

    [Fact]
    public async Task ReadsDefaultContentProperty()
    {
        var registry = await new SerializedConfigSource().LoadAsync(_dir, default);
        Assert.Equal("ContentTemplate", registry!.GetControl("cc", "Widget")!.DefaultContentProperty);
    }

    [Fact]
    public async Task FindsConfigInParentDirectory()
    {
        var nested = Path.Combine(_dir, "Views", "Admin");
        Directory.CreateDirectory(nested);
        var registry = await new SerializedConfigSource().LoadAsync(nested, default);
        Assert.NotNull(registry);
        Assert.True(registry!.IsKnownPrefix("cc"));
    }

    [Fact]
    public async Task ReturnsNullOnMalformedJson()
    {
        var bad = Path.Combine(Path.GetTempPath(), "dotvvm-ls-bad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(bad);
        await File.WriteAllTextAsync(Path.Combine(bad, SerializedConfigSource.FileName), "{ not json");
        try
        {
            Assert.Null(await new SerializedConfigSource().LoadAsync(bad, default));
        }
        finally { Directory.Delete(bad, recursive: true); }
    }

    [Fact]
    public async Task ParsesRealWorldConfigWhenAvailable()
    {
        // A real project's directory, named by DOTVVM_LS_REAL_PROJECT. The test is skipped without
        // it: no serialized config ships with the repository, because DotVVM writes the file only
        // once the application has actually run.
        var realDir = Environment.GetEnvironmentVariable("DOTVVM_LS_REAL_PROJECT");
        if (string.IsNullOrEmpty(realDir)) return;
        if (!File.Exists(Path.Combine(realDir, SerializedConfigSource.FileName))) return;

        var registry = await new SerializedConfigSource().LoadAsync(realDir, default);
        Assert.NotNull(registry);
        Assert.True(registry!.IsKnownPrefix("dot"));
        Assert.True(registry.GetTagsForPrefix("dot").Count > 50);

        // In a real file the properties are nested under the type, not held in a flat key.
        // Without this assertion a parser unaware of them would pass and hover would stay empty.
        var button = registry.GetControl("dot", "Button");
        Assert.NotNull(button);
        Assert.Contains(button!.Properties, p => p.Name == "ButtonTagName");
    }
}
