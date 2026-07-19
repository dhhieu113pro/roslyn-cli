# Roslyn CLI

Semantic command-line tooling for C# repositories: symbol navigation, diagnostics,
dependency and complexity analysis, plus a complete previewable refactoring suite.

## Commands

Focused commands cover common investigation workflows:

```bash
roslyn symbol search <workspace> '*Service' --kind type
roslyn symbol references <workspace> ProcessPaymentAsync
roslyn symbol usages <workspace> PaymentService.cs --line 9 --column 39
roslyn symbol info <workspace> ProcessPaymentAsync --format json
roslyn analyze validate <workspace> PaymentService.cs
roslyn analyze dependencies <workspace> --format json
roslyn analyze complexity <workspace> --threshold 7 --limit 20
```

The extended operation bridge exposes all 41 operations from the MIT-licensed
RoslynMcp.Core library, including navigation, metrics, control/data flow,
generation, conversions, using organization, formatting, and solution-wide
refactorings.

```bash
roslyn tool list
```

The examples below use these placeholders:

```bash
WS=/repo/MySolution.sln
FILE=/repo/src/OrderService.cs
```

### Navigation and analysis operations

```bash
# 1. diagnose
roslyn tool run "$WS" diagnose --params '{}'

# 2. find-references
roslyn tool run "$WS" find-references --params "{\"sourceFile\":\"$FILE\",\"symbolName\":\"ProcessOrder\",\"maxResults\":100}"

# 3. find-callers
roslyn tool run "$WS" find-callers --params "{\"sourceFile\":\"$FILE\",\"symbolName\":\"ProcessOrder\",\"maxResults\":100}"

# 4. find-implementations
roslyn tool run "$WS" find-implementations --params "{\"sourceFile\":\"$FILE\",\"symbolName\":\"IOrderService\",\"maxResults\":100}"

# 5. go-to-definition
roslyn tool run "$WS" go-to-definition --params "{\"sourceFile\":\"$FILE\",\"line\":20,\"column\":15}"

# 6. search-symbols
roslyn tool run "$WS" search-symbols --params '{"query":"Order","kindFilter":"Class","maxResults":100}'

# 7. get-diagnostics
roslyn tool run "$WS" get-diagnostics --params "{\"sourceFile\":\"$FILE\",\"severityFilter\":\"Warning\"}"

# 8. get-code-metrics
roslyn tool run "$WS" get-code-metrics --params "{\"sourceFile\":\"$FILE\",\"symbolName\":\"ProcessOrder\"}"

# 9. analyze-control-flow
roslyn tool run "$WS" analyze-control-flow --params "{\"sourceFile\":\"$FILE\",\"startLine\":20,\"endLine\":35}"

# 10. analyze-data-flow
roslyn tool run "$WS" analyze-data-flow --params "{\"sourceFile\":\"$FILE\",\"startLine\":20,\"endLine\":35}"

# 11. get-document-outline
roslyn tool run "$WS" get-document-outline --params "{\"sourceFile\":\"$FILE\"}"

# 12. get-symbol-info
roslyn tool run "$WS" get-symbol-info --params "{\"sourceFile\":\"$FILE\",\"symbolName\":\"ProcessOrder\"}"

# 13. get-type-hierarchy
roslyn tool run "$WS" get-type-hierarchy --params "{\"sourceFile\":\"$FILE\",\"symbolName\":\"OrderService\",\"direction\":\"Both\"}"
```

### Extract, move, and signature operations

All mutating examples request a preview. Review the returned changes, then set
`preview` to `false` to apply them.

```bash
# 14. extract-method
roslyn tool run "$WS" extract-method --params "{\"sourceFile\":\"$FILE\",\"startLine\":20,\"startColumn\":9,\"endLine\":25,\"endColumn\":30,\"methodName\":\"ValidateOrder\",\"preview\":true}"

# 15. extract-variable
roslyn tool run "$WS" extract-variable --params "{\"sourceFile\":\"$FILE\",\"startLine\":20,\"startColumn\":20,\"endLine\":20,\"endColumn\":40,\"variableName\":\"total\",\"preview\":true}"

# 16. extract-constant
roslyn tool run "$WS" extract-constant --params "{\"sourceFile\":\"$FILE\",\"startLine\":12,\"startColumn\":20,\"endLine\":12,\"endColumn\":23,\"constantName\":\"MaxRetries\",\"preview\":true}"

# 17. extract-interface
roslyn tool run "$WS" extract-interface --params "{\"sourceFile\":\"$FILE\",\"typeName\":\"OrderService\",\"interfaceName\":\"IOrderService\",\"members\":[\"ProcessOrder\"],\"preview\":true}"

# 18. extract-base-class
roslyn tool run "$WS" extract-base-class --params "{\"sourceFile\":\"$FILE\",\"typeName\":\"OrderService\",\"baseClassName\":\"OrderServiceBase\",\"members\":[\"ValidateOrder\"],\"preview\":true}"

# 19. introduce-parameter
roslyn tool run "$WS" introduce-parameter --params "{\"sourceFile\":\"$FILE\",\"variableName\":\"timeout\",\"line\":20,\"preview\":true}"

# 20. rename-symbol
roslyn tool run "$WS" rename-symbol --params "{\"sourceFile\":\"$FILE\",\"symbolName\":\"OrderService\",\"newName\":\"OrderProcessor\",\"preview\":true}"

# 21. inline-variable
roslyn tool run "$WS" inline-variable --params "{\"sourceFile\":\"$FILE\",\"variableName\":\"total\",\"line\":20,\"preview\":true}"

# 22. change-signature
roslyn tool run "$WS" change-signature --params "{\"sourceFile\":\"$FILE\",\"methodName\":\"ProcessOrder\",\"parameters\":[{\"name\":\"cancellationToken\",\"type\":\"CancellationToken\",\"newPosition\":1}],\"preview\":true}"

# 23. encapsulate-field
roslyn tool run "$WS" encapsulate-field --params "{\"sourceFile\":\"$FILE\",\"fieldName\":\"_status\",\"propertyName\":\"Status\",\"preview\":true}"

# 24. move-type-to-file
roslyn tool run "$WS" move-type-to-file --params "{\"sourceFile\":\"$FILE\",\"symbolName\":\"OrderService\",\"targetFile\":\"/repo/src/OrderProcessor.cs\",\"preview\":true}"

# 25. move-type-to-namespace
roslyn tool run "$WS" move-type-to-namespace --params "{\"sourceFile\":\"$FILE\",\"symbolName\":\"OrderService\",\"targetNamespace\":\"MyApp.Orders\",\"preview\":true}"
```

### Conversion operations

```bash
# 26. convert-to-async
roslyn tool run "$WS" convert-to-async --params "{\"sourceFile\":\"$FILE\",\"methodName\":\"ProcessOrder\",\"renameToAsync\":true,\"preview\":true}"

# 27. convert-expression-body
roslyn tool run "$WS" convert-expression-body --params "{\"sourceFile\":\"$FILE\",\"memberName\":\"OrderCount\",\"direction\":\"ToBlockBody\",\"preview\":true}"

# 28. convert-property
roslyn tool run "$WS" convert-property --params "{\"sourceFile\":\"$FILE\",\"propertyName\":\"Status\",\"direction\":\"ToFullProperty\",\"preview\":true}"

# 29. convert-foreach-linq
roslyn tool run "$WS" convert-foreach-linq --params "{\"sourceFile\":\"$FILE\",\"line\":30,\"preview\":true}"

# 30. convert-to-interpolated-string
roslyn tool run "$WS" convert-to-interpolated-string --params "{\"sourceFile\":\"$FILE\",\"line\":30,\"preview\":true}"

# 31. convert-to-pattern-matching
roslyn tool run "$WS" convert-to-pattern-matching --params "{\"sourceFile\":\"$FILE\",\"line\":30,\"preview\":true}"
```

### Generation, organization, and formatting operations

```bash
# 32. generate-constructor
roslyn tool run "$WS" generate-constructor --params "{\"sourceFile\":\"$FILE\",\"typeName\":\"OrderService\",\"members\":[\"_repository\"],\"addNullChecks\":true,\"preview\":true}"

# 33. generate-equals-hashcode
roslyn tool run "$WS" generate-equals-hashcode --params "{\"sourceFile\":\"$FILE\",\"typeName\":\"Order\",\"fields\":[\"Id\"],\"preview\":true}"

# 34. generate-overrides
roslyn tool run "$WS" generate-overrides --params "{\"sourceFile\":\"$FILE\",\"typeName\":\"OrderService\",\"members\":[\"ToString\"],\"preview\":true}"

# 35. generate-tostring
roslyn tool run "$WS" generate-tostring --params "{\"sourceFile\":\"$FILE\",\"typeName\":\"Order\",\"fields\":[\"Id\",\"Total\"],\"preview\":true}"

# 36. implement-interface
roslyn tool run "$WS" implement-interface --params "{\"sourceFile\":\"$FILE\",\"typeName\":\"OrderService\",\"interfaceName\":\"IOrderService\",\"preview\":true}"

# 37. add-null-checks
roslyn tool run "$WS" add-null-checks --params "{\"sourceFile\":\"$FILE\",\"methodName\":\"ProcessOrder\",\"style\":\"ThrowIfNull\",\"preview\":true}"

# 38. add-missing-usings
roslyn tool run "$WS" add-missing-usings --params "{\"sourceFile\":\"$FILE\",\"preview\":true}"

# 39. remove-unused-usings
roslyn tool run "$WS" remove-unused-usings --params "{\"sourceFile\":\"$FILE\",\"preview\":true}"

# 40. sort-usings
roslyn tool run "$WS" sort-usings --params "{\"sourceFile\":\"$FILE\",\"preview\":true}"

# 41. format-document
# Applies immediately; use a clean working tree.
roslyn tool run "$WS" format-document --params "{\"sourceFile\":\"$FILE\"}"
```

Extended operation parameter names follow the [RoslynMcpServer tool contracts](https://github.com/JoshuaRamirez/RoslynMcpServer#available-tools).

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
dnx RoslynCli.Tool@0.2.0 --source src/RoslynCli/bin/Release -- \
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
