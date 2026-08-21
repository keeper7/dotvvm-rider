# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DotVVM plugin for JetBrains Rider. Two parts, joined by plan 3: a native Kotlin plugin built
on the IntelliJ Platform's HTML infrastructure — **no ReSharper engine, no RD protocol** — and a
standalone C# LSP server under `server/` that the plugin bundles and launches as a subprocess.

`.dothtml`, `.dotmaster` and `.dotcontrol` are treated as an HTML superset: the language extends
`HTMLLanguage`, so HTML/CSS/JS support comes for free. Binding expressions (`{value: Name}`) are
located by a hand-written scanner — not a regex, which broke on nested braces and on `}` inside
string literals — and injected as a separate `DotVVMBinding` language with its own lexer and
highlighter.

The LSP server supplies the semantics the plugin cannot know: which control prefixes exist,
which tags are valid, and where the ViewModel lives. It fills its control registry from three
sources of increasing accuracy — built-in defaults, DotVVM's `dotvvm_serialized_config.json.tmp`,
and a probe process that loads the project's built assembly. A higher tier adds to the lower
ones rather than replacing them, so standard controls stay known even when the project has
never been built. With an empty registry the validator stays silent: without knowledge of the
project, underlining everything would be worse than saying nothing.

## Build Commands

All Gradle commands run from `plugin/`, which is a standalone Gradle project with its own wrapper:

```bash
cd plugin
./gradlew buildPlugin                    # Full build — also re-zips the bundled server
./gradlew test                           # All tests (57 after plan 4)
./gradlew test --tests "*ScannerTest*"   # Single test class
./gradlew runRider                       # Debug in a sandbox Rider — the target IDE
./gradlew runIde                         # Sandbox IDEA Ultimate (the compile platform)
```

`./gradlew build` does **not** rebuild the distribution zip. After changing the server, run
`buildPlugin`, or anything unpacking `build/distributions/` tests a stale server — a silent trap,
because the plugin still compiles and the old server still answers.

Run long builds in the background with output to a file, not piped into `tail` — a pipe returns the
pipe's exit code, masking a failed build. Beware the same trap in `cmd > log 2>&1; echo $?`, where
the reported code belongs to `echo`, not to the build.

## Architecture

- `plugin/src/main/kotlin/com/keeper7/dotvvm/lang/` — language, file types and parser definition
- `plugin/src/main/kotlin/com/keeper7/dotvvm/binding/` — binding scanner, lexer, injector, highlighter
- `plugin/src/main/kotlin/com/keeper7/dotvvm/lsp/` — server locator, LSP client, status bar widget
- `plugin/src/main/resources/META-INF/plugin.xml` — plugin descriptor
- `server/src/DotVVM.LanguageServer/` — LSP server: `Model/`, `Configuration/`, `Analysis/`, `Handlers/`
- `server/src/DotVVM.LanguageServer.Probe/` — loads the project assembly in its own process
- `fixtures/SampleApp/` — sample DotVVM app for manual and integration testing; it is a real
  buildable app, because the probe needs a built assembly and go-to-definition needs a `.csproj`

Registering `HTMLParserDefinition` directly for the DotVVM language is not enough — it builds the
PSI file with `HTMLLanguage` hardcoded, so `psiFile.language` never returns DotVVM. That is what
`DotvvmParserDefinition` overrides.

`BindingScanner` decides **where** bindings are and must stay free of any IntelliJ API, so it can be
tested with plain JUnit. `BindingInjector` decides **how** to hand them to the platform and is tested
through `BasePlatformTestCase`. Keep that split — it is why the hardest logic is testable without an IDE.

The server draws the same line: `Model/`, `Configuration/` and `Analysis/` know nothing about LSP,
and `Handlers/` hold no domain logic, only coordinate and type conversion. That boundary is why
most of the server is testable without speaking the protocol.

The probe runs as a separate process on purpose. It executes the user's own `DotvvmStartup`, which
can fail in any way at all — the isolation turns that into an exit code and a fallback to the
serialized config, not a dead language server.

## LSP integration

The plugin bundles the published server under `<plugin>/server/` and starts it with
`dotnet <dll>`; `ServerBinaryLocator` finds it and stays free of IntelliJ API so plain JUnit
can test it.

The platform renamed its LSP classes to match LSP terminology — **the IDE is the client**, the
external process is the server. Use `LspIntegrationProvider` and `ProjectWideLspClientDescriptor`
registered under `com.intellij.platform.lsp.integrationProvider`; the `*ServerSupportProvider*`
pair still exists but is the older spelling. Per-feature `lspXxxSupport` properties are gone,
replaced by a single `lspCustomization` holding `Lsp*Customizer` objects — formatting, on-type
formatting, folding and document symbols are switched off there, because the native HTML support
does them better than a server that sees only text.

LSP lives in `com.intellij.modules.lsp`, which Community lacks — hence the `<depends>` entry.
Both IU and Rider bundle it.

`dotvvm/configurationTier` is a custom notification, so nothing in the protocol carries it:
`DotvvmLsp4jClient` subclasses `Lsp4jClient` and picks it up with `@JsonNotification`. Without
that subclass the status bar would sit at its default forever.

Repainting that widget needs `EditorBasedStatusBarPopup.update()` on the instance fetched from
the status bar. `StatusBarWidgetsManager.updateWidget(factory)` looks like the right call and
is not — it only re-evaluates whether the widget is *available*, so the widget kept showing the
tier it computed before the server answered, and corrected itself only when an unrelated editor
event forced a repaint. That made it look intermittent rather than broken.

The validator reports an unknown *prefix* only when the registry came from a source that can
see the project's own prefixes (`IConfigurationSource.KnowsProjectPrefixes`). Built-in defaults
cannot, so on tier 1 a `<cc:MyControl>` stays silent while `<dot:NoSuchControl>` is still
flagged — standard controls are known even there.

## Directives

Directives (`@viewModel`, `@masterPage`, …) live at the top of the file and the HTML parser
sees them as plain text — so `<!DOCTYPE>` after them is `Unexpected tokens`. `DirectiveScanner`
finds them and, like `BindingScanner`, stays free of IntelliJ API.

**Do not give them their own node in the tree.** Emitting the block as a token — even a
well-formed XML comment the parser accepts — pushes `XML_PROLOG` out of first place, and the
platform then loses the HTML schema and reports `<html>` and `<div>` as unknown tags. That
trades one message per file for a squiggle under every tag. `DirectiveErrorFilter` instead
leaves the tree alone and hides just that one error; the `PsiErrorElement` stays in the PSI,
invisible to the user.

Highlighting and navigation therefore hang off text offsets, not off a directive node —
`DirectiveAnnotator` annotates the file element, and `MasterPageNavigationHandler` resolves
`@masterPage`, `@js` and `@viewModule` against the project's content roots. Directives naming
a .NET type (`@viewModel`, `@baseType`) stay with the server, which alone has the registry.

`HtmlUnknownTagInspection` cannot be tested here: the test IU has no HTML schema at all, so it
reports `<html>` as unknown even for untouched files. Anything about schema needs the sandbox.

## Testing

JUnit 4 runner, two styles side by side:
- Plain unit tests with `@Test` annotations (`BindingScannerTest`)
- Platform tests extending `BasePlatformTestCase`, using the JUnit 3 `fun testXxx()` convention

Anything touching `IElementType` or `TextAttributesKey` needs `BasePlatformTestCase` — those write to
a global platform registry and fail without an initialised `Application`.

## Probe traps

The probe fails on real projects unless all of these hold — each cost a debugging round:

- **`dotnet publish` refuses a multi-TFM project with `--output`** (NETSDK1129). The Gradle
  build therefore publishes each framework separately into `probe/<tfm>/`.
- **Its TFM must be at least as new as the target project's.** A net8.0 host refuses a net9.0
  assembly. It therefore builds for `net8.0;net9.0` into `probe/<tfm>/`, and
  `AssemblyProbeSource` picks the variant from `tfm` in the target's `runtimeconfig.json`.
- **It needs `FrameworkReference` on `Microsoft.AspNetCore.App`.** Web app types reference
  `Microsoft.AspNetCore.*`, which lives in the shared framework, not in NuGet packages, so
  `AssemblyDependencyResolver` cannot find it.
- **Never call `Assembly.GetTypes()` directly** — one unloadable type kills the whole scan.
  Read `ex.Types` from `ReflectionTypeLoadException` instead.
- **Register an empty `IConfiguration`.** User startups routinely resolve it from DI, and
  `DotvvmConfiguration.CreateDefault()` does not provide it.

The `properties` section of `dotvvm_serialized_config.json.tmp` is nested (`Type → Property`),
never the flat `"Type.Property"` key it looks like it might be.

## Versions and platform

Kotlin 2.4.10, IntelliJ Platform Gradle Plugin 2.18.1, Gradle 9.7.1, JVM target 21,
`since-build = 262`. Server: .NET 8, OmniSharp.Extensions.LanguageServer 0.19.9, xUnit 2.9.3.

**The compile/test platform is IntelliJ IDEA Ultimate 2026.2.1, not Rider** — `BasePlatformTestCase`
cannot run against Rider at all: Rider preloads services that need an open solution, so test
application startup dies with `solution can't be null`. This was verified with a control test
containing no plugin code. IU 2026.2.1 is build `262.9437.185` and Rider 2026.2.1 is `262.9437.287`,
so it is the same platform version minus the .NET backend, and the plugin uses no Rider API.

The cost is that `<depends>com.intellij.modules.rider</depends>` had to go — otherwise the plugin
would not load in IU. The plugin is therefore no longer formally Rider-only. Revisit this before
publishing to Marketplace.

The `runRider` sandbox lives in `.intellijPlatform/sandbox/dotvvm-rider/IU-*/…_runRider/` —
named after the compile platform (IU), not Rider, so `RD-*/` next to it is a stale leftover.

Two related gotchas: Rider does not publish its test-framework as an artifact (it needs
`TestFrameworkType.Bundled` — note the docs wrongly say `TestFrameworkType.Platform.Bundled`), and
Rider rejects installer distributions, so it needs `useInstaller.set(false)`.

## Planning Documents

Analysis, design and three implementation plans live in `.private/analyzy/` — outside git, start with
`ZACNI-TADY.md`.
