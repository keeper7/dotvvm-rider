namespace DotVVM.LanguageServer.Analysis;

/// <summary>
/// Reads the @baseType directive. For a markup control this names the code-behind class, and
/// that class is the only place its properties exist - the .dotcontrol file itself declares none.
/// </summary>
public static class BaseTypeDirective
{
    private const string DirectiveName = "@baseType";

    public static string? Parse(string text) =>
        TypeDirective.Parse(text, DirectiveName)?.TypeName;
}
