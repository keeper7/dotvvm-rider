using System.Reflection;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Compilation.Javascript;
using DotVVM.Framework.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotVVM.LanguageServer.Compiler;

/// <summary>
/// Which methods a binding may call. Measured against the framework on a view model with one
/// method of its own and on `string.Substring`, which nothing translates:
///
/// <code>
/// {value: Name.Substring(1)}          rejected      {command: Name.Substring(1)}        compiles
/// {resource: Name.Substring(1)}       compiles      {staticCommand: Name.Substring(1)}  rejected
/// {command: Save()}                   compiles      {staticCommand: Save()}             rejected
/// </code>
///
/// So a resource and a command are evaluated on the server and take any method at all, while a
/// value has to reach the browser. A static command reaches it too, and takes a method of the
/// project only where [AllowStaticCommand] says so - which the framework reports, misleadingly,
/// as a method that cannot be translated.
/// </summary>
internal enum MethodRule
{
    /// <summary>Only what DotVVM can turn into JavaScript.</summary>
    JavaScript,

    /// <summary>Anything: the expression is evaluated on the server.</summary>
    Server,

    /// <summary>What can be translated, plus the methods marked [AllowStaticCommand].</summary>
    StaticCommand,
}

internal static class MethodRules
{
    public static MethodRule For(string binding) => binding switch
    {
        "resource" or "command" or "controlCommand" => MethodRule.Server,
        "staticCommand" => MethodRule.StaticCommand,
        _ => MethodRule.JavaScript,
    };
}

/// <summary>
/// The members that may be written on a type. Properties are offered as they are; methods only
/// where DotVVM can translate them to JavaScript, since a binding is compiled, not interpreted -
/// offering `Substring` would offer something the compiler then rejects with
/// "Method string.Substring(int, int) cannot be translated to Javascript".
///
/// The list of what can be translated is the framework's own registry rather than a copy of its
/// documentation: it is always current, and it includes the translators a project registers for
/// itself in DotvvmStartup.
/// </summary>
internal sealed class MemberOffer
{
    /// <summary>
    /// Which methods may be called, by the type declaring them and their name. A generic type
    /// is reduced to its definition on both sides, or `List&lt;Customer&gt;.Contains` would
    /// never match the `List&lt;&gt;.Contains` the registry holds.
    /// </summary>
    private readonly HashSet<(Type Declaring, string Name)> _translatable;

    public MemberOffer(DotvvmConfiguration configuration)
    {
        var collection =
            configuration.ServiceProvider.GetService<IJavascriptMethodTranslator>()
                as JavascriptTranslatableMethodCollection
            ?? JavascriptTranslatableMethodCollection.CreateDefault();

        _translatable = collection.MethodTranslators.Keys
            .Where(m => m.DeclaringType is not null)
            .Select(m => (Definition(m.DeclaringType!), m.Name))
            .ToHashSet();
    }

    /// <summary>
    /// What may be written where an expression begins: the data context's own members, and the
    /// words the binding parser understands on top of them.
    /// </summary>
    public IReadOnlyList<CompletionItemData> ForContext(
        DataContextStack stack, IReadOnlyList<NamespaceImport> configured, MethodRule rule)
    {
        var items = new List<CompletionItemData>();
        if (stack.DataContextType is not null)
        {
            items.AddRange(For(new Target(stack.DataContextType, false), rule));
        }

        // The classes an import brings in. Measured, 2775 of a real project's 8303 member
        // accesses begin with one - `{resource: Fields.Title}` - so leaving them out would
        // offer nothing at the very place a resource binding starts.
        foreach (var type in ExpressionTypes.Imported(stack, configured))
        {
            items.Add(new CompletionItemData(
                type.Name, "class", type.Namespace, type.Name));
        }

        items.Add(Parameter("_this", stack.DataContextType));
        items.Add(Parameter("_root", ExpressionTypes.Root(stack).DataContextType));
        if (stack.Parent is not null) items.Add(Parameter("_parent", stack.Parent.DataContextType));

        // _parent0 and _parent1 are only other spellings of _this and _parent - measured - so
        // the numbered form is offered from the first level it can reach that they cannot
        for (var levels = 2; ExpressionTypes.Ancestor(stack, levels) is { } ancestor; levels++)
        {
            items.Add(Parameter($"_parent{levels}", ancestor.DataContextType));
        }

        foreach (var parameter in ExpressionTypes.Parameters(stack))
        {
            items.Add(Parameter(parameter.Identifier,
                                ResolvedTypeDescriptor.ToSystemType(parameter.ParameterType)));
        }

        return items;
    }

    /// <summary>
    /// The members of what the path named. A type named rather than a value - a resource class
    /// brought in by an @import - offers its static members instead, which is where the labels
    /// of a real project live.
    /// </summary>
    public IReadOnlyList<CompletionItemData> For(Target target, MethodRule rule)
    {
        var flags = BindingFlags.Public |
                    (target.Static ? BindingFlags.Static : BindingFlags.Instance);

        var items = new List<CompletionItemData>();

        foreach (var property in target.Type.GetProperties(flags)
                     .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && !IsPlumbing(p))
                     .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            items.Add(new CompletionItemData(
                property.Name, "property", Describe(property.PropertyType), property.Name));
        }

        foreach (var field in target.Type.GetFields(flags)
                     .Where(f => !IsPlumbing(f))
                     .OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            items.Add(new CompletionItemData(field.Name, "property", Describe(field.FieldType), field.Name));
        }

        foreach (var method in Methods(target, rule, flags)
                     .DistinctBy(m => m.Name)
                     .OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            items.Add(Method(method));
        }

        return items;
    }

    /// <summary>
    /// The methods of the type the binding may call, and over a collection the LINQ operators
    /// too: they read like instance methods and are how half of what a view asks of a collection
    /// is written. The operators stay judged by the registry whatever the rule - the full set is
    /// some two hundred names, and it would bury the methods the author is actually looking for.
    /// </summary>
    private IEnumerable<MethodInfo> Methods(Target target, MethodRule rule, BindingFlags flags)
    {
        var declared = target.Type.GetMethods(flags)
            .Where(m => !m.IsSpecialName && Allows(rule, m));

        if (target.Static || ExpressionTypes.ElementTypeOf(target.Type) is null) return declared;

        return declared.Concat(
            typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(IsTranslatable));
    }

    private bool Allows(MethodRule rule, MethodInfo method) => rule switch
    {
        // Everything but what a view model inherits and nobody calls - Object's own, and the
        // Init/Load/PreRender of DotvvmViewModelBase. ToString survives that: the registry
        // holds it, so a value binding may write it too.
        MethodRule.Server => IsTranslatable(method) || !IsPlumbing(method),
        MethodRule.StaticCommand => IsTranslatable(method) || AllowsStaticCommand(method),
        _ => IsTranslatable(method),
    };

    private bool IsTranslatable(MethodInfo method) =>
        method.DeclaringType is not null &&
        _translatable.Contains((Definition(method.DeclaringType), method.Name));

    /// <summary>
    /// Whether a static command may call it. Read by the attribute's name rather than by the
    /// type: the DotVVM loaded here is the target project's, and its version is not ours to
    /// assume.
    /// </summary>
    private static bool AllowsStaticCommand(MethodInfo method) =>
        method.CustomAttributes.Any(a => a.AttributeType.Name == "AllowStaticCommandAttribute");

    /// <summary>
    /// What a view model inherits from the framework and nobody writes in a binding - Context
    /// among the properties, Init, Load and PreRender among the methods. Judged by the declaring
    /// type's name rather than by the type itself: the DotVVM loaded here is the target
    /// project's, and its version is not ours to assume.
    /// </summary>
    private static bool IsPlumbing(MemberInfo member) =>
        member.DeclaringType?.FullName is "System.Object" or "DotVVM.Framework.ViewModel.DotvvmViewModelBase";

    private static CompletionItemData Method(MethodInfo method)
    {
        var parameters = method.GetParameters();

        // An extension method's first parameter is the receiver, which the author does not write
        if (method.DeclaringType == typeof(Enumerable) && parameters.Length > 0)
        {
            parameters = parameters[1..];
        }

        var signature =
            $"{Describe(method.ReturnType)} {method.Name}" +
            $"({string.Join(", ", parameters.Select(p => Describe(p.ParameterType)))})";

        return new CompletionItemData(
            method.Name, "method", signature,
            parameters.Length == 0 ? method.Name + "()" : method.Name + "($0)",
            Snippet: parameters.Length > 0);
    }

    private static CompletionItemData Parameter(string name, Type? type) =>
        new(name, "parameter", type is null ? null : Describe(type), name);

    private static Type Definition(Type type) =>
        type.IsGenericType && !type.IsGenericTypeDefinition ? type.GetGenericTypeDefinition() : type;

    private static readonly Dictionary<Type, string> Aliases = new()
    {
        [typeof(string)] = "string", [typeof(int)] = "int", [typeof(long)] = "long",
        [typeof(bool)] = "bool", [typeof(double)] = "double", [typeof(decimal)] = "decimal",
        [typeof(object)] = "object", [typeof(char)] = "char", [typeof(byte)] = "byte",
        [typeof(short)] = "short", [typeof(float)] = "float", [typeof(void)] = "void",
    };

    /// <summary>A type as a reader of the view would write it, not as the runtime names it.</summary>
    public static string Describe(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } inner) return Describe(inner) + "?";
        if (Aliases.TryGetValue(type, out var alias)) return alias;
        if (type.IsArray) return Describe(type.GetElementType()!) + "[]";

        if (!type.IsGenericType) return type.Name;

        var name = type.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0) name = name[..tick];

        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(Describe))}>";
    }
}
