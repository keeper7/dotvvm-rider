# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Hover reports the properties of the project's own controls. The assembly source now reads the
  control types themselves, not merely their registrations, and a markup control is connected to
  its code-behind class through the `@baseType` directive - which is where its properties live,
  since a `.dotcontrol` file declares none.

### Fixed

- `@viewModel` navigation on a file starting with a byte order mark. U+FEFF is not whitespace to
  .NET, so the directive was never recognised - and every real file has one.

## [0.1.0] – 2026-08-22

First release. The plugin treats `.dothtml`, `.dotmaster` and `.dotcontrol` as a superset
of HTML, so all HTML, CSS and JavaScript support keeps working unchanged.

### Added

**Binding expressions**
- Highlighting for `{value: …}`, `{command: …}`, `{staticCommand: …}`, `{resource: …}`
  and the other kinds, both in attributes and in text
- Expressions are located by a hand-written scanner, so neither nested braces
  (`{value: new { A = x.Name }}`) nor a brace inside a string literal
  (`{value: Format("}")}`) ends them early
- A quote inside an expression (`{staticCommand: A = B ?? ""}`) no longer ends the
  attribute value, even though HTML would read it that way

**Directives**
- Highlighting for the directive name and its value in the file header
- Completion of directive names; the popup opens as soon as `@` is typed
- Navigation to the referenced file from `@masterPage`, `@js` and `@viewModule`

**Project knowledge (language server)**
- Validation of tags against the registered controls
- Completion of controls and their properties
- Navigation to the view model from the `@viewModel` directive, even when the file name
  does not match the view name
- Documentation on hover
- Project data comes from three sources of increasing accuracy: a built-in list of standard
  controls, the configuration written by the last application run, and the project's compiled
  assembly. A higher source adds to the lower ones, so standard controls are known even in a
  project that has never been built
- A status bar indicator names the source currently in use — without it there would be no way
  to tell why the server does not know the project's own controls

### Fixed

Suppressed the messages the platform reports only because it does not know DotVVM:

- `Unexpected tokens` on `<!DOCTYPE html>` following directives
- `Namespace 'dot' is not bound` on every prefixed control
- `Attribute … is not allowed here` on DotVVM properties used on HTML elements
  (`Visible`, `Class-required`, `Validator.Value` and the like)

The suppression is deliberately narrow: a typo in an ordinary HTML attribute or tag is still
reported.

### Known limitations

- Until the project is built, the server does not know its own controls and does not flag
  them as errors — the status bar says what it currently knows
- Completion covers directive names, not their values
- Verified on macOS only so far

[Unreleased]: https://github.com/keeper7/dotvvm-rider/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/keeper7/dotvvm-rider/releases/tag/v0.1.0
