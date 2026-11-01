---
paths:
  - "**/*.cs"
---

# C# style and structure

Applies to every new and touched C# file, tests included. The build enforces `_field` naming, camelCase local constants, `var` for locals (and `var (a, b) =` deconstruction), collection expressions, method groups instead of forwarding lambdas, blank line after a block, unused usings, locals and needless `!`, and passing the `CancellationToken` on (`.editorconfig`, warnings are errors); the rest is checked by the reviewer.

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

## Readability

- No local functions (named or lambda-valued, least of all with function parameters): make them private methods, with the shared state in fields or parameters.
- Lines stay within 120 columns (`max_line_length`): wrap long signatures, calls, initializers and conditions, one argument per line once they don't fit. Wrap every line you add or change; leave untouched lines alone.
- Blank lines separate steps: before `while`/`foreach`/`if` after a setup block, before `return`, and between groups of wiring statements. Write them as you write the code, in one pass: a statement that spans several lines (a collection or object initializer, a lambda, a chained call) has a blank line before and after it, and a run of declarations is separated from the calls or loop that use them (`var x = ...;` lines, blank, `x.Add(...)`, blank, `var y = ...;`). Long wiring methods (`Run`, composition) are split into private methods named for the step instead of one wall of inlined lambdas.
- Conditional `?:` is allowed only as one short line `a ? b : c`. No nested `?:`, no two on one line and no `?:` wrapped over several lines (a leading `?` or `:` line): use `if`/`return`, a `switch` expression, a named local or a small private method.
- Prefer `while (await ReadLineAsync() is { } line)` over declaring the variable and assigning in the condition.

## Disposal and lambdas

These are quality inspections, not style, so neither the compiler nor `.editorconfig` reports them: `scripts/inspect.sh` does (Rider's own engine). Not on every edit: `--changed` (~10 s) once a change touching lambdas, events or `using` is done, the full run (~40 s) before a PR.

- A lambda or method group never captures a variable that a `using` disposes in the enclosing scope (Rider: "captured variable is disposed in the outer scope"). Pass it as state (`token.Register(KillQuietly, process)`) or keep the object in a field and call through a private method.
- A captured local is not reassigned after the lambda is created ("captured variable is modified in the outer scope"): use a field, a `StrongBox<T>` or restructure so the lambda reads an object whose state changes. A `using` local read by a callback or fake in a test becomes a field disposed in `Dispose()`; a `ManualResetEventSlim` whose `WaitHandle` is never used needs no disposal.
- `CancellationTokenRegistration` is `await using` in async methods. Pass the token to every async call that takes one (`WaitToReadAsync(token)`).
- Fields assigned in `Run` and disposed in `Dispose` are not null-conditional (`_view.Dispose()`) unless they can really be null.
- An object that wires itself up in its constructor is created with `_ = new X(...)` and a comment saying so, not stored in an unused local.

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

## Dependencies, not callbacks

- A class gets its collaborators as objects: an interface it calls (`IBranchWriter`, a small port the owner implements), a store or a context record. No `Func<>`/`Action<>` constructor parameters and no settable delegate properties (`public Func<bool>? IsBusy { get; set; }`) to reach another object's state or behaviour, and no `null!` fields that the composition fills in later.
- Delegates are a last resort, for a decorator or wrapper whose job is to call the function it wraps. Fine as before: C# events, and a lambda passed as a plain call argument to a pure algorithm (LINQ, a comparer, `token.Register(KillQuietly, process)`), as long as it is not stored as a dependency.
- More than five constructor parameters means the class does too much: split it, or group what always travels together into one context record.
- Existing code that breaks these rules is being migrated (#965); new and touched code follows them.
- Where logic goes: decisions in Core reducers (`Core/<Feature>/`), git and file work in `Gitunia.App` effect handlers, views and input translation in `Gitunia.Tui`; a feature adds one line each to the root command, state, reducer and runner switch, in alphabetical order (`docs/architecture.md`, "Effects as data").

## Testability

- No `InternalsVisibleTo`: test through the public API. If something needs testing, make it a small public type or parameter (e.g. an injected env lookup), not an internal exposed to tests.

## Native AOT

- No reflection, runtime codegen or `dynamic`; use source generators (`System.Text.Json` source gen, `LoggerMessage`). No Newtonsoft, no proxies, no runtime-codegen DI.
- Every dependency must be AOT and trimming compatible. After adding one, or touching anything reflection-adjacent, run the AOT publish and the smoke run (commands in CLAUDE.md); `IL*` warnings fail the build.
