namespace DotVVM.LanguageServer.Compilation;

/// <summary>
/// One thing the compiler process says may be written where the caret stands. Named apart from
/// the protocol's own CompletionItem, which this is turned into by the handler.
/// </summary>
/// <param name="Kind">"property", "method" or "parameter" - what the handler sorts and paints by.</param>
/// <param name="Snippet">Whether InsertText holds a $0 placeholder.</param>
public record CompilerCompletionItem(
    string Label,
    string Kind,
    string? Detail,
    string InsertText,
    bool Snippet = false);
