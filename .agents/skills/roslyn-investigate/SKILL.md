---
name: roslyn-investigate
description: Find and explain C# declarations semantically with Roslyn. Use for requests such as "where is this class or method defined?", "find this C# symbol", "which declaration matches this name?", or "locate the source before explaining this .NET behavior." Prefer this over text search for C# types, methods, properties, fields, and events. Do not use for non-C# code, string literals, configuration values, or reference/caller tracing that the current CLI does not support.
---

# Roslyn Investigate

Use Roslyn semantic results as the first source of evidence when locating C# declarations.

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
4. Read only the returned source locations needed to answer the request.
5. Explain the finding with qualified symbol names and clickable file locations when available.
6. Use text search only for literals, configuration, generated artifacts, or semantic-load failures.

## Interpret results

- Treat `count: 0` as no matching source declaration, not proof that the concept is absent.
- Expect `schemaVersion`, `count`, and `results` in JSON output.
- Distinguish overloads and partial declarations by qualified name and source location.
- Report solution-load and compilation failures explicitly.
- Do not infer callers, references, or implementations from declaration results.
- Do not modify source during investigation unless the user separately requests a change.
