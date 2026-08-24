using System.Reflection;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Compilation.Binding;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.ControlTree.Resolved;

namespace DotVVM.LanguageServer.Compiler;

/// <summary>What a path names: a value of that type, or the type itself.</summary>
internal readonly record struct Target(Type Type, bool Static);

/// <summary>
/// What a written expression evaluates to. Completion needs it for the chaining: the members
/// offered after `Customer.Address.` are those of whatever `Customer.Address` turns out to be,
/// which is only known by walking the path one member at a time.
/// </summary>
internal static class ExpressionTypes
{
    /// <summary>
    /// What the path names: a value of some type, or a type itself. Null when it names
    /// something that is not there - a misspelt property, a class nobody imported. Nothing is
    /// offered then, which is the honest answer: the expression as written has no members.
    /// </summary>
    public static Target? Resolve(
        DataContextStack stack, string path, IReadOnlyList<NamespaceImport> configured)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return stack.DataContextType is null ? null : new Target(stack.DataContextType, false);
        }

        var segments = Segments(path).Select(Read).ToList();
        if (segments.Any(s => s.Name.Length == 0)) return null;

        Target current;
        int index;

        // A value first, a type second: a property named like an imported class is the one the
        // author meant, and the framework resolves it the same way round.
        var value = Start(stack, segments[0].Name, segments[0].Call);
        if (value is not null)
        {
            current = new Target(value, false);
            index = 1;
        }
        else
        {
            var type = NamedType(stack, configured, segments, out index);
            if (type is null) return null;
            current = new Target(type, true);
        }

        for (var i = 0; i < segments[index - 1].Indices; i++)
        {
            var element = ElementTypeOf(current.Type);
            if (element is null) return null;
            current = new Target(element, false);
        }

        for (; index < segments.Count; index++)
        {
            var stepped = Step(current, segments[index].Name, segments[index].Call);
            if (stepped is null) return null;
            current = new Target(stepped, false);

            for (var i = 0; i < segments[index].Indices; i++)
            {
                var element = ElementTypeOf(current.Type);
                if (element is null) return null;
                current = new Target(element, false);
            }
        }

        return current;
    }

    /// <summary>
    /// The first name of a path may be one of the binding's own words rather than a member of
    /// the data context. `_this`, `_root` and `_parent` are the ones the parser understands
    /// everywhere; the rest - `_index`, `_collection`, `_control` and anything a project adds -
    /// stand on the context stack itself.
    /// </summary>
    private static Type? Start(DataContextStack stack, string name, bool call)
    {
        if (call) return Step(new Target(stack.DataContextType!, false), name, true);

        switch (name)
        {
            case "_this": return stack.DataContextType;
            case "_root": return Root(stack).DataContextType;
            case "_parent": return stack.Parent?.DataContextType;
        }

        // Measured against the framework: _parent0 is the current context and _parent1 the one
        // above it, so the number counts levels rather than parents
        if (name.StartsWith("_parent", StringComparison.Ordinal) &&
            int.TryParse(name[7..], out var levels) && levels >= 0)
        {
            return Ancestor(stack, levels)?.DataContextType;
        }

        var parameter = Parameters(stack).FirstOrDefault(p => p.Identifier == name);
        if (parameter is not null) return ResolvedTypeDescriptor.ToSystemType(parameter.ParameterType);

        return stack.DataContextType is null
            ? null
            : Step(new Target(stack.DataContextType, false), name, false);
    }

    /// <summary>
    /// A path may begin with a class rather than with a value - `{resource: Fields.Title}`,
    /// where Fields is a resource class brought in by an @import. Measured on a real project:
    /// of 8303 places where a member follows a dot, 3949 begin that way, nearly all of them
    /// resource classes, so a completion that did not know them would be half a feature.
    ///
    /// The name may be written in full, so the leading segments are joined one at a time until
    /// something resolves.
    /// </summary>
    private static Type? NamedType(
        DataContextStack stack, IReadOnlyList<NamespaceImport> configured,
        List<(string Name, bool Call, int Indices)> segments, out int consumed)
    {
        var written = "";

        for (consumed = 1; consumed <= segments.Count; consumed++)
        {
            var segment = segments[consumed - 1];

            // A namespace is written as plain names; anything called or indexed ends the search
            if (segment.Call || segment.Indices > 0) break;

            written = written.Length == 0 ? segment.Name : written + "." + segment.Name;

            // What the binding parser itself would make of the name: the C# aliases and the
            // namespaces every view has, `string.Format` and `DateOnly.FromDateTime` among them
            if (Aliased(written) is { } aliased) return aliased;

            foreach (var import in Imports(stack).Concat(configured))
            {
                if (import.HasAlias && import.Alias == written && Loaded(import.Namespace) is { } named)
                {
                    return named;
                }

                if (Loaded(import.Namespace + "." + written) is { } imported) return imported;
            }

            // The namespace of the type in scope goes with it: measured, `Item.Nonexistent`
            // fails on the member and not on the type, so the view model's own namespace is
            // one a binding may leave out
            foreach (var context in Contexts(stack))
            {
                if (context.Namespace is { } space && Loaded(space + "." + written) is { } nearby)
                {
                    return nearby;
                }
            }

            if (Loaded(written) is { } whole) return whole;
        }

        consumed = 0;
        return null;
    }

    /// <summary>
    /// The classes a view may name without qualifying them: what its own @import brings in and
    /// what DotvvmStartup registered. The framework's default namespaces are deliberately left
    /// out - offering every type of System would bury the handful a view actually uses.
    ///
    /// Remembered per namespace, because the answer costs a walk over the types of every
    /// assembly the application references and completion asks on every keystroke.
    /// </summary>
    public static IEnumerable<Type> Imported(
        DataContextStack stack, IReadOnlyList<NamespaceImport> configured)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var import in Imports(stack).Concat(configured))
        {
            if (import.Namespace is not { Length: > 0 } space || !seen.Add(space)) continue;

            foreach (var type in TypesIn(space)) yield return type;
        }
    }

    /// <summary>
    /// Whether a class is worth offering by its bare name.
    ///
    /// **Neither visibility nor [CompilerGenerated] is a criterion.** A .resx generates its
    /// strongly typed class as `internal` and marks it compiler generated, and those classes -
    /// `Fields`, `Buttons` - are what a real project names most often at the start of a resource
    /// binding. The same trap as filtering view models by IsPublic, which dropped 60 of 177.
    ///
    /// The framework's own internals are another matter: `ThrowHelper` and
    /// `SystemCore_EnumerableDebugView` sit in System.Linq, which every view imports, and
    /// nobody writes them. So from a System or Microsoft assembly only what is public is
    /// offered, and from the application's own everything.
    /// </summary>
    private static bool Offerable(Type type) =>
        !type.IsInterface &&
        !type.IsNested &&
        !type.IsGenericTypeDefinition &&
        !type.Name.StartsWith('<') &&
        !typeof(Delegate).IsAssignableFrom(type) &&
        (type.IsPublic || !IsFramework(type.Assembly));

    private static bool IsFramework(Assembly assembly)
    {
        var name = assembly.GetName().Name ?? "";
        return name is "mscorlib" or "netstandard" or "System" ||
               name.StartsWith("System.", StringComparison.Ordinal) ||
               name.StartsWith("Microsoft.", StringComparison.Ordinal);
    }

    private static readonly Dictionary<string, List<Type>> Namespaces = new(StringComparer.Ordinal);

    /// <summary>
    /// Reads the classes of every namespace named, in **one** walk over the assemblies. Measured
    /// on a real project: 372 assemblies are loaded, and doing that walk once per namespace
    /// would cost the first popup several times over. Priming it in the background at start-up
    /// keeps even that one walk off the request.
    /// </summary>
    public static void Prime(IEnumerable<string> spaces)
    {
        lock (Namespaces)
        {
            var wanted = spaces
                .Where(s => s.Length > 0 && !Namespaces.ContainsKey(s))
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(s => s, _ => new List<Type>(), StringComparer.Ordinal);

            if (wanted.Count == 0) return;

            foreach (var assembly in Assemblies())
            {
                foreach (var type in TypesOf(assembly))
                {
                    if (type.Namespace is { } space &&
                        wanted.TryGetValue(space, out var list) &&
                        Offerable(type))
                    {
                        list.Add(type);
                    }
                }
            }

            foreach (var (space, list) in wanted)
            {
                Namespaces[space] = list
                    .GroupBy(t => t.Name, StringComparer.Ordinal)
                    .Select(g => g.First())
                    .OrderBy(t => t.Name, StringComparer.Ordinal)
                    .ToList();
            }
        }
    }

    private static List<Type> TypesIn(string space)
    {
        Prime(new[] { space });
        lock (Namespaces) return Namespaces.GetValueOrDefault(space) ?? new List<Type>();
    }

    /// <summary>
    /// Everything loaded. **Not GetReferencedAssemblies** - measured on a real project, that is
    /// 18 assemblies against 372, and the one holding the resource classes is not among them.
    /// </summary>
    private static IEnumerable<Assembly> Assemblies()
    {
        var all = new HashSet<Assembly>();

        try { all.UnionWith(CompiledAssemblyCache.Instance?.GetAllAssemblies() ?? Array.Empty<Assembly>()); }
        catch (Exception) { /* the framework's own view of them may fail; the domain still answers */ }

        all.UnionWith(AppDomain.CurrentDomain.GetAssemblies());
        return all;
    }

    /// <summary>One unloadable type must not cost the whole assembly.</summary>
    private static IEnumerable<Type> TypesOf(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<Type>(); }
        catch (Exception) { return Array.Empty<Type>(); }
    }

    /// <summary>The types the caret's context and everything above it are written against.</summary>
    private static IEnumerable<Type> Contexts(DataContextStack stack)
    {
        for (var current = stack; current is not null; current = current.Parent)
        {
            if (current.DataContextType is not null) yield return current.DataContextType;
        }
    }

    /// <summary>
    /// The namespaces a view has in scope. A file's own @import is only half of it: a project
    /// registers namespaces in DotvvmStartup, and a real one leans on that - measured, 2775 of
    /// its 8303 member accesses begin with a resource class that no file imports by name.
    /// </summary>
    private static IEnumerable<NamespaceImport> Imports(DataContextStack stack)
    {
        for (var current = stack; current is not null; current = current.Parent)
        {
            foreach (var import in current.NamespaceImports) yield return import;
        }
    }

    /// <summary>
    /// The framework's own registry of what a bare name means, asked rather than copied: it
    /// holds the C# aliases and the namespaces a binding may use without importing them, and it
    /// answers for the version of DotVVM the project is on. A namespace resolves to an
    /// "unknown" identifier, which is not an answer - the caller then tries a longer name.
    /// </summary>
    private static readonly Lazy<TypeRegistry?> Names = new(() =>
    {
        try
        {
            var cache = CompiledAssemblyCache.Instance;
            return cache is null ? null : TypeRegistry.Default(cache);
        }
        catch (Exception) { return null; }
    });

    private static Type? Aliased(string name)
    {
        try
        {
            var resolved = Names.Value?.Resolve(name, throwOnNotFound: false);
            return resolved is null || resolved.GetType().Name.StartsWith("Unknown", StringComparison.Ordinal)
                ? null
                : resolved.Type;
        }
        catch (Exception) { return null; }
    }

    /// <summary>
    /// A type by its full name, from whatever is loaded in this process - which is the target
    /// application and everything it references, the process having been started with its own
    /// deps.json. Remembered because a name that resolves to nothing costs a walk over some
    /// three hundred assemblies, and completion asks again on every keystroke.
    /// </summary>
    private static readonly Dictionary<string, Type?> Known = new(StringComparer.Ordinal);

    private static Type? Loaded(string fullName)
    {
        lock (Known)
        {
            if (Known.TryGetValue(fullName, out var remembered)) return remembered;

            // The framework's own lookup, which reaches the assemblies the application
            // references rather than only those already loaded - a resource class lives in one
            // of those and nothing has needed it yet
            Type? found = null;
            try { found = CompiledAssemblyCache.Instance?.FindType(fullName, false); }
            catch (Exception) { /* a name that resolves to nothing is the ordinary case */ }

            Known[fullName] = found;
            return found;
        }
    }

    private static Type? Step(Target current, string name, bool call)
    {
        var flags = BindingFlags.Public | (current.Static ? BindingFlags.Static : BindingFlags.Instance);
        var type = current.Type;

        if (!call)
        {
            var property = type.GetProperty(name, flags);
            if (property is not null) return property.PropertyType;

            var field = type.GetField(name, flags);
            if (field is not null) return field.FieldType;

            // A nested class is written like a member and is one more thing a path may name
            return current.Static ? type.GetNestedType(name, BindingFlags.Public) : null;
        }

        var method = type.GetMethods(flags)
            .FirstOrDefault(m => m.Name == name && !m.IsSpecialName);
        if (method is not null) return Returned(method, type);

        if (current.Static) return null;

        // An extension method reads like an instance one, and over a collection that is how
        // half of what a binding may call is written - `Items.Count()`, `Items.FirstOrDefault()`
        var element = ElementTypeOf(type);
        if (element is null) return null;

        var extension = typeof(Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == name);

        return extension is null ? null : Returned(extension, type);
    }

    /// <summary>
    /// What a call gives back. A generic method over a collection is worth the substitution -
    /// `FirstOrDefault()` returns the item, not an unbound TSource - and anything more involved
    /// than one type argument is left alone rather than guessed at.
    /// </summary>
    private static Type? Returned(MethodInfo method, Type receiver)
    {
        if (!method.IsGenericMethodDefinition) return method.ReturnType;

        var element = ElementTypeOf(receiver);
        if (element is null || method.GetGenericArguments().Length != 1) return null;

        try { return method.MakeGenericMethod(element).ReturnType; }
        catch (ArgumentException) { return null; }
    }

    /// <summary>What a collection holds, or null when the type is not one.</summary>
    public static Type? ElementTypeOf(Type type)
    {
        if (type.IsArray) return type.GetElementType();

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return type.GetGenericArguments()[0];
        }

        return type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                 i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
    }

    /// <summary>The context that many levels up, or null when the tree is not that deep.</summary>
    public static DataContextStack? Ancestor(DataContextStack stack, int levels)
    {
        DataContextStack? current = stack;
        for (var i = 0; i < levels && current is not null; i++) current = current.Parent;
        return current;
    }

    public static DataContextStack Root(DataContextStack stack)
    {
        while (stack.Parent is not null) stack = stack.Parent;
        return stack;
    }

    /// <summary>
    /// The extension parameters in scope, the ancestors' included: a parameter is written the
    /// same way however far up it was introduced.
    /// </summary>
    public static IEnumerable<BindingExtensionParameter> Parameters(DataContextStack stack)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var current = stack; current is not null; current = current.Parent)
        {
            foreach (var parameter in current.ExtensionParameters)
            {
                if (seen.Add(parameter.Identifier)) yield return parameter;
            }
        }
    }

    /// <summary>
    /// Splits the path on the dots that separate members. A dot inside a call's arguments or
    /// inside a string is not one of them.
    /// </summary>
    private static IEnumerable<string> Segments(string path)
    {
        var depth = 0;
        var quote = '\0';
        var start = 0;

        for (var i = 0; i < path.Length; i++)
        {
            var c = path[i];

            if (quote != '\0')
            {
                if (c == '\\') i++;
                else if (c == quote) quote = '\0';
                continue;
            }

            switch (c)
            {
                case '"' or '\'': quote = c; break;
                case '(' or '[': depth++; break;
                case ')' or ']': depth--; break;
                case '.' when depth == 0:
                    yield return path[start..i];
                    start = i + 1;
                    break;
            }
        }

        yield return path[start..];
    }

    /// <summary>
    /// Reads one segment: the name, whether it is called, and how many times it is indexed.
    /// `Items[0]` is a property indexed once, `Where(x =&gt; x.A)[0]` a call indexed once.
    /// </summary>
    private static (string Name, bool Call, int Indices) Read(string segment)
    {
        segment = segment.Trim();

        var i = 0;
        while (i < segment.Length && (char.IsLetterOrDigit(segment[i]) || segment[i] == '_')) i++;

        var name = segment[..i];
        var call = false;
        var indices = 0;

        while (i < segment.Length)
        {
            var c = segment[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            // Anything else after the name - an operator, a stray brace - means the segment is
            // not a member access at all, and half-reading one would resolve to the wrong type
            if (c is not ('(' or '[')) return ("", false, 0);

            var close = Closing(segment, i);
            if (close < 0) return ("", false, 0);

            if (c == '(') call = true; else indices++;
            i = close + 1;
        }

        return (name, call, indices);
    }

    /// <summary>The index of the bracket closing the one at start, or -1 when it is not closed.</summary>
    private static int Closing(string text, int start)
    {
        var depth = 0;
        var quote = '\0';

        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];

            if (quote != '\0')
            {
                if (c == '\\') i++;
                else if (c == quote) quote = '\0';
                continue;
            }

            switch (c)
            {
                case '"' or '\'': quote = c; break;
                case '(' or '[': depth++; break;
                case ')' or ']' when --depth == 0: return i;
            }
        }

        return -1;
    }
}
