namespace DotVVM.LanguageServer.Compilation;

/// <summary>What the view compiler said about one place in a file. Lines and columns are 1-based.</summary>
public record CompilerDiagnostic(
    string Severity,
    string Message,
    int? StartLine,
    int? StartColumn,
    int? EndLine,
    int? EndColumn);
