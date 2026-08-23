using System.Text;
using DotVVM.LanguageServer.Model;

namespace DotVVM.LanguageServer.Analysis;

/// <summary>
/// Builds the markdown shown when hovering over a tag. Free of LSP dependencies, so what the
/// server is willing to claim about a control can be tested without speaking the protocol.
/// </summary>
public static class ControlHoverText
{
    /// <summary>How many properties to list before the popup stops being readable.</summary>
    private const int MaxProperties = 15;

    public static string Build(
        ControlRegistry registry, bool knowsProjectPrefixes, string prefix, string tagName)
    {
        var text = new StringBuilder($"**{prefix}:{tagName}**");

        var control = registry.GetControl(prefix, tagName);
        if (control is null)
        {
            var note = DescribeUnknown(registry, knowsProjectPrefixes, prefix, tagName);
            if (note is not null) text.Append("\n\n").Append(note);
            return text.ToString();
        }

        text.Append("\n\n").Append('`').Append(control.FullTypeName).Append('`');

        if (control.DefaultContentProperty is not null)
        {
            text.Append("\n\nDefault content property: `")
                .Append(control.DefaultContentProperty).Append('`');
        }

        if (control.Properties.Count > 0)
        {
            text.Append("\n\nProperties: ").Append(string.Join(", ",
                control.Properties.Take(MaxProperties).Select(p => $"`{p.Name}`")));

            if (control.Properties.Count > MaxProperties)
            {
                text.Append(" and ").Append(control.Properties.Count - MaxProperties).Append(" more");
            }
        }

        // The families are listed apart from the properties: what follows the prefix is the
        // author's own word, so Class- alongside Text would read as a property named "Class-"
        if (control.Groups.Count > 0)
        {
            text.Append("\n\nProperty groups: ").Append(string.Join(", ",
                control.Groups.Select(g => $"`{g.Prefix}`")));
        }

        return text.ToString();
    }

    /// <summary>
    /// What to say when the control's type is not known. Saying nothing is a valid answer: with
    /// nothing loaded about the project, or with a prefix no source could have seen, any claim
    /// would be a guess - the same reason TagValidator stays quiet in those cases.
    /// </summary>
    private static string? DescribeUnknown(
        ControlRegistry registry, bool knowsProjectPrefixes, string prefix, string tagName)
    {
        if (registry.AllPrefixes.Count == 0) return null;

        // Only a markup control may be called one. It has no type of its own, so its properties
        // are reachable only through the class its @baseType names.
        if (registry.IsMarkupControl(prefix, tagName))
        {
            return "_Markup control — its properties are not known: "
                 + "the class named by @baseType was not found._";
        }

        if (!registry.IsKnownPrefix(prefix))
        {
            // The same distinction TagValidator draws, and with the same wording, so the tooltip
            // never says something other than the squiggle under the very same tag.
            return knowsProjectPrefixes
                ? $"_Unknown control prefix '{prefix}'. Register it in DotvvmStartup._"
                : null;
        }

        return "_Not found among the registered controls._";
    }
}
