using DotVVM.Framework.Compilation;
using DotVVM.Framework.Configuration;

namespace DotVVM.LanguageServer.Compiler;

/// <summary>
/// What may be written where the caret stands inside a binding. Three questions, each answered
/// by a class of its own: where the data context comes from, what the written path evaluates to,
/// and which members of that may be offered.
///
/// The server has already decided that the caret *is* inside a binding and what stands to its
/// left - that much is text, and text is testable without a project. Everything here needs the
/// project's own types, which is why it runs in this process and nowhere else.
/// </summary>
internal sealed class MemberCompletion
{
    private readonly DataContextLookup _contexts;
    private readonly MemberOffer _members;

    /// <summary>
    /// What DotvvmStartup put in scope for every view. A file's own @import is only half of the
    /// answer, and in a real project the smaller half.
    /// </summary>
    private readonly IReadOnlyList<NamespaceImport> _imports;

    public MemberCompletion(DotvvmConfiguration configuration)
    {
        _contexts = new DataContextLookup(configuration);
        _members = new MemberOffer(configuration);
        _imports = configuration.Markup.ImportedNamespaces.ToList();

        // Reading the classes of those namespaces means walking every loaded assembly. Doing it
        // now, while nobody is waiting, is the difference between a first popup that answers and
        // one that arrives after the user has given up on it.
        var spaces = _imports.Select(i => i.Namespace).ToList();
        Task.Run(() =>
        {
            try { ExpressionTypes.Prime(spaces); }
            catch (Exception ex) { Console.Error.WriteLine($"priming failed: {ex.Message}"); }
        });
    }

    public IReadOnlyList<CompletionItemData> Complete(
        string fileName, string text, int offset, string expression, string binding)
    {
        var stack = _contexts.Find(fileName, text, offset);
        if (stack is null) return Array.Empty<CompletionItemData>();

        var rule = MethodRules.For(binding);
        if (expression.Length == 0) return _members.ForContext(stack, _imports, rule);

        var target = ExpressionTypes.Resolve(stack, expression, _imports);
        return target is null
            ? Array.Empty<CompletionItemData>()
            : _members.For(target.Value, rule);
    }
}
