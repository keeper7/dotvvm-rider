# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DotVVM plugin for JetBrains Rider. Native Kotlin plugin built on the IntelliJ Platform's HTML
infrastructure — **no .NET backend, no ReSharper engine, no RD protocol**.

`.dothtml`, `.dotmaster` and `.dotcontrol` are treated as an HTML superset: the language extends
`HTMLLanguage`, so HTML/CSS/JS support comes for free. Binding expressions (`{value: Name}`) are
located by a hand-written scanner — not a regex, which broke on nested braces and on `}` inside
string literals — and injected as a separate `DotVVMBinding` language with its own lexer and
highlighter.

Semantic features (validation, completion) are planned as a separate C# LSP server.

## Build Commands

All Gradle commands run from `plugin/`, which is a standalone Gradle project with its own wrapper:

```bash
cd plugin
./gradlew buildPlugin                    # Full build
./gradlew test                           # All tests (28 as of task 7)
./gradlew test --tests "*ScannerTest*"   # Single test class
./gradlew runRider                       # Debug in a sandbox Rider — the target IDE
./gradlew runIde                         # Sandbox IDEA Ultimate (the compile platform)
```

Run long builds in the background with output to a file, not piped into `tail` — a pipe returns the
pipe's exit code, masking a failed build. Beware the same trap in `cmd > log 2>&1; echo $?`, where
the reported code belongs to `echo`, not to the build.

## Architecture

- `plugin/src/main/kotlin/com/keeper7/dotvvm/lang/` — language, file types and parser definition
- `plugin/src/main/kotlin/com/keeper7/dotvvm/binding/` — binding scanner, lexer, injector, highlighter
- `plugin/src/main/resources/META-INF/plugin.xml` — plugin descriptor
- `fixtures/SampleApp/` — sample DotVVM app for manual and integration testing

Registering `HTMLParserDefinition` directly for the DotVVM language is not enough — it builds the
PSI file with `HTMLLanguage` hardcoded, so `psiFile.language` never returns DotVVM. That is what
`DotvvmParserDefinition` overrides.

`BindingScanner` decides **where** bindings are and must stay free of any IntelliJ API, so it can be
tested with plain JUnit. `BindingInjector` decides **how** to hand them to the platform and is tested
through `BasePlatformTestCase`. Keep that split — it is why the hardest logic is testable without an IDE.

## Testing

JUnit 4 runner, two styles side by side:
- Plain unit tests with `@Test` annotations (`BindingScannerTest`)
- Platform tests extending `BasePlatformTestCase`, using the JUnit 3 `fun testXxx()` convention

Anything touching `IElementType` or `TextAttributesKey` needs `BasePlatformTestCase` — those write to
a global platform registry and fail without an initialised `Application`.

## Versions and platform

Kotlin 2.4.10, IntelliJ Platform Gradle Plugin 2.18.1, Gradle 9.7.1, JVM target 21,
`since-build = 262`.

**The compile/test platform is IntelliJ IDEA Ultimate 2026.2.1, not Rider** — `BasePlatformTestCase`
cannot run against Rider at all: Rider preloads services that need an open solution, so test
application startup dies with `solution can't be null`. This was verified with a control test
containing no plugin code. IU 2026.2.1 is build `262.9437.185` and Rider 2026.2.1 is `262.9437.287`,
so it is the same platform version minus the .NET backend, and the plugin uses no Rider API.

The cost is that `<depends>com.intellij.modules.rider</depends>` had to go — otherwise the plugin
would not load in IU. The plugin is therefore no longer formally Rider-only. Revisit this before
publishing to Marketplace.

Two related gotchas: Rider does not publish its test-framework as an artifact (it needs
`TestFrameworkType.Bundled` — note the docs wrongly say `TestFrameworkType.Platform.Bundled`), and
Rider rejects installer distributions, so it needs `useInstaller.set(false)`.

## Planning Documents

Analysis, design and three implementation plans live in `.private/analyzy/` — outside git, start with
`ZACNI-TADY.md`.
