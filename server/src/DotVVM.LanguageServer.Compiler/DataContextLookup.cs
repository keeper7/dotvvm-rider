using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Compilation.Parser.Dothtml.Parser;
using DotVVM.Framework.Compilation.Parser.Dothtml.Tokenizer;
using DotVVM.Framework.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotVVM.LanguageServer.Compiler;

/// <summary>
/// What the data context is at one place in a file. The tree DotVVM's own resolver builds
/// carries it: every control and every binding in it knows the type its expressions are written
/// against, which is what changes inside an ItemTemplate and what completion has to follow.
///
/// It works on a file being edited as well. ResolveTree writes its complaints onto the nodes
/// rather than throwing, so an unfinished binding still yields a tree - measured on a view with
/// `&lt;span&gt;{{value: ` left open, 46 nodes and the context inside the template still right.
/// Nothing here needs the masking the plugin's lexer does.
/// </summary>
internal sealed class DataContextLookup
{
    private readonly IControlTreeResolver _resolver;

    public DataContextLookup(DotvvmConfiguration configuration) =>
        _resolver = configuration.ServiceProvider.GetRequiredService<IControlTreeResolver>();

    public DataContextStack? Find(string fileName, string text, int offset)
    {
        var tokenizer = new DothtmlTokenizer();
        tokenizer.Tokenize(text);

        var root = new DothtmlParser().Parse(tokenizer.Tokens);
        if (_resolver.ResolveTree(root, fileName) is not ResolvedTreeRoot tree) return null;

        var visitor = new Narrowest(offset);
        tree.Accept(visitor);
        return visitor.Found;
    }

    /// <summary>
    /// Keeps the smallest node that holds the offset. A binding's own node is narrower than the
    /// control's, and a control's is narrower than its parent's, so the innermost answer wins
    /// without any of them having to be told apart.
    /// </summary>
    private sealed class Narrowest(int offset) : ResolvedControlTreeVisitor
    {
        private int _width = int.MaxValue;

        public DataContextStack? Found { get; private set; }

        public override void VisitControl(ResolvedControl control)
        {
            Consider(control.DothtmlNode, control.DataContextTypeStack);
            base.VisitControl(control);
        }

        public override void VisitBinding(ResolvedBinding binding)
        {
            Consider(binding.DothtmlNode, binding.DataContextTypeStack);
            base.VisitBinding(binding);
        }

        private void Consider(DothtmlNode? node, DataContextStack? stack)
        {
            if (node is null || stack is null) return;

            // The end counts as inside: the caret at the end of an unfinished binding is exactly
            // where completion is asked for
            if (offset < node.StartPosition || offset > node.StartPosition + node.Length) return;
            if (node.Length > _width) return;

            _width = node.Length;
            Found = stack;
        }
    }
}
