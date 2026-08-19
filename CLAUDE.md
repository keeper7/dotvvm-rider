# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DotVVM plugin for JetBrains Rider and ReSharper. Dual-platform plugin with:
- **Kotlin/Java frontend** (Rider IDE) in `src/rider/`
- **.NET backend** (ReSharper engine) in `src/dotnet/`

## Build Commands

```bash
./gradlew buildPlugin      # Full build
./gradlew compileDotNet    # .NET backend only
./gradlew testDotNet       # Run tests
./gradlew runIde           # Debug in Rider
```

## Architecture

- `src/dotnet/ReSharperPlugin.DotVVM/` - Core ReSharper plugin (shared)
- `src/dotnet/ReSharperPlugin.DotVVM.Rider.csproj` - Rider-specific .NET
- `src/dotnet/ReSharperPlugin.DotVVM.Tests/` - Unit tests
- `src/rider/main/kotlin/` - Rider frontend
- `src/rider/main/resources/META-INF/plugin.xml` - Plugin descriptor

## Version Sync

Keep `ProductVersion` in `gradle.properties` in sync with `SdkVersion` in `src/dotnet/Plugin.props`.