namespace DotVVM.LanguageServer.Model;

/// <summary>
/// Jeden záznam z config.markup.controls. Buď registrace celého namespace
/// (Namespace + Assembly), nebo jedné markup kontrolky (TagName + Src).
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
