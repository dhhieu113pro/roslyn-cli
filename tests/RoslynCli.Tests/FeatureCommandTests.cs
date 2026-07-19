using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace RoslynCli.Tests;

public sealed class FeatureCommandTests
{
    [Fact]
    public async Task FeatureCommands_InvokeServicesAndWriteResults()
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        FindReferences references = (_, _, include, _) => Task.FromResult<IReadOnlyList<SymbolReferenceResult>>([new("Demo.C", "Demo", "C.cs", 1, 2, include)]);
        GetSymbolInfo info = (_, _, _) => Task.FromResult<IReadOnlyList<SymbolInfoResult>>([new("C", "Demo.C", "type", "public", "Demo", null, null, [], [], null, "C.cs", 1, 1)]);
        AnalyzeDependencies dependencies = (_, _) => Task.FromResult(new DependencyAnalysisResult([new("Demo", "System", "assembly")], [new("Demo", "System", 1)]));
        AnalyzeComplexity complexity = (_, threshold, _, _) => Task.FromResult<IReadOnlyList<ComplexityResult>>([new("Demo", "Demo.C.M()", "C.cs", 2, threshold)]);
        FindUsages usages = (_, _, line, column, include, _) => Task.FromResult<IReadOnlyList<SymbolReferenceResult>>([new("Demo.C", "Demo", "C.cs", line, column, include)]);
        ValidateFile validate = (_, _, analyzers, _) => Task.FromResult<IReadOnlyList<DiagnosticResult>>([new("CS0001", "warning", analyzers.ToString(), "C.cs", 1, 1, "compiler")]);
        RunExtendedTool tool = (_, operation, json, _) => Task.FromResult($"{{\"success\":true,\"operation\":\"{operation}\",\"params\":{json}}}");

        Assert.Equal(0, await Invoke(["symbol", "references", "demo.sln", "C", "--exclude-definition"], references: references));
        Assert.Equal(0, await Invoke(["symbol", "info", "demo.sln", "C", "--format", "json"], info: info));
        Assert.Equal(0, await Invoke(["analyze", "dependencies", "demo.sln"], dependencies: dependencies));
        Assert.Equal(0, await Invoke(["analyze", "complexity", "demo.sln", "--threshold", "7", "--limit", "2", "--format", "json"], complexity: complexity));
        Assert.Equal(0, await Invoke(["symbol", "usages", "demo.sln", "C.cs", "--line", "3", "--column", "4", "--exclude-definition"], usages: usages));
        Assert.Equal(0, await Invoke(["analyze", "validate", "demo.sln", "C.cs", "--no-analyzers", "--format", "json"], validate: validate));
        Assert.Equal(0, await Invoke(["tool", "run", "demo.sln", "rename-symbol", "--params", "{\"preview\":true}"], tool: tool));
        Assert.Contains("rename-symbol", output.ToString());
        Assert.Empty(error.ToString());

        async Task<int> Invoke(string[] args, FindReferences? references = null, GetSymbolInfo? info = null,
            AnalyzeDependencies? dependencies = null, AnalyzeComplexity? complexity = null,
            FindUsages? usages = null, ValidateFile? validate = null, RunExtendedTool? tool = null) =>
            await RoslynCliApp.InvokeAsync(args, output, error, references: references, info: info,
                dependencies: dependencies, complexity: complexity, usages: usages, validate: validate, runTool: tool);
    }

    [Fact]
    public async Task ToolList_ListsCompleteOperationSet()
    {
        using var output = new StringWriter();
        var exitCode = await RoslynCliApp.InvokeAsync(["tool", "list"], output);
        Assert.Equal(0, exitCode);
        Assert.Equal(41, output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("diagnose", output.ToString());
        Assert.Contains("rename-symbol", output.ToString());
    }

    [Fact]
    public void Readme_DocumentsEveryExtendedOperation()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var documented = ParseDocumented(readme.ReplaceLineEndings("\n"));
        Assert.Equal(documented, ParseDocumented(readme.ReplaceLineEndings("\r\n")));
        Assert.Equal(ExtendedToolService.GetOperationNames().Order(), documented.Order());
        Assert.Equal(documented.Length, documented.Distinct().Count());

        static string[] ParseDocumented(string content) =>
            Regex.Matches(content, @"(?m)^# \d+\. ([a-z0-9-]+)\r?$")
                .Select(match => match.Groups[1].Value)
                .ToArray();
    }

    [Theory]
    [InlineData("analyze", "complexity", "--threshold")]
    [InlineData("symbol", "usages", "--line")]
    public async Task PositiveOptions_AreValidated(string group, string command, string option)
    {
        using var error = new StringWriter();
        var args = group == "analyze"
            ? new[] { group, command, "demo.sln", option, "0" }
            : new[] { group, command, "demo.sln", "C.cs", option, "0", "--column", "1" };
        Assert.Equal(2, await RoslynCliApp.InvokeAsync(args, error: error));
        Assert.Contains("greater than zero", error.ToString());
    }

    [Fact]
    public void OutputWriter_WritesEveryFeatureAsTextAndJson()
    {
        var references = new[] { new SymbolReferenceResult("Demo.C", "Demo", "C.cs", 1, 2, true) };
        var info = new[] { new SymbolInfoResult("C", "Demo.C", "type", "public", "Demo", null, null, [], [], null, "C.cs", 1, 1) };
        var dependencies = new DependencyAnalysisResult([new("Demo", "System", "assembly")], [new("Demo", "System", 2)]);
        var complexity = new[] { new ComplexityResult("Demo", "Demo.C.M()", "C.cs", 2, 4) };
        var diagnostics = new[] { new DiagnosticResult("CS1", "error", "bad", "C.cs", 2, 3, "compiler") };
        foreach (var format in new[] { "text", "json" })
        {
            using var writer = new StringWriter();
            OutputWriter.WriteReferences(references, format, writer);
            OutputWriter.WriteSymbolInfo(info, format, writer);
            OutputWriter.WriteDependencies(dependencies, format, writer);
            OutputWriter.WriteComplexity(complexity, format, writer);
            OutputWriter.WriteDiagnostics(diagnostics, format, writer);
            Assert.NotEmpty(writer.ToString());
        }
        using var raw = new StringWriter();
        OutputWriter.WriteRawJson("{\"ok\":true}", "text", raw);
        Assert.Contains("\"ok\": true", raw.ToString());
    }

    [Fact]
    public void ComplexityCalculator_CountsDecisionKinds()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { void M(int x) { if (x > 0 && x < 3) x++; else x--; while (x > 0) x--; do x++; while (x < 0); for (;;) break; foreach (var y in new int[0]) { } try { } catch { } var z = x > 0 ? 1 : 2; int? n = x; var q = n ?? 0; switch (x) { case 1: break; } var s = x switch { 1 => 2, _ => 3 }; } }");
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        Assert.True(RoslynAnalysisService.CalculateCyclomaticComplexity(method) >= 12);
    }
}
