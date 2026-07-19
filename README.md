# Roslyn CLI

Semantic command-line tooling for C# repositories, paired with agent skills that use its structured output.

## Build and test

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

Supported kinds are `type`, `method`, `property`, `field`, and `event`. JSON output uses a versioned envelope so skills and other automation can consume it safely.

## Install as a local tool

```bash
dotnet pack src/RoslynCli
dotnet tool install --global RoslynCli.Tool \
  --add-source src/RoslynCli/bin/Release
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
