---
name: roslyn-investigate
description: Investigate C# declarations, references, callers, implementations, diagnostics, and code structure semantically with Roslyn. Prefer this over text search for C# symbols and relationships. Do not use for non-C# code, string literals, or configuration values.
---

# Roslyn Investigate

Use Roslyn semantic results as the first source of evidence when locating or tracing C# symbols.

## Prepare the command

1. Run `roslyn --help` to check whether the packaged tool is available.
2. If it is unavailable, locate this plugin or repository checkout and use:

   ```bash
   dotnet run --project <roslyn-cli-root>/src/RoslynCli -- <arguments>
   ```

3. If neither command is available, report that the Roslyn CLI must be installed or built. Do not pretend semantic search ran.

Call the selected executable `<roslyn>` below.

## Investigate

1. Locate the nearest `.sln`, `.slnx`, or `.csproj` containing the code in scope.
2. Search the smallest meaningful name fragment:

   ```bash
   <roslyn> symbol search <solution-or-project> <pattern> --format json
   ```

3. Narrow noisy results with `--kind type|method|property|field|event` and `--limit`.
4. Use exact-name or position-aware navigation when the request needs relationships:

   ```bash
   <roslyn> symbol references <solution-or-project> <symbol> --format json
   <roslyn> symbol usages <solution-or-project> <file> --line <line> --column <column> --format json
   <roslyn> symbol info <solution-or-project> <symbol> --format json
   ```

5. For callers, implementations, hierarchy, outline, metrics, or flow analysis,
   select the matching operation from `<roslyn> tool list` and run it with
   `<roslyn> tool run <solution> <operation> --params '<json>'`.
6. Read only the returned source locations needed to answer the request.
7. Explain the finding with qualified symbol names and clickable file locations when available.
8. Use text search only for literals, configuration, generated artifacts, or semantic-load failures.

## Interpret results

- Treat `count: 0` as no matching source declaration, not proof that the concept is absent.
- Expect `schemaVersion`, `count`, and `results` in JSON output.
- Distinguish overloads and partial declarations by qualified name and source location.
- Report solution-load and compilation failures explicitly.
- Do not infer callers, references, or implementations from declaration results; run the corresponding semantic operation.
- Do not modify source during investigation unless the user separately requests a change.
