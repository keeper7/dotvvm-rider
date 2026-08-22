namespace DotVVM.LanguageServer.Analysis;

/// <summary>
/// Reads the @viewModel directive. This is the only binding source of truth about which view
/// model a view belongs to; the file naming convention is mere habit.
/// </summary>
public static class ViewModelDirective
{
    private const string DirectiveName = "@viewModel";

    public static TypeReference? Parse(string text) => TypeDirective.Parse(text, DirectiveName);
}
