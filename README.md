# DotVVM for JetBrains Rider

DotVVM support for Rider: syntax highlighting, navigation and validation for `.dothtml`,
`.dotmaster` and `.dotcontrol` files.

These files are treated as a superset of HTML, so the IDE's HTML, CSS and JavaScript support
keeps working. Binding expressions are highlighted as a language of their own, and a bundled
language server adds what the editor alone cannot know: which control prefixes the project
registers, which tags are valid, and where the view model lives.

## Features

- Highlighting of binding expressions — `{value: …}`, `{command: …}`, `{staticCommand: …}`,
  `{resource: …}` — with correct handling of nested braces and of quotes inside the expression
- Directives: highlighting, completion of names, navigation from `@masterPage` to its file
- Validation of tags against the controls the project registers
- Navigation from `@viewModel` to the view model class, driven by the directive rather than
  by a file naming convention
- A status bar indicator naming which source of project data is currently available

## Requirements

Rider 2026.2 or newer, and a .NET 8 runtime or newer on the machine. The language server ships
with the plugin, but it is published framework-dependent, so it needs a runtime to start it —
Rider's own is not used. The plugin looks for one in the IDE's PATH and in the usual install
locations; when it finds none, the status bar stays empty and the idea.log says so.

## Installing

Until the plugin is on the Marketplace, install the built distribution by hand:

1. `cd plugin && ./gradlew buildPlugin` — the zip lands in `plugin/build/distributions/`
2. *Settings → Plugins → ⚙ → Install Plugin from Disk…*, pick that zip, restart Rider
3. *Settings → Editor → File Types* — remove `*.dothtml` and `*.dotmaster` from **Razor** and
   `*.dotcontrol` from **Blazor**

Step 3 is not optional. A file type mapping made by hand wins over the one the plugin declares,
so while those extensions belong to Razor and Blazor the plugin does not show at all — which
looks exactly like the plugin being broken.

## Building

```bash
cd plugin
./gradlew buildPlugin     # builds the plugin, bundling the language server
./gradlew test            # runs the test suite
./gradlew runRider        # launches a sandbox Rider with the plugin installed
```

## License

[MIT](LICENSE)
