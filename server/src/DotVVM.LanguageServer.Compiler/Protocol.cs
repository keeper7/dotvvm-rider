using System.Text.Json.Serialization;

namespace DotVVM.LanguageServer.Compiler;

/// <summary>One file to compile. The text travels with it: the buffer being edited is not on disk.</summary>
public record CompileRequest(int Id, string Path, string Text);

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

public record CompileResponse(int Id, List<CompileDiagnostic> Diagnostics, string? Error = null);

[JsonSerializable(typeof(CompileRequest))]
[JsonSerializable(typeof(CompileResponse))]
internal partial class ProtocolContext : JsonSerializerContext;
