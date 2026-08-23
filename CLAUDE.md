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
./gradlew test                           # All tests (153; the server has 272 of its own)
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
- `server/src/DotVVM.LanguageServer.Compiler/` — runs DotVVM's own view compiler, long-lived
- `fixtures/SampleApp/` — sample DotVVM app for manual and integration testing; it is a real
  buildable app, because the probe needs a built assembly and go-to-definition needs a `.csproj`.
  `SiteMaster.dotmaster` and `Address.dotcontrol` are written for this fixture, and their
  **structure** is what makes them worth having —
  each caught a bug the hand-written fixtures did not — so keep the byte order marks, the
  multi-line binding with quotes inside it, and the DotVVM properties on plain HTML elements.
  `MyControl` carries a code-behind class named by `@baseType`, because that is the only shape
  in which a markup control's properties can be resolved at all.

  **`Sample.dothtml` compiles cleanly and has to keep doing so** — live validation runs the real
  compiler over it, so a mistake there shows up as noise in every manual round. What it holds is
  chosen to survive that: `>` inside a binding, a closing brace inside a string literal, `{0}`
  inside one, a comment between attributes. An anonymous type (`new { A = x.Name }`) used to
  stand there as the nested-braces case and had to go — **DotVVM does not compile one at all**,
  its binding ends at the first `}`, so the file had been broken since it was written and only
  the compiler noticed. `SiteMaster.dotmaster` and `Address.dotcontrol` are a different matter:
  their worth is their *structure*, and making them compile would
  mean writing the resource classes and the data model of a whole imaginary
  application

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
mismatch no PSI dump reveals, because the PSI is correct. Both now run through the single
`DotvvmMaskingLexer`, which is what keeps them from drifting apart; `DotvvmMasks.applyAll`
chains every mask, and it is the only caller of the individual maskers.

## Server-side comments

`<%-- --%>` gets the same treatment: `ServerCommentMasker` turns it into `<!-- -->` and the
lexer does the colouring and the parsing on its own, with no annotator. The padding space goes
**before** the closer (`--%>` → ` -->`), not after it. With `--> ` the `XmlComment` ended one
character early and the final `>` fell out of it as whitespace — unpainted, on the first line
anyone looks at.

**Between attributes the trick is not available.** HTML knows no comment inside a tag, so
`<!--` there reads as three more attributes, the tag never closes, and the rest of the file
falls apart with it — `</th>` included. DotVVM does allow the form, which is worth checking
before deciding it is the user's mistake: its own tokenizer parses
`<th <%-- width="30%" --%>>` with no error and keeps the attributes on either side. The masker
therefore **blanks such a comment out**, character for character but leaving the line breaks —
blanking those would shift every line number after it, and LSP diagnostics are addressed by
line and column. `ServerCommentAnnotator` then puts the colour back off text offsets, the way
`DirectiveAnnotator` does. On the server the same case needed two branches in
`CompletionContextScanner`: `EndOfTag` would otherwise stop at a `>` inside the comment, and
the attribute walk would stop at its `<`.

Two things the mask alone does not fix, because they read the PSI rather than the token stream:

- The comment becomes an `XmlComment` **inside** an `XmlText`, and `XmlText` is a host
  `BindingInjector` injects into. Without skipping the comment's ranges, a commented-out
  binding stays live code — highlighted, resolved and navigable.
- `DothtmlScanner` on the server has its own branch for `<%--`. Without it `<` passed, `%` was
  neither a letter nor a colon, and the scanner stepped one character on — straight into the
  comment. Both `TagValidator` and `HoverHandler` go through it, so one branch fixed both.

`DotvvmCommenter` registers the block form only; DotVVM has no line comment, so the platform
falls back to the block one for the line action too.

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

The caret **touching** an attribute's name means that attribute is being replaced — including
at its first character, because the editor replaces the whole name from there too. One rule,
two consequences: it is not counted among the written ones (so it is offered back), and only the
name is inserted, leaving its value alone. Getting either half wrong is visible immediately:
completing over `|Text="x"` produced `Enabled=""="x"`.

The space is declared as a trigger character *and* `LspCompletionSupport.isTriggerCharacterRespected`
is overridden — the platform asks before acting on one, and without the override the list opened
only on the first letter or on Ctrl+Space.

`ControlCompletion` decides *what* may be written and stays free of protocol types, the same
split as `ControlHoverText`. Snippets are used only when `capability.CompletionItem.SnippetSupport`
says so — otherwise `$0` would be inserted literally, and a group uses `$1` as well, so the
fallback strips every `$n` rather than that one.

**Property groups are a second kind of property.** `Class-active`, `Style-width`, `Param-Id` —
a prefix plus a word the author picks, so only the prefix can be offered. Measured over 4.3.17:
`Class-`, `Style-` and the `Attributes` family sit on 34 of the framework's 56 controls, and on
85 of a real project's own; `Param-`/`Query-` belong to `RouteLink`. `Attributes` carries the
**empty** prefix, meaning any attribute at all goes — nothing to offer, so empty prefixes are
dropped when read. Unlike a property, a group is never filtered by what is already written.
The probe reads them from `DotvvmPropertyGroup.GetPropertyGroups(type)`, which resolves
inheritance itself; tier 2 has them under `propertyGroups`, with one prefix under `prefix` and
several under `prefixes`.

**They belong on plain HTML elements too.** An element in a view compiles to
`HtmlGenericControl`, so `<label Class-required="{value: X}">` is ordinary DotVVM — checked
against the framework's resolver, which accepts it, and it is how a real project writes it.
`ControlRegistry.HtmlElementGroups` is what the offer uses where there is no prefix. It stays
empty on tier 1, whose list holds only controls a view writes by name.

**Tier 2 lists only what each type declares.** `dot:Label` holds one property there (`For`);
`Text`, `Visible` and the `Class-` group all sit above it. `ControlRegistry.GetControl`
therefore walks the `BaseType` chain and returns the control with everything it inherits, while
`Controls` keeps what the sources said. `baseType` is written *with* the assembly there and
`FullTypeName` without, hence `ControlInfo.BareBaseType`.

`MarkupControlResolver` rebuilds the registry, so anything it does not touch must be passed
through explicitly. Attached properties were lost exactly that way, and no unit test saw it:
only driving the whole chain over a real project showed a registry with none of them left.

## Directive values

`DirectiveContextScanner` says whether the caret stands in a directive's value;
`DirectiveCompletion` says what belongs there. Same split as the pair for tags, and the values
travel by ordinary `textDocument/completion` — no `dotvvm/*` request was needed, because the
server already receives the position and `ProjectRoot.Find` gives it the project root.

`ParserConstants` in `DotVVM.Framework` is the only authority on directive names. The parser
accepts **any** name — `@totalNonsense Something` yields a well-formed directive node with no
error, and only compiling the view rejects it — so the offered list is where a typo shows.
Measured against it, `DirectiveScanner` used to offer `viewModule`, which does not exist (the
view module directive is `js`), and to miss `resourceType`, `resourceNamespace` and `wrapperTag`.

**View models must not be filtered by visibility or abstractness.** Measured on a real project:
requiring `IsPublic` drops 60 of 177, since DotVVM instantiates them by reflection; requiring
`!IsAbstract` drops the four `Base*MasterViewModel` types that serve master pages — the files
where a human writes the directive most often. With both filters the offer misses 61 of the
178 types the views declare.

`@js` looks like a path and is not one: it names a resource registered in `DotvvmStartup`,
which is why `ViewModuleDirectiveCompiler` takes a `DotvvmResourceRepository`. Listing `.js`
files off the disk offered entries like `build-docker.js`. It is left empty on purpose, and
`MasterPageNavigationHandler` still treats it as a path — a leftover worth revisiting.

**An auto-popup opens with no item selected.** In the body the platform picks one itself, which
is why Tab always inserted a tag and only the directive popup was dead: `currentItem` was null,
so Tab typed a tab character. Measured — setting `LookupFocusDegree.FOCUSED` changes nothing,
setting `currentItem` alone is enough. `DirectiveLookupFocus` fills in an absent selection, and
it has to do so from a `LookupListener` on `uiRefreshed`: when the lookup appears it holds no
items yet.

Test this through `CompletionAutoPopupTestCase`, never `completeBasic`. Explicit completion
selects an item on its own, so Tab has always worked there — a green suite said nothing about
the popup the user sees.

**A directive's path is relative to the DotVVM project's root** — the nearest directory upwards
holding a `.csproj` — not to a content root of the IDE. The two differ as soon as the opened
project is larger than the web app: with the whole repository open, `Views/Site.dotmaster`
resolved against the repository root, where no such file is, and navigation silently found
nothing. `MasterPageNavigationHandler` now reads it the way the server always has, and falls
back to the content roots only when there is no `.csproj` at all.

Two navigation traps in one: a test that calls `getGotoDeclarationTargets` directly proves
nothing about whether the platform ever asks. Go through
`GotoDeclarationAction.findTargetElement`, the way Cmd+click does.

**Navigation out of a tag is the plugin's too, and for a different reason.** The platform routes
an LSP definition through `psi.implicitReferenceProvider`, and *implicit* means it asks only
where the element carries no reference of its own — an `XmlTag` always carries one, resolving to
its own name. That self-reference is what underlines `<cc:MyControl>` and then leads nowhere but
back to the tag. `ControlNavigationHandler` resolves it instead, off registrations the server
sends as `dotvvm/controlRegistrations` beside the tier; `ControlRegistrations` holds them.
Read the name from the **token**, not from `XmlTag.name`: an HTML tag reports its name
lower-cased, and `cc:mycontrol` matches no registration.

**Navigation to a type is done in the plugin, not over LSP**, although the server answers
`textDocument/definition` for `@viewModel` correctly — verified by hand. A directive is not
markup: the PSI holds it as bare `XML_DATA_CHARACTERS` directly under `HTML_DOCUMENT`, not even
wrapped in `XmlText`, and on such a position the platform never asks the LSP client — the link
was not even underlined, while `@masterPage`, handled by the plugin, both underlined and jumped.
Finding the file is a filesystem search either way, and the plugin has `FilenameIndex`.

**The two halves of a type directive's value lead to different places** — the type to its `.cs`
source, the assembly after the comma to the `.csproj` that builds it. One range covering the
whole value sent a click on the assembly to the type's source, the one file the reader was
demonstrably not asking about. Assembly names match their project files unless `<AssemblyName>`
says otherwise; when nothing matches, navigation stays silent rather than guessing.

## Validating directives

What DotVVM refuses is not guesswork: running its own `IControlTreeResolver` over a broken
header makes it say so, because the resolver writes its complaints back onto the parser's
nodes. That is where `DirectiveValidator`'s messages come from. `MarkupPageMetadata` settles
how many of each may appear — what it holds as an `ImmutableList` may repeat (`@import`,
`@service`, `@property`), everything else may not.

**A type is judged only when its namespace is known.** Measured over a real project: without
that rule the check reports `@viewModel System.Object` and five `@import` values — eight valid
directives — because the registry holds the project's assemblies, not the BCL's. Same reasoning
as `KnowsProjectPrefixes` for tags, one storey down. `@import` is never judged at all: its value
*is* a namespace, so nothing tells an unknown one from a wrong one.

A misspelt directive name is worth an error even though DotVVM ignores it, and precisely
because it does: nothing else would ever tell the user.

Measured on a real project of 244 files, the whole validator reports **nothing** — which is
also why it has to be tested on deliberately broken headers, and why any finding on real code
should be treated as a false alarm until proven otherwise.

**A capability can be absent, not merely empty.** `CompletionHandler.GetRegistrationOptions`
dereferenced its `CompletionCapability` unguarded, so a client that does not ask for completion
at all killed `initialize` itself — the server never came up. The IDE does ask, which is why
this only surfaced when a hand-written client asked for `definition` alone.

## Live validation

`TagValidator` and `DirectiveValidator` see only what can be judged without compiling. The rest —
a mistyped property in a binding, a wrong data context, a value of the wrong type — comes from
running DotVVM's **own** compiler over the file, in `DotVVM.LanguageServer.Compiler`.

`IViewCompiler.CompileView(source, fileName)` takes the **text**, which is what a buffer being
edited needs. Two traps sit in that one call: the `Func<IControlBuilder>` it returns is **lazy**,
so nothing is compiled and even a plainly broken file comes back clean until the Func is called;
and the result is **cached by fileName**, so without `InvalidateCache` the second run of a file
reports the errors of the version before it. Both cost a measurement round.

`DotvvmCompilationException.AllDiagnostics` carries `Message`, `Severity` and a `Location` with
start *and* end line and column — an LSP `Diagnostic` one for one, `DiagnosticConversion` only
shifts 1-based to 0-based. An empty range is widened to one character, because DotVVM reports one
for an unfinished tag and nothing would be underlined.

**The process must be long-lived.** The first compilation pays for Roslyn waking up — measured at
14 s on a cold start — and every one after it costs milliseconds: over a real project of 244
views, a median of 13 ms and 45 ms at the 90th percentile. A process per request is out of the
question, which is the opposite of the probe's design.

**It has to be started with the target application's own `deps.json` and `runtimeconfig.json`**
(`dotnet exec --depsfile … --runtimeconfig …`). DotVVM's `CompiledAssemblyCache` reads
`DependencyContext.Default`, which comes from the **entry assembly's** deps.json; with our own,
the project's assembly is absent — measured: 329 assemblies loaded in the process, 317 in that
list, the project's not among them — and `DefaultControlResolver` throws in its constructor
before a single view is compiled. In a real application the entry assembly *is* the application,
which is why nothing there ever hits this.

**Nothing in `Program.Main` may touch a DotVVM type.** The reference is built against the newest
DotVVM while the project may be on an older one, and the JIT resolves the types a method mentions
when it compiles that method — so a mention in `Main` would demand exactly that version before
the assembly resolver is registered. Measured: without the split into `Session`, a build against
4.3.17 does not run against a project on 4.3.6 at all; with it, one build serves both. The
reference is 4.3.17 because it is the oldest version with no published vulnerability, and
`DotvvmCompilationDiagnostic` exists from 4.3.0 — older projects get no live validation.

`CreateDefault()` is missing one service the compiler needs: **`IViewModelProtector`**, which
`StaticCommandMethodTranslator` takes in its constructor. Without it every `staticCommand` in the
project fails — measured on a real project, 13 files and 44 diagnostics, all from that one
service. `ViewModelProtectorStub` supplies it; nothing ever protects anything, compilation only
needs the service to exist.

The compiler is handed the **project's root**, not the folder the view sits in: DotVVM resolves a
markup control's `Src` and a master page's path against it, and with the view's own folder a
registered `<cc:MyControl>` is reported as a file that was not found. For the same reason
`LiveValidation` keys its processes by root — keyed by folder, a project would end up with one
Roslyn per directory.

`LiveValidation` decides *when*: a save compiles at once, a change waits 500 ms for the typing to
stop. A file halfway through a keystroke is not worth compiling — an unfinished tag alone yields
three complaints, one of them about the end of the file.

Calibrated the way `DirectiveValidator` was: over a real project of 244 views the whole thing
reports **nothing**, and a finding on real code should be treated as a false alarm until proven
otherwise. Proven silence is not deadness — the same project with a binding identifier renamed,
a control renamed or a tag left open reports each of them.

`DOTVVM_LS_LIVE_VALIDATION=off` switches it off. Something that starts a process running the
user's own code and holding a Roslyn of its own needs a way out short of uninstalling the plugin.
There is no setting in the IDE for it yet — that is the piece still missing.

The child process ends on its own when the server dies: its `Console.In.ReadLine()` returns null
once the pipe closes. Verified with `kill -9` on the server — no process left behind, unlike the
`dotnet` processes the tests used to leak.

## The LSP client at run time

**Do not let a test start the server.** `BasePlatformTestCase` tears the project down without
closing the LSP client, so every test that opened a `.dothtml` file left a `dotnet` process
alive — 48 were found running at once, the oldest over a day old. `DotvvmLspIntegrationProvider`
therefore returns early under `isUnitTestMode`, and a test guards that.

The platform's own `LspClientImpl.start` can die with a `ConcurrentModificationException` inside
`LspDocumentSyncManager.forEachOpenedFile` when several supported files are restored at IDE
startup. Nothing in this plugin appears in that stack; the effect is `Failed to start LSP server`
in the log and no server at all for that session. Reopening one of the files starts it again.

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
Refreshing it needs `prepareSandbox_runRider`, not `prepareSandbox`: the latter fills the
*default* sandbox and leaves the running one untouched. Either way the sandbox has to be
restarted afterwards — overwriting the plugin underneath a running IDE shuts it down.

**Closing the sandbox leaves its processes behind.** `JBDevice.framework` daemons outlive the
IDE that started them: eight were found running at once, the oldest three days old, alongside
the sandbox's own language server — which now drags the view compiler along with it, since that
is its child and holds a Roslyn of its own. Killing the server does take the compiler down
(verified), so one `kill` on the orphaned `dotnet` clears both. Both leak the same way and neither is ever reaped, so a few
rounds of `runRider` quietly cost a good deal of memory. Tell them from the real Rider's by
their path — the sandbox runs them out of the Gradle cache
(`transforms/…/riderRD-*/bin/JBDevice.framework/`), the installed IDE out of
`Rider.app/Contents/bin/` — and kill only the former. This is the same trap as the `dotnet`
processes tests used to leave; there `isUnitTestMode` fixed it at the source, here there is
nothing to fix, only to sweep up afterwards.

Two related gotchas: Rider does not publish its test-framework as an artifact (it needs
`TestFrameworkType.Bundled` — note the docs wrongly say `TestFrameworkType.Platform.Bundled`), and
Rider rejects installer distributions, so it needs `useInstaller.set(false)`.

## Planning Documents

Analysis, design and three implementation plans live in `.private/analyzy/` — outside git, start with
`ZACNI-TADY.md`.
