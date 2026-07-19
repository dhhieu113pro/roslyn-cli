---
name: roslyn-investigate
description: Investigate C# and .NET codebases with the Roslyn CLI using semantic symbol discovery. Use when Codex needs to locate type or member declarations, distinguish symbols from text matches, identify exact source locations, or gather evidence before explaining C# behavior.
---

# Roslyn Investigate

Use `roslyn symbol search` before text search when locating C# declarations.

## Workflow

1. Locate the nearest `.sln`, `.slnx`, or `.csproj` that contains the code in scope.
2. Search for the smallest meaningful name fragment:

   ```bash
   roslyn symbol search <solution-or-project> <pattern> --format json
   ```

3. Narrow noisy results with `--kind type|method|property|field|event` and `--limit`.
4. Read only the returned source locations needed to answer the question.
5. Cite qualified symbol names and file locations in the explanation.
6. Fall back to text search for string literals, configuration, generated artifacts, or when semantic loading fails.

## Guardrails

- Treat an empty result as “no matching source declaration,” not proof that a concept is absent.
- Report solution-load or compilation failures; do not silently replace semantic evidence with guesses.
- Prefer JSON for automated workflows. Expect `schemaVersion`, `count`, and `results` at the top level.
- Do not modify source files during investigation unless the user separately requests a change.
