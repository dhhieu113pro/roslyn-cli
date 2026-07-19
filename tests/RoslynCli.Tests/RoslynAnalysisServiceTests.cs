namespace RoslynCli.Tests;

public sealed class RoslynAnalysisServiceTests
{
    private static readonly string Root = GetRepositoryRoot();
    private static readonly string Solution = Path.Combine(Root, "samples", "SkillFixture", "SkillFixture.slnx");
    private static readonly string PaymentService = Path.Combine(Root, "samples", "SkillFixture", "src", "SkillFixture", "Payments", "PaymentService.cs");
    private static readonly string Project = Path.Combine(Root, "samples", "SkillFixture", "src", "SkillFixture", "SkillFixture.csproj");
    private static readonly string ClassicSolution = Path.Combine(Root, "tests", "RoslynCli.Tests", "Fixtures", "Classic", "Classic.sln");

    [Fact]
    public async Task NavigationAndAnalysis_WorkAgainstFixture()
    {
        var references = await RoslynAnalysisService.FindReferencesPathAsync(Solution, "ProcessPaymentAsync", true);
        var info = await RoslynAnalysisService.GetSymbolInfoPathAsync(Solution, "ProcessPaymentAsync");
        var dependencies = await RoslynAnalysisService.AnalyzeDependenciesPathAsync(Solution);
        var complexity = await RoslynAnalysisService.AnalyzeComplexityPathAsync(Solution, 1, 20);
        var usages = await RoslynAnalysisService.FindUsagesAtPositionAsync(Solution, PaymentService, 9, 39, true);
        var diagnostics = await RoslynAnalysisService.ValidateFilePathAsync(Solution, PaymentService, false);
        var analyzerDiagnostics = await RoslynAnalysisService.ValidateFilePathAsync(Solution, PaymentService, true);
        var withoutDefinitions = await RoslynAnalysisService.FindReferencesPathAsync(Solution, "ProcessPaymentAsync", false);
        var usagesWithoutDefinition = await RoslynAnalysisService.FindUsagesAtPositionAsync(Solution, PaymentService, 9, 39, false);
        var typeInfo = await RoslynAnalysisService.GetSymbolInfoPathAsync(Project, "PaymentService");
        var classicComplexity = await RoslynAnalysisService.AnalyzeComplexityPathAsync(ClassicSolution, 1, 5);
        var gatewayReferences = await RoslynAnalysisService.FindReferencesPathAsync(Solution, "IPaymentGateway", true);
        var attributedInfo = await RoslynAnalysisService.GetSymbolInfoPathAsync(Solution, "AttributedService");
        var qualifiedInfo = await RoslynAnalysisService.GetSymbolInfoPathAsync(Solution, "SkillFixture.Payments.PaymentService.ProcessPaymentAsync(SkillFixture.Domain.Order, System.Threading.CancellationToken)");
        Assert.Contains(references, result => result.IsDefinition);
        Assert.Contains(info, result => result.Name == "ProcessPaymentAsync");
        Assert.NotEmpty(dependencies.Dependencies);
        Assert.NotEmpty(dependencies.NamespaceUsages);
        Assert.NotEmpty(complexity);
        Assert.NotEmpty(usages);
        Assert.DoesNotContain(diagnostics, result => result.Severity == "error");
        Assert.DoesNotContain(analyzerDiagnostics, result => result.Severity == "error");
        Assert.DoesNotContain(withoutDefinitions, result => result.IsDefinition);
        Assert.DoesNotContain(usagesWithoutDefinition, result => result.IsDefinition);
        Assert.Contains(typeInfo, result => result.Kind == "type");
        Assert.Empty(classicComplexity);
        Assert.Contains(gatewayReferences, result => !result.IsDefinition);
        Assert.Contains(attributedInfo, result => result.Attributes.Contains("System.ObsoleteAttribute") && result.Documentation is not null);
        Assert.Single(qualifiedInfo);
    }

    [Fact]
    public async Task ValidateFile_ReturnsAndFiltersCompilerDiagnostics()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"roslyn-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var project = Path.Combine(directory, "Broken.csproj");
            var good = Path.Combine(directory, "Good.cs");
            var bad = Path.Combine(directory, "Bad.cs");
            await File.WriteAllTextAsync(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup></Project>");
            await File.WriteAllTextAsync(good, "public sealed class Good;");
            await File.WriteAllTextAsync(bad, "public sealed class Bad { MissingType Value; }");
            Assert.Empty(await RoslynAnalysisService.ValidateFilePathAsync(project, good, false));
            Assert.Contains(await RoslynAnalysisService.ValidateFilePathAsync(project, bad, false), result => result.Id == "CS0246");
            var emptyProject = Path.Combine(directory, "Empty.csproj");
            await File.WriteAllTextAsync(emptyProject, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup></Project>");
            await Assert.ThrowsAsync<ArgumentException>(() => RoslynAnalysisService.FindUsagesAtPositionAsync(emptyProject, good, 1, 1, true));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AnalysisValidation_ReportsBadInputs()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => RoslynAnalysisService.GetSymbolInfoPathAsync(Solution, "MissingThing"));
        await Assert.ThrowsAsync<ArgumentException>(() => RoslynAnalysisService.GetSymbolInfoPathAsync(Solution, " "));
        await Assert.ThrowsAsync<ArgumentException>(() => RoslynAnalysisService.FindUsagesAtPositionAsync(Solution, PaymentService, 0, 1, true));
        await Assert.ThrowsAsync<ArgumentException>(() => RoslynAnalysisService.FindUsagesAtPositionAsync(Solution, PaymentService, 999, 1, true));
        await Assert.ThrowsAsync<ArgumentException>(() => RoslynAnalysisService.FindUsagesAtPositionAsync(Solution, PaymentService, 2, 1, true));
        Assert.Contains(await RoslynAnalysisService.FindUsagesAtPositionAsync(Solution, PaymentService, 5, 44, false), result => !result.IsDefinition);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RoslynAnalysisService.FindUsagesAtPositionAsync(Solution, PaymentService, 5, 44, true, cancellation.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => RoslynAnalysisService.ValidateFilePathAsync(Solution, "missing.cs", false));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => RoslynAnalysisService.AnalyzeComplexityPathAsync(Solution, 0, 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => RoslynAnalysisService.AnalyzeComplexityPathAsync(Solution, 1, 0));
        await Assert.ThrowsAsync<ArgumentException>(() => RoslynAnalysisService.GetSymbolInfoPathAsync("invalid.txt", "Anything"));
    }

    [Fact]
    public async Task ExtendedTools_RunQueriesAndDiagnostics()
    {
        var search = await ExtendedToolService.ExecuteAsync(Solution, "search-symbols", "{\"query\":\"PaymentService\",\"kindFilter\":\"Class\",\"maxResults\":10}");
        var diagnosedWorkspace = await ExtendedToolService.ExecuteAsync(Solution, "diagnose", "{}");
        var diagnosedEnvironment = await ExtendedToolService.ExecuteAsync("", "DIAGNOSE", "{}");
        Assert.Contains("PaymentService", search);
        Assert.Contains("\"healthy\"", diagnosedWorkspace);
        Assert.Contains("\"workspace\":null", diagnosedEnvironment);
        await Assert.ThrowsAsync<ArgumentException>(() => ExtendedToolService.ExecuteAsync(Solution, "missing", "{}"));
    }

    private static string GetRepositoryRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
}
