using DotVVM.Framework.Hosting;
using DotVVM.Framework.Security;

namespace DotVVM.LanguageServer.Compiler;

/// <summary>
/// Stands in for the real protector, which the hosting layer registers and
/// DotvvmConfiguration.CreateDefault does not.
///
/// Without it every `staticCommand` in the project fails to compile: the binding needs
/// StaticCommandMethodTranslator, which takes IViewModelProtector in its constructor. Measured on
/// a real project of 244 views: 13 files reported 44 diagnostics, and all of them came from this
/// one missing service. Nothing here is ever asked to protect anything - compilation only needs
/// the service to exist.
/// </summary>
public sealed class ViewModelProtectorStub : IViewModelProtector
{
    public string Protect(string serializedData, IDotvvmRequestContext context) => serializedData;

    public byte[] Protect(byte[] plaintextData, params string[] purposes) => plaintextData;

    public string Unprotect(string protectedData, IDotvvmRequestContext context) => protectedData;

    public byte[] Unprotect(byte[] protectedData, params string[] purposes) => protectedData;
}
