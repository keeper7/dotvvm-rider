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
    string? Src)
{
    public bool IsMarkupControl => TagName is not null && Src is not null;
}
