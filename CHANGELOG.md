# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Projects targeting **.NET 10** are supported. The probe and the view compiler ship a variant
  per framework and only `net8.0` and `net9.0` existed, so on a newer project the probe found no
  variant it could run: the plugin fell back to the serialized configuration, losing the
  project's own controls, and live validation had nothing to compile with.

  Measured against a real net10.0 build of the sample application rather than assumed: DotVVM
  4.3.17 compiles on it, the probe reports 58 controls including the project's own with their
  properties, and the view compiler answers a clean view with nothing and a deliberately broken
  one with DotVVM's own message. Both halves of the pair matter — an empty answer means little
  unless something else proves the process is alive.

### Changed

- The plugin no longer reaches for API the platform keeps to itself. Marketplace's verifier
  reported one internal call and two deprecated ones against 0.4.0: the bundled server was
  located through `PluginManagerCore.getPlugin`, which is internal, and the completion popup was
  opened from `CompletionContributor.invokeAutoPopup`, which is deprecated. The server is now
  found through `PluginPathManager` and the popup opened by a typed handler — the same extension
  point that already writes a binding's closing braces. Nothing changes for the reader; it is
  what keeps the plugin loading on the next IDE.

### Fixed

- The language server runs on a newer .NET than it was built against. It is built as `net8.0`,
  and the default policy never crosses a major version, so a machine carrying only .NET 9 failed
  with `The framework 'Microsoft.NETCore.App', version '8.0.0' was not found` — and showed
  nothing but an empty status bar, since the plugin itself loads either way. The README promised
  "a .NET 8 runtime or newer", which was not true as shipped; it is now.

  Measured before changing it: the server starts on 9.0.14 with nothing on stderr, so what stood
  in the way was the policy rather than the code.

## [0.4.0] – 2026-08-24

### Added

- Completion inside a binding expression. An opening brace offers the kinds of binding a file
  may use - `value:`, `command:`, `staticCommand:`, `resource:`, and the two control bindings in
  a `.dotcontrol`. After the colon come the members of the data context, which follows the view:
  inside an `ItemTemplate` it is the item, with `_parent`, `_index` and `_collection` beside it.
  A dot walks into whatever the expression evaluates to, however long the chain -
  `Customer.Address.City`, `Items[0].Name`, `Name.ToUpper()`.

  Which methods are offered depends on the kind of binding, measured against the framework
  rather than assumed: a `value` and a `staticCommand` reach the browser and take only what
  DotVVM can translate to JavaScript, while a `resource` and a `command` are evaluated on the
  server and take any method the type has - the view model's own included, which is what a
  command binding is usually for.

  A path may also begin with a class rather than with a value, `{resource: Fields.Title}` among
  them; the namespaces a project registers in `DotvvmStartup` count as much as a file's own
  `@import`. Measured over a real project of 245 views: of 10694 places where an expression
  begins and 8303 where a member follows a dot, none is left without an answer, at a median of
  8 ms and 30 ms at the 90th percentile. On a file mid-keystroke, with the binding at the caret
  still unfinished, 697 of 705 are answered.

  Typing `{{` writes the closing `}}` for you, unless something already closes the binding. What
  the editor offers inside a binding is kept to what may be written there: no HTML tags, and no
  Emmet abbreviations - `{{f` used to fill the list with `fieldset:d` and `form`. The file's
  header is kept clear of both as well.

  It needs a project that has been built, the same as live validation and through the same
  process - so `DOTVVM_LS_LIVE_VALIDATION=off` switches this off as well.

### Changed

- The plugin now declares the IDE builds it supports as a closed range, `262` to `262.*`,
  instead of promising every future one. It stands on the platform's LSP API, where 14 of the
  40 classes in `com.intellij.platform.lsp.api` are already deprecated by a rename; a build
  nobody has run against the next branch should not claim to work there. The range is widened
  by publishing a build, which is the cheaper half of the trade.

## [0.3.0] – 2026-08-23

### Added

- Live validation: the errors a build would report now show while editing - a mistyped property
  in a binding, an identifier the data context does not have, a value of the wrong type, a
  control that does not exist. The findings come from DotVVM's own view compiler, run over the
  file in a separate long-lived process, so the messages are the framework's own rather than a
  reimplementation of them.

  Saving compiles at once; a change waits half a second for the typing to stop, since a file
  halfway through a keystroke is not worth compiling. Measured over a real project of 244 views:
  nothing reported, a median of 13 ms per file and 45 ms at the 90th percentile once warm.

  Projects on DotVVM older than 4.3.0 are not covered - the diagnostics the feature rests on do
  not exist there - and neither is a project that has never been built. Setting
  `DOTVVM_LS_LIVE_VALIDATION=off` switches it off; there is no setting in the IDE for it yet.

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

[Unreleased]: https://github.com/keeper7/dotvvm-rider/compare/v0.4.0...HEAD
[0.4.0]: https://github.com/keeper7/dotvvm-rider/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/keeper7/dotvvm-rider/compare/v0.2.1...v0.3.0
[0.2.1]: https://github.com/keeper7/dotvvm-rider/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/keeper7/dotvvm-rider/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/keeper7/dotvvm-rider/releases/tag/v0.1.0
