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

    /// <summary>
    /// The property families a control rendering HTML accepts. Measured over DotVVM 4.3.17:
    /// every control listed below carries them except Content and NamedCommand, which render no
    /// element of their own — the rest descend from HtmlGenericControl, which declares them.
    /// </summary>
    private static readonly string[] HtmlGroups = { "Class-", "Style-", "html:" };

    private static readonly string[] NoGroups = Array.Empty<string>();

    private static readonly (string Tag, string[] Props, string[] Groups)[] Controls =
    {
        ("Button",       new[] { "Text", "Click", "Enabled", "ButtonTagName", "IsSubmitButton" }, HtmlGroups),
        ("TextBox",      new[] { "Text", "Type", "Enabled", "Changed", "SelectAllOnFocus" }, HtmlGroups),
        ("Literal",      new[] { "Text", "FormatString", "RenderSpanElement" }, HtmlGroups),
        ("Repeater",     new[] { "DataSource", "ItemTemplate", "SeparatorTemplate",
                                 "EmptyDataTemplate", "WrapperTagName", "RenderAsNamedTemplate" },
                         HtmlGroups),
        ("GridView",     new[] { "DataSource", "Columns", "SortChanged", "ShowHeaderWhenNoData" }, HtmlGroups),
        // Param- and Query- are families, not properties: what follows the dash is the route's
        // own parameter name. The list used to carry "Param-Id" as if it were one.
        ("RouteLink",    new[] { "RouteName", "Text", "Enabled" },
                         new[] { "Class-", "Style-", "html:", "Param-", "Query-" }),
        ("LinkButton",   new[] { "Text", "Click", "Enabled" }, HtmlGroups),
        ("CheckBox",     new[] { "Checked", "CheckedItems", "CheckedValue", "Text", "Changed" }, HtmlGroups),
        ("RadioButton",  new[] { "Checked", "CheckedValue", "GroupName", "Text" }, HtmlGroups),
        ("ComboBox",     new[] { "DataSource", "SelectedValue", "ItemTextBinding",
                                 "ItemValueBinding", "EmptyItemText" }, HtmlGroups),
        ("ListBox",      new[] { "DataSource", "SelectedValue", "ItemTextBinding" }, HtmlGroups),
        ("Content",      new[] { "ContentPlaceHolderID" }, NoGroups),
        ("ContentPlaceHolder", new[] { "ID" }, HtmlGroups),
        ("Validator",    new[] { "Value", "InvalidCssClass", "ShowErrorMessageText" }, HtmlGroups),
        ("ValidationSummary", new[] { "IncludeErrorsFromChildren", "HideWhenValid" }, HtmlGroups),
        ("Panel",        new[] { "Visible", "Enabled", "WrapperTagName" }, HtmlGroups),
        ("HtmlLiteral",  new[] { "Html", "WrapperTagName" }, HtmlGroups),
        ("FileUpload",   new[] { "UploadedFiles", "AllowMultipleFiles", "UploadCompleted" }, HtmlGroups),
        ("UpdateProgress", new[] { "Delay" }, HtmlGroups),
        ("EmptyData",    new[] { "DataSource", "RenderWrapperTag" }, HtmlGroups),
        ("Decorator",    Array.Empty<string>(), HtmlGroups),
        ("RoleView",     new[] { "Roles", "IsMemberTemplate", "IsNotMemberTemplate" }, HtmlGroups),
        ("AuthenticatedView", new[] { "AuthenticatedTemplate", "NotAuthenticatedTemplate" }, HtmlGroups),
        ("SpaContentPlaceHolder", new[] { "DefaultRouteName" }, HtmlGroups),
        ("NamedCommand", new[] { "Command", "Name" }, NoGroups),
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
            c.Props.Select(p => new ControlProperty(p)).ToList(),
            c.Groups.Select(g => new ControlPropertyGroup(g, g)).ToList()));

        return Task.FromResult<ControlRegistry?>(new ControlRegistry(registrations, controls));
    }
}
