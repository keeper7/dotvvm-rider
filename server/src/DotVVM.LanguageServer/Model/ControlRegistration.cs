namespace DotVVM.LanguageServer.Model;

/// <summary>
/// A single entry from config.markup.controls: either a whole namespace registration
/// (Namespace + Assembly) or one markup control (TagName + Src).
/// </summary>
public record ControlRegistration(
    string TagPrefix,
    string? Namespace,
    string? Assembly,
    string? TagName,
    string? Src,
    /// <summary>
    /// The code-behind class of a markup control, read from @baseType in the file Src names.
    /// Filled in by MarkupControlResolver once all the tiers are merged; null until then, and
    /// null for a control whose file declares no base type.
    /// </summary>
    string? BaseTypeName = null)
{
    public bool IsMarkupControl => TagName is not null && Src is not null;
}
