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
./gradlew test                           # All tests
./gradlew test --tests "*ScannerTest*"   # Single test class
./gradlew runIde                         # Debug in a sandbox Rider
```

The first build downloads the Rider SDK (~2.5 GB). Run it in the background with output to a file,
not piped into `tail` — a pipe returns the pipe's exit code, masking a failed build.

## Architecture

- `plugin/src/main/kotlin/com/keeper7/dotvvm/lang/` — language and file types (extend HTML)
- `plugin/src/main/kotlin/com/keeper7/dotvvm/binding/` — binding scanner, lexer, injector, highlighter
- `plugin/src/main/resources/META-INF/plugin.xml` — plugin descriptor
- `fixtures/SampleApp/` — sample DotVVM app for manual and integration testing

`BindingScanner` decides **where** bindings are and must stay free of any IntelliJ API, so it can be
tested with plain JUnit. `BindingInjector` decides **how** to hand them to the platform and is tested
through `BasePlatformTestCase`. Keep that split — it is why the hardest logic is testable without an IDE.

## Testing

JUnit 4 runner, two styles side by side:
- Plain unit tests with `@Test` annotations (`BindingScannerTest`)
- Platform tests extending `BasePlatformTestCase`, using the JUnit 3 `fun testXxx()` convention

Anything touching `IElementType` or `TextAttributesKey` needs `BasePlatformTestCase` — those write to
a global platform registry and fail without an initialised `Application`.

## Versions

Target platform is Rider 2026.2.1 (`since-build = 262`), Kotlin 2.4.10, IntelliJ Platform Gradle
Plugin 2.18.1, Gradle 9.7.1, JVM target 21. Keep `platformVersion` in `plugin/gradle.properties` and
`sinceBuild` in `plugin/build.gradle.kts` in sync.

## Planning Documents

Analysis, design and three implementation plans live in `.private/analyzy/` — outside git, start with
`ZACNI-TADY.md`.
