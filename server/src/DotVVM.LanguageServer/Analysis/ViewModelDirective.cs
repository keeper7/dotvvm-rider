namespace DotVVM.LanguageServer.Analysis;

/// <summary>A view model reference from the @viewModel directive, with the type name's position.</summary>
public record ViewModelReference(
    string TypeName, string? AssemblyName, int Line, int Character, int Length);

/// <summary>
/// Parses the @viewModel directive. This is the only binding source of truth about which view
/// model a view belongs to; the file naming convention is mere habit.
/// </summary>
public static class ViewModelDirective
{
    private const string DirectiveName = "@viewModel";

    public static ViewModelReference? Parse(string text)
    {
        var lines = text.Split('\n');

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var raw = lines[lineIndex].TrimEnd('\r');
            var trimmedStart = raw.Length - raw.TrimStart().Length;
            var lineText = raw.TrimStart();

            if (!lineText.StartsWith(DirectiveName, StringComparison.OrdinalIgnoreCase))
            {
                // Directives sit at the start of the file; after the first tag there is no point looking
                if (lineText.StartsWith('<')) return null;
                continue;
            }

            var rest = lineText[DirectiveName.Length..];
            var valueOffset = rest.Length - rest.TrimStart().Length;
            var value = rest.TrimStart();
            if (value.Length == 0) return null;

            var commaIndex = FindAssemblySeparator(value);
            var typeName = (commaIndex < 0 ? value : value[..commaIndex]).TrimEnd();
            var assembly = commaIndex < 0 ? null : value[(commaIndex + 1)..].Trim();

            return new ViewModelReference(
                TypeName: typeName,
                AssemblyName: string.IsNullOrEmpty(assembly) ? null : assembly,
                Line: lineIndex,
                Character: trimmedStart + DirectiveName.Length + valueOffset,
                Length: typeName.Length);
        }

        return null;
    }

    /// <summary>
    /// Finds the comma separating the assembly name. Commas inside generic arguments
    /// (List&lt;A, B&gt;, for example) are skipped.
    /// </summary>
    private static int FindAssemblySeparator(string value)
    {
        var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            switch (value[i])
            {
                case '<': depth++; break;
                case '>': depth--; break;
                case ',' when depth == 0: return i;
            }
        }
        return -1;
    }
}
