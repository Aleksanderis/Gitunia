---
paths:
  - "**/*.cs"
---

# C# style and structure

Applies to every new and touched C# file, tests included. The build enforces `_field` naming, `var` for locals, formatting and unused usings (`.editorconfig`, warnings are errors); the rest is checked by the reviewer.

## Naming

- Names are spelled out: no truncated or single-letter names (`platform`, `dictionary`, `selection`, `buffer`, not `p`, `d`, `sel`, `buf`); loop indexes and well-known domain terms excepted. This applies to variables, parameters, members and test locals.
- Type names are spelled out too (`Repository`, not `Repo`). So is user-visible text: "Repository", never "Repo" (stable ids such as `repo.pin` keep the short form).
- Private and protected instance fields are `_camelCase` (consts and statics excluded).
- Comments, doc comments included, start with a capital letter.

## Signatures and expressions

- `CancellationToken` is always the last parameter.
- No tuples in signatures, fields or members: use a small record or struct.
- Locals are declared with `var` (built-in, apparent and non-apparent types alike). Explicit types stay in signatures, fields and members.
- Build text with string interpolation, not `+`.
- No fully qualified names: use `using` imports (rename a member if it hides a type).
- No magic numbers, letters or strings from git output or formats: named constants, defined once next to the format they describe (shared ones in one place, e.g. ref prefixes). Fixed argument lists and option objects are `static readonly`, not rebuilt per call.

## Files and folders

- One type per file, named after the type. Models (records, enums, plain data) and classes with logic live in separate files; unrelated models get separate files too, unless they are closely related or inherited from each other.
- Folders group by domain first (`Commits/`, `Refs/`, `Status/`), technical kind second (`Models/` for records and enums); namespaces follow folders; a test file sits in the same relative folder as the type it tests. Don't leave dozens of files in one folder.
- Member order: private methods come after all public/protected methods (fields, constructors and properties stay on top). Test classes too: test methods first, private helpers last.

## Static or instance

- Static: pure parsers, formatters and layout (same input, same output, no I/O, no clock, no cache, no state).
- Instance: anything that spawns a process, touches the file system, reads a clock or keeps a cache, even when it has no dependency yet, so it can be injected and faked. Small sealed classes that take what they use in the constructor and have no static state.
- Interface: only where another layer consumes the class and tests need a fake (a fake lives in `tests/Gitunia.Tui.Tests/Fakes`).
- Composition root: dependencies are wired by hand in `Program.Main` and `GitServices.Create`. No DI container (it needs reflection or codegen and costs startup). Tests build the same `GitServices` record, or swap one part with `with` and a fake.
- Git readers take a `GitRunner` and get the `RepositoryLocation` as an argument on every call, so one reader serves every tab.

## Testability

- No `InternalsVisibleTo`: test through the public API. If something needs testing, make it a small public type or parameter (e.g. an injected env lookup), not an internal exposed to tests.

## Native AOT

- No reflection, runtime codegen or `dynamic`; use source generators (`System.Text.Json` source gen, `LoggerMessage`). No Newtonsoft, no proxies, no runtime-codegen DI.
- Every dependency must be AOT and trimming compatible. After adding one, or touching anything reflection-adjacent, run the AOT publish and the smoke run (commands in CLAUDE.md); `IL*` warnings fail the build.
