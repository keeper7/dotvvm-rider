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

Rider 2026.2 or newer. The language server ships with the plugin and runs on the .NET runtime
that Rider already provides.

## Building

```bash
cd plugin
./gradlew buildPlugin     # builds the plugin, bundling the language server
./gradlew test            # runs the test suite
./gradlew runRider        # launches a sandbox Rider with the plugin installed
```

## License

[MIT](LICENSE)
