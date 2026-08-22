namespace DotVVM.LanguageServer.Analysis;

/// <summary>A .NET type named by a directive, with the type name's position in the file.</summary>
public record TypeReference(
    string TypeName, string? AssemblyName, int Line, int Character, int Length);

/// <summary>
/// Parses the directives that name a .NET type - @viewModel, @baseType - which all share one
/// grammar: the directive, a type name, and an optional assembly name after a comma.
/// </summary>
public static class TypeDirective
{
    /// <summary>
    /// What may precede the directive. The byte order mark belongs here because every real file
    /// starts with one and U+FEFF is *not* whitespace to .NET, so TrimStart() leaves it in place
    /// and the directive is never recognised.
    /// </summary>
    private static readonly char[] Leading = [' ', '\t', '﻿'];

    public static TypeReference? Parse(string text, string directiveName)
    {
        var lines = text.Split('\n');

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var raw = lines[lineIndex].TrimEnd('\r');
            var lineText = raw.TrimStart(Leading);
            var trimmedStart = raw.Length - lineText.Length;

            if (!lineText.StartsWith(directiveName, StringComparison.OrdinalIgnoreCase))
            {
                // Directives sit at the start of the file; after the first tag there is no point looking
                if (lineText.StartsWith('<')) return null;
                continue;
            }

            var rest = lineText[directiveName.Length..];
            var valueOffset = rest.Length - rest.TrimStart().Length;
            var value = rest.TrimStart();
            if (value.Length == 0) return null;

            var commaIndex = FindAssemblySeparator(value);
            var typeName = (commaIndex < 0 ? value : value[..commaIndex]).TrimEnd();
            var assembly = commaIndex < 0 ? null : value[(commaIndex + 1)..].Trim();

            return new TypeReference(
                TypeName: typeName,
                AssemblyName: string.IsNullOrEmpty(assembly) ? null : assembly,
                Line: lineIndex,
                Character: trimmedStart + directiveName.Length + valueOffset,
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
