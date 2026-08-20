using DotVVM.LanguageServer.Documents;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class DocumentStoreTests
{
    [Fact]
    public void StoresAndReadsDocument()
    {
        var store = new DocumentStore();
        store.Set("file:///a.dothtml", "<dot:Button />");
        Assert.Equal("<dot:Button />", store.Get("file:///a.dothtml"));
    }

    [Fact]
    public void OverwritesOnUpdate()
    {
        var store = new DocumentStore();
        store.Set("file:///a.dothtml", "one");
        store.Set("file:///a.dothtml", "two");
        Assert.Equal("two", store.Get("file:///a.dothtml"));
    }

    [Fact]
    public void RemovesOnClose()
    {
        var store = new DocumentStore();
        store.Set("file:///a.dothtml", "x");
        store.Remove("file:///a.dothtml");
        Assert.Null(store.Get("file:///a.dothtml"));
    }

    [Fact]
    public void ReturnsNullForUnknownDocument()
    {
        Assert.Null(new DocumentStore().Get("file:///missing.dothtml"));
    }
}
