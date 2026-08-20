namespace DotVVM.LanguageServer.Model;

/// <summary>Kontrolka známá podle typu, včetně vlastností.</summary>
public record ControlInfo(
    string FullTypeName,
    string? BaseType,
    string? DefaultContentProperty,
    IReadOnlyList<string> Properties)
{
    /// <summary>Jméno tagu, tedy poslední segment plného jména typu.</summary>
    public string TagName => FullTypeName[(FullTypeName.LastIndexOf('.') + 1)..];

    /// <summary>Namespace typu, tedy vše před posledním segmentem.</summary>
    public string Namespace
    {
        get
        {
            var idx = FullTypeName.LastIndexOf('.');
            return idx < 0 ? string.Empty : FullTypeName[..idx];
        }
    }
}
