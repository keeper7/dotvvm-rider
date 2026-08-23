# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.2.1] – 2026-08-23

### Added

- Completion offers the property families - `Class-`, `Style-`, `Param-` and their kind. What
  follows the prefix is the author's own word, so the offer stops there: `Class-` is inserted
  with the caret where the name goes and a second stop inside the value. Measured over DotVVM
  4.3.17, the `Class-`, `Style-` and `html:` families sit on 34 of the framework's 56 controls,
  and on 85 of a real project's own. They are offered on plain HTML elements as well, since an
  element in a view compiles to `HtmlGenericControl` and takes what it declares - which is where
  a real project writes `Class-required`, on a `<label>`. A family already written stays in the
  offer, because `Class-active` next to `Class-invalid` is the ordinary way to use it.
- Cmd-click on a control tag opens what it names: the `.dotcontrol` file for a markup control,
  the class declaring it for one registered by namespace. The platform routes an LSP definition
  through an *implicit* reference provider, which it consults only where the element carries no
  reference of its own - and an `XmlTag` always does, resolving to its own name. That
  self-reference is what underlined the tag and then led nowhere.

### Fixed

- A control read from the serialized configuration lost everything it inherits: `dot:Label`
  offered a single property there - `For` - while `Text`, `Visible` and the whole `Class-`
  family sit on the classes above it. Only a project that has never been built was affected,
  since the assembly probe resolves the chain itself.
- `dot:RouteLink` offered `Param-Id` as though it were a property. It is a use of the `Param-`
  family, where the word after the dash is the route's own parameter.
- `dot:Form` was offered, and no such control exists in DotVVM.

## [0.2.0] – 2026-08-23

### Added

- Cmd-click in a directive opens what it names: the source of the type for `@viewModel` and
  `@baseType`, and the `.csproj` for the assembly written after the comma. The two halves of
  the value lead to different places, which is the point - the assembly half used to open the
  type's source.
- The file header is now validated. A directive that cannot be repeated but is - `@viewModel`,
  `@masterPage` and their kind - a misspelt name, a missing value, a `@service` without its
  assignment, a master page that is not there and a missing `@viewModel` are all reported the
  way DotVVM itself reports them. Two things the framework passes over in silence are reported
  as warnings, since nothing else would tell: `@noWrapperTag` in a view, where it governs
  nothing, and a value on a directive that takes none.
- Completion of a directive's value, not merely its name: the view model type for `@viewModel`,
  the control type for `@baseType`, a namespace for `@import` and the path to a master page for
  `@masterPage`. Measured on a real project, those four carry 713 of its 715 directives.
  Namespaces and paths are offered shallowest first, since a file usually names the outermost
  one. With nothing known about the project the list stays empty, the same rule the validator
  follows.
- DotVVM's server-side comment `<%-- --%>` is now treated as a comment: painted like one,
  kept out of the parse tree, and produced by the comment shortcut in place of `<!-- -->`.
  Unlike the HTML form it never reaches the browser, which is why it is the one a DotVVM file
  wants. It is understood between a tag's attributes too - `<th <%-- width="30%" --%>>` -
  which DotVVM allows and HTML has no equivalent for.
- Completion of a control's properties inside its tag, with the value it takes: a property that
  accepts nothing but a binding is inserted as `Name="{value: }"`, anything else as `Name=""`.
  Required properties come first, then the control's own, then the attached ones. A property
  written as a child element is not offered as an attribute, and one already on the tag is not
  offered twice. Completing over an attribute that already has a value renames it and leaves the
  value alone.
- Completion of attached properties - `Validation.Enabled`, `Validator.Value` and their kind -
  on any element, plain HTML included.
- Hover reports the properties of the project's own controls. The assembly source now reads the
  control types themselves, not merely their registrations, and a markup control is connected to
  its code-behind class through the `@baseType` directive - which is where its properties live,
  since a `.dotcontrol` file declares none.

### Fixed

- A directive's path is resolved against the project's own root - the nearest directory holding
  a `.csproj` - rather than against a content root of the IDE. With a repository larger than the
  web app open, `Views/Site.dotmaster` pointed above the project and navigation found nothing.
- The server starts for a client that does not ask for completion. Reading the absent capability
  threw, and that took `initialize` itself down.
- The list of directive names now matches DotVVM. `viewModule` was offered although no such
  directive exists - the view module one is called `js` - while `resourceType`,
  `resourceNamespace` and `wrapperTag` were missing. The parser accepts any name at all, so
  the offered list is the only place a typo shows.
- Markup inside a server-side comment is no longer taken for code. A control switched off with
  `<%-- --%>` was underlined as unknown, described on hover and had its bindings resolved -
  the scanner did not know the marker and simply walked into it. A comment between a tag's
  attributes broke the rest of the file with it, since the tag never closed.
- Completion no longer offers tag prefixes in the middle of text or inside an attribute value.
  It used to fall back to them whenever it could not find a tag on the caret's own line, which
  also meant it saw nothing at all in a tag spanning several lines - a third of them in a real
  project.
- Hover no longer lists the properties that cannot be written: `ClientID` and the capability
  containers such as `HtmlCapability`. Measured over the framework's controls, 95 of 614.
- Hover no longer calls every control it does not know a markup control. It now says which of
  the three it actually is - an unregistered prefix, a control missing from the registry, or a
  markup control whose `@baseType` class was not found - matching what the squiggle under the
  same tag says. With nothing loaded about the project it claims nothing at all.
- `@viewModel` navigation on a file starting with a byte order mark. U+FEFF is not whitespace to
  .NET, so the directive was never recognised - and every real file has one.

### Known limitations

- Until the project is built, the server knows nothing of its own controls and stays silent
  about them rather than flagging them — the status bar says what it currently knows
- Validation covers the file header and tag names; the errors a full compilation would give
  are not there yet
- `@import` values are not checked, since nothing separates a namespace the registry does not
  hold from one that does not exist
- Verified on macOS only so far

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
- Completion of control prefixes and tags
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

[Unreleased]: https://github.com/keeper7/dotvvm-rider/compare/v0.2.1...HEAD
[0.2.1]: https://github.com/keeper7/dotvvm-rider/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/keeper7/dotvvm-rider/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/keeper7/dotvvm-rider/releases/tag/v0.1.0
