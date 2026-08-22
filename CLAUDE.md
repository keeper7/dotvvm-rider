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

**Everything in the repository is written in English** — comments, identifiers, user-facing
strings, commit messages, CHANGELOG and README. Conversation with the maintainer is in Czech;
the artefacts are not. The one exception is `.private/`, which holds Czech working notes and
stays outside git.

## Build Commands

All Gradle commands run from `plugin/`, which is a standalone Gradle project with its own wrapper:

```bash
cd plugin
./gradlew buildPlugin                    # Full build — also re-zips the bundled server
./gradlew test                           # All tests (81; the server has 134 of its own)
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
  buildable app, because the probe needs a built assembly and go-to-definition needs a `.csproj`.
  `SiteMaster.dotmaster` and `Address.dotcontrol` are written for this fixture, and their
  **structure** is what makes them worth having —
  each caught a bug the hand-written fixtures did not — so keep the byte order marks, the
  multi-line binding with quotes inside it, and the DotVVM properties on plain HTML elements.
  `MyControl` carries a code-behind class named by `@baseType`, because that is the only shape
  in which a markup control's properties can be resolved at all

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

**Do not start the runtime by the bare `dotnet` name.** An IDE launched from the Dock inherits
`/usr/bin:/bin:/usr/sbin:/sbin`, which holds no .NET installation — the macOS installer puts it
in `/usr/local/share/dotnet` and leaves no symlink. The sandbox never shows this, because Gradle
starts it from a shell that has the full PATH, so the same build works from `runRider` and fails
from the Dock, silently: the server never comes up and the status bar simply stays empty.
`DotvvmLspClientDescriptor` therefore asks `PathEnvironmentVariableUtil` for the IDE's own PATH
first, then walks `ServerBinaryLocator.dotnetSearchPath`, and only then falls back to the name.

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

`@viewModel` and `@baseType` share one grammar — a type name and an optional assembly after a
comma — so `TypeDirective` parses both and the two named classes are thin wrappers. Its
`Leading` set includes **U+FEFF**: the byte order mark is not whitespace to .NET, so `TrimStart()`
leaves it before the `@` and the directive is never recognised. Every real file has one.

`@baseType` is the only route to a markup control's properties. It is registered by file, not by
type, so the namespace lookup never reaches it and the `.dotcontrol` file itself declares
nothing; `MarkupControlResolver` reads the file, records the class name and lets the registry
match it against a type any tier contributed. It runs **after** the merge, for that reason. A
`Src` holding `embedded://` is DotVVM's own and is skipped — there is no such file.

Highlighting and navigation therefore hang off text offsets, not off a directive node —
`DirectiveAnnotator` annotates the file element, and `MasterPageNavigationHandler` resolves
`@masterPage`, `@js` and `@viewModule` against the project's content roots. Directives naming
a .NET type (`@viewModel`, `@baseType`) stay with the server, which alone has the registry.

`HtmlUnknownTagInspection` cannot be tested here: the test IU has no HTML schema at all, so it
reports `<html>` as unknown even for untouched files. Anything about schema needs the sandbox.

Quotes inside a binding (`Changed="{staticCommand: X = Y ?? ""}"`) are valid DotVVM but end
the attribute value in HTML. Three attempts to steer the HTML lexer all failed on its internal
state: `BaseHtmlLexer.start()` refuses to restart at another position, stepping the delegate
forward leaves it outside the tag, and `restore(LexerPosition)` only returns to a position
already visited. What works is not steering the lexer but handing it text without the problem —
`AttributeQuoteMasker` replaces those quotes with spaces **character for character**, so every
offset still matches the original. The masked text goes to the lexer only; the document keeps
the real quotes, and a test guards that.

Mask it in **two** places: `DotvvmParserDefinition.createLexer` builds the PSI, but the editor
paints from `SyntaxHighlighter`, which runs a lexer of its own. With only the first, the tree
is right while the closing quote and everything after it lose their attribute colouring — a
mismatch no PSI dump reveals, because the PSI is correct.

## Completion

`CompletionContextScanner` walks the text **forward** as a state machine instead of searching
back from the caret. Two reasons, both fatal to the simpler version: a third of the prefixed
tags in a real project span several lines, and the tag being typed has no closing `>` yet — so
`EndOfTag` returning "not closed" and the caller treating that as *the caret is inside* is load
bearing, not a detail.

A property's name alone is not enough to offer it. Measured over the framework's controls: of
614 properties, 50 are `MappingMode.Exclude`, 45 are capability containers, and 44 are written
as a **child element**. `ControlProperty` therefore carries `Usage` and `Value`, filled by the
probe from `MarkupOptions` and by tier 2 from `mappingMode`/`onlyBindings`/`onlyHardcoded`.

`ControlCompletion` decides *what* may be written and stays free of protocol types, the same
split as `ControlHoverText`. Snippets are used only when `capability.CompletionItem.SnippetSupport`
says so — otherwise `$0` would be inserted literally.

`MarkupControlResolver` rebuilds the registry, so anything it does not touch must be passed
through explicitly. Attached properties were lost exactly that way, and no unit test saw it:
only driving the whole chain over a real project showed a registry with none of them left.

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
- **`DotvvmProperty.ResolveProperties` needs the class constructors run first** — and not the
  type's alone. Properties are registered from static fields, and the inherited ones come from
  the base classes, so `RunClassConstructor` has to walk the whole chain. Measured on
  `dot:Repeater`: **0** properties with no constructor run, **6** with the type's own, **15**
  with the chain. The nine missing ones are `Visible`, `DataContext`, `ID`, `IncludeInPage` —
  the most used of all, so a half-done version looks like it works. It runs the user's own code,
  hence the `try`/`catch` per control.
- **Scan the registered assemblies, not just the project's.** `dot:Repeater` lives in
  `DotVVM.Framework`; without them tier 3 would know every standard control's *name* and none of
  its properties.
- **Attached properties are found by `AttachedPropertyAttribute` on the static field, nothing
  else.** Measured on 4.3.17: the marker yields exactly the 26 the framework itself serializes as
  `isAttached`, while "no backing `PropertyInfo`" yields 54 (dragging in the `Internal.*`
  plumbing) and "declared outside a control" yields 38 while losing `Validator.*`, written 503
  times in a real project.

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
