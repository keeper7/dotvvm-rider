using System.Text.Json.Serialization;

namespace DotVVM.LanguageServer.Compiler;

/// <summary>
/// One request. The text travels with it whatever the kind: the buffer being edited is not on
/// disk. Kind tells the two apart and defaults to a compilation, which is what the process was
/// built for and still does most of the time.
/// </summary>
/// <param name="Offset">Where the caret is, for a completion. Ignored by a compilation.</param>
/// <param name="Expression">
/// The member access written to the left of the word being typed - `Customer.Address` in
/// `{value: Customer.Address.Ci|`. Empty where the data context's own members belong.
/// </param>
/// <param name="Binding">
/// Which kind of binding is being written. It decides which methods may be offered: measured
/// against the framework, `value` and `staticCommand` reject a method that cannot be translated
/// to JavaScript while `resource` and `command` run on the server and take any of them.
/// </param>
public record CompileRequest(
    int Id,
    string Path,
    string Text,
    string Kind = Kinds.Compile,
    int Offset = 0,
    string Expression = "",
    string Binding = "value");

public static class Kinds
{
    public const string Compile = "compile";
    public const string Complete = "complete";
}

/// <summary>
/// What the compiler found. Lines and columns are 1-based, the way DotVVM reports them; the
/// server converts to the protocol's 0-based positions.
/// </summary>
public record CompileDiagnostic(
    string Severity,
    string Message,
    int? StartLine,
    int? StartColumn,
    int? EndLine,
    int? EndColumn);

/// <summary>
/// One thing that may be written where the caret stands. Snippet marks an InsertText holding a
/// $0 placeholder; the server strips it for a client that cannot handle one.
/// </summary>
public record CompletionItemData(
    string Label,
    string Kind,
    string? Detail,
    string InsertText,
    bool Snippet = false);

public record CompileResponse(
    int Id,
    List<CompileDiagnostic> Diagnostics,
    List<CompletionItemData>? Items = null,
    string? Error = null);

[JsonSerializable(typeof(CompileRequest))]
[JsonSerializable(typeof(CompileResponse))]
internal partial class ProtocolContext : JsonSerializerContext;
