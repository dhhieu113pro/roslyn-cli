# Roslyn CLI

Semantic command-line tooling for C# repositories, paired with agent skills that use its structured output.

## Showcase

Roslyn CLI searches compiled C# symbols rather than matching arbitrary text. It understands namespaces, member kinds, overloads, nested declarations, and partial types while returning exact source locations.

All examples below use the included `samples/SkillFixture/SkillFixture.slnx` solution.

### Jump to a method declaration

Find a method without matching comments, documentation, or string literals:

```bash
roslyn symbol search samples/SkillFixture/SkillFixture.slnx \
  ProcessPaymentAsync --kind method
```

```text
method   SkillFixture.Payments.PaymentService.ProcessPaymentAsync(
         SkillFixture.Domain.Order, System.Threading.CancellationToken)
         /repo/samples/SkillFixture/.../PaymentService.cs:9:39
```

This is useful when entering an unfamiliar service and you know a class or member name but not its project, namespace, or file.

### Discover overloads

Search by semantic member kind to see every overload:

```bash
roslyn symbol search samples/SkillFixture/SkillFixture.slnx \
  RefundAsync --kind method
```

```text
method   SkillFixture.Payments.PaymentService.RefundAsync(string)
method   SkillFixture.Payments.PaymentService.RefundAsync(
         SkillFixture.Payments.PaymentReceipt)
```

The qualified signatures make it clear which parameter types distinguish the overloads.

### Find every part of a partial type

```bash
roslyn symbol search samples/SkillFixture/SkillFixture.slnx \
  PaymentService --kind type
```

```text
type     SkillFixture.Payments.PaymentService  .../PaymentService.Metadata.cs:3:29
type     SkillFixture.Payments.PaymentService  .../PaymentService.cs:5:29
```

Roslyn CLI reports both source declarations instead of treating the partial class as unrelated text matches.

### Separate events, properties, and fields

Use `--kind` when a shared name appears on different symbol categories:

```bash
roslyn symbol search samples/SkillFixture/SkillFixture.slnx \
  PaymentCompleted --kind event

roslyn symbol search samples/SkillFixture/SkillFixture.slnx \
  ProcessorName --kind property
```

Supported kinds are `type`, `method`, `property`, `field`, and `event`.

### Feed structured evidence to AI agents

JSON output is stable, versioned, and suitable for Codex or other automation:

```bash
roslyn symbol search samples/SkillFixture/SkillFixture.slnx \
  ProcessPaymentAsync --kind method --format json
```

```json
{
  "schemaVersion": "1.0",
  "count": 1,
  "results": [
    {
      "name": "ProcessPaymentAsync",
      "qualifiedName": "SkillFixture.Payments.PaymentService.ProcessPaymentAsync(SkillFixture.Domain.Order, System.Threading.CancellationToken)",
      "kind": "method",
      "project": "SkillFixture",
      "file": "/repo/samples/SkillFixture/src/SkillFixture/Payments/PaymentService.cs",
      "line": 9,
      "column": 39
    }
  ]
}
```

An agent can use this result to read only the relevant file, cite the exact declaration, and avoid guessing from text-search results.

### Search a project or a whole solution

Use a project for a narrow search or a solution to search across project boundaries:

```bash
roslyn symbol search src/MyLibrary/MyLibrary.csproj OrderService
roslyn symbol search MyProduct.sln OrderService --limit 20
```

Typical development uses include onboarding to an unfamiliar codebase, locating APIs before refactoring, distinguishing overloads, finding partial declarations, and giving AI tools precise semantic context.

## Build and test

Run the complete cross-platform verification workflow:

```bash
python scripts/test-all.py
```

This command enforces 100% line and branch coverage, validates both skill packages, creates the NuGet tool package, installs it into an isolated temporary directory, and runs every case in `samples/SkillFixture/skill-cases.json`.

Run the .NET test suite directly when you only need code tests and coverage:

```bash
dotnet build RoslynCli.slnx
dotnet test RoslynCli.slnx
```

## Run

```bash
dotnet run --project src/RoslynCli -- \
  symbol search RoslynCli.slnx SymbolSearch --format json
```

Filter results by declaration kind:

```bash
dotnet run --project src/RoslynCli -- \
  symbol search RoslynCli.slnx Search --kind method --limit 20
```

JSON output uses a versioned envelope so skills and other automation can consume it safely.

## Install as a local tool

```bash
dotnet pack src/RoslynCli
dotnet tool install --global RoslynCli.Tool \
  --add-source src/RoslynCli/bin/Release
```

With the .NET 10 SDK, the same tool package supports one-shot execution without installation:

```bash
dnx RoslynCli.Tool@0.1.0 --source src/RoslynCli/bin/Release -- \
  symbol search RoslynCli.slnx SymbolSearch --format json
```

## Use with Codex

The repository-scoped skill lives at `.agents/skills/roslyn-investigate`, a path Codex discovers automatically when working in this checkout. Invoke it explicitly with `$roslyn-investigate`, or ask a matching question such as “Where is `ProcessPaymentAsync` declared?” for implicit activation.

For use in other repositories, install the bundled plugin:

```bash
codex plugin marketplace add dhhieu113pro/roslyn-cli
codex plugin add roslyn-cli@roslyn-cli
```

The plugin includes the same skill under `plugins/roslyn-cli`. CI verifies that the repository and plugin copies remain identical.

## Skill fixture

`samples/SkillFixture` is a standalone C# solution for exercising semantic skill workflows. For example:

```bash
roslyn symbol search samples/SkillFixture/SkillFixture.slnx \
  ProcessPaymentAsync --kind method --format json
```

The fixture includes types, methods, overloads, partial declarations, properties, fields, events, and a no-result case. Run its complete black-box suite through the packaged tool with `python scripts/test-all.py`.
