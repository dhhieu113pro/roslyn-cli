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

The initial companion skill lives at `skills/roslyn-investigate` and should be distributed from this repository with the CLI.
