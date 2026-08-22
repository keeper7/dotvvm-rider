using DotVVM.LanguageServer.Model;

namespace DotVVM.LanguageServer.Configuration;

/// <summary>
/// Tier 1: a minimal knowledge of the standard DotVVM controls. Works the moment a project is
/// opened, before it has been built.
/// </summary>
public sealed class BuiltInDefaults : IConfigurationSource
{
    public string Name => "built-in";

    public bool KnowsProjectPrefixes => false;

    private const string Ns = "DotVVM.Framework.Controls";

    private static readonly (string Tag, string[] Props)[] Controls =
    {
        ("Button",       new[] { "Text", "Click", "Enabled", "ButtonTagName", "IsSubmitButton" }),
        ("TextBox",      new[] { "Text", "Type", "Enabled", "Changed", "SelectAllOnFocus" }),
        ("Literal",      new[] { "Text", "FormatString", "RenderSpanElement" }),
        ("Repeater",     new[] { "DataSource", "ItemTemplate", "SeparatorTemplate",
                                 "EmptyDataTemplate", "WrapperTagName", "RenderAsNamedTemplate" }),
        ("GridView",     new[] { "DataSource", "Columns", "SortChanged", "ShowHeaderWhenNoData" }),
        ("RouteLink",    new[] { "RouteName", "Text", "Param-Id", "Enabled", "QueryParameters" }),
        ("LinkButton",   new[] { "Text", "Click", "Enabled" }),
        ("CheckBox",     new[] { "Checked", "CheckedItems", "CheckedValue", "Text", "Changed" }),
        ("RadioButton",  new[] { "Checked", "CheckedValue", "GroupName", "Text" }),
        ("ComboBox",     new[] { "DataSource", "SelectedValue", "ItemTextBinding",
                                 "ItemValueBinding", "EmptyItemText" }),
        ("ListBox",      new[] { "DataSource", "SelectedValue", "ItemTextBinding" }),
        ("Content",      new[] { "ContentPlaceHolderID" }),
        ("ContentPlaceHolder", new[] { "ID" }),
        ("Validator",    new[] { "Value", "InvalidCssClass", "ShowErrorMessageText" }),
        ("ValidationSummary", new[] { "IncludeErrorsFromChildren", "HideWhenValid" }),
        ("Panel",        new[] { "Visible", "Enabled", "WrapperTagName" }),
        ("HtmlLiteral",  new[] { "Html", "WrapperTagName" }),
        ("FileUpload",   new[] { "UploadedFiles", "AllowMultipleFiles", "UploadCompleted" }),
        ("UpdateProgress", new[] { "Delay" }),
        ("EmptyData",    new[] { "DataSource", "RenderWrapperTag" }),
        ("Decorator",    Array.Empty<string>()),
        ("RoleView",     new[] { "Roles", "IsMemberTemplate", "IsNotMemberTemplate" }),
        ("AuthenticatedView", new[] { "AuthenticatedTemplate", "NotAuthenticatedTemplate" }),
        ("SpaContentPlaceHolder", new[] { "DefaultRouteName" }),
        ("Form",         new[] { "Enabled" }),
        ("NamedCommand", new[] { "Command", "Name" }),
    };

    public Task<ControlRegistry?> LoadAsync(string projectDir, CancellationToken ct)
    {
        var registrations = new[]
        {
            new ControlRegistration("dot", Ns, "DotVVM.Framework", null, null)
        };

        // Tier 1 knows the names and nothing else, and must not pretend otherwise: the
        // defaults of ControlProperty describe the ordinary attribute, which is what it can
        // honestly claim.
        var controls = Controls.Select(c => new ControlInfo(
            $"{Ns}.{c.Tag}", "DotvvmControl", null,
            c.Props.Select(p => new ControlProperty(p)).ToList()));

        return Task.FromResult<ControlRegistry?>(new ControlRegistry(registrations, controls));
    }
}
