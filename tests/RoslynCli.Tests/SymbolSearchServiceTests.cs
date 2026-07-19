using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RoslynCli.Tests;

public sealed class SymbolSearchServiceTests
{
    [Theory]
    [InlineData("Shop.*Service")]
    [InlineData("Shop.?rderService")]
    public async Task SearchSolutionAsync_SupportsWildcardPatterns(string pattern)
    {
        using var workspace = new AdhocWorkspace();
        var project = CreateProject(workspace, "Demo", "namespace Shop; public class OrderService { }");
        var results = await SymbolSearchService.SearchSolutionAsync(project.Solution, pattern, "type", 10);
        Assert.Single(results);
        Assert.Equal("OrderService", results[0].Name);
    }

    [Fact]
    public async Task SearchSolutionAsync_FindsTypesAndMembersSemantically()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Shop", LanguageNames.CSharp)
            .WithMetadataReferences([MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        project = project.AddDocument(
            "Orders.cs",
            "namespace Shop; public class OrderService { public void ProcessOrder() { } }")
            .Project;

        var results = await SymbolSearchService.SearchSolutionAsync(
            project.Solution,
            "order",
            kind: null,
            limit: 10);

        Assert.Collection(
            results,
            result => Assert.Equal("Shop.OrderService", result.QualifiedName),
            result => Assert.Equal("Shop.OrderService.ProcessOrder()", result.QualifiedName));
    }

    [Fact]
    public async Task SearchSolutionAsync_FiltersByKind()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Shop", LanguageNames.CSharp)
            .WithMetadataReferences([MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        project = project.AddDocument(
            "Orders.cs",
            "public class Order { public int OrderCount { get; init; } }")
            .Project;

        var results = await SymbolSearchService.SearchSolutionAsync(
            project.Solution,
            "order",
            kind: "property",
            limit: 10);

        var result = Assert.Single(results);
        Assert.Equal("property", result.Kind);
        Assert.Equal("Order.OrderCount", result.QualifiedName);
    }

    [Fact]
    public async Task SearchSolutionAsync_FindsEverySupportedKindAndNestedDeclarations()
    {
        using var workspace = new AdhocWorkspace();
        var project = CreateProject(workspace, "Kinds", """
            namespace Outer.Inner;
            public class MatchType
            {
                public int MatchField;
                public int MatchProperty { get; set; }
                public event System.Action? MatchEvent;
                public void MatchMethod() { }
                public class MatchNested { }
            }
            """);

        var results = await SymbolSearchService.SearchSolutionAsync(
            project.Solution, "MATCH", null, 20);

        Assert.Contains(results, result => result.Kind == "type" && result.Name == "MatchType");
        Assert.Contains(results, result => result.Kind == "type" && result.Name == "MatchNested");
        Assert.Contains(results, result => result.Kind == "field" && result.Name == "MatchField");
        Assert.Contains(results, result => result.Kind == "property" && result.Name == "MatchProperty");
        Assert.Contains(results, result => result.Kind == "event" && result.Name == "MatchEvent");
        Assert.Contains(results, result => result.Kind == "method" && result.Name == "MatchMethod");
    }

    [Fact]
    public async Task SearchSolutionAsync_ReturnsEachPartialDeclarationLocation()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Partial", LanguageNames.CSharp)
            .WithMetadataReferences([MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        project = project.AddDocument("One.cs", "public partial class SharedPart { }").Project;
        project = project.AddDocument("Two.cs", "public partial class SharedPart { }").Project;

        var results = await SymbolSearchService.SearchSolutionAsync(
            project.Solution, "SharedPart", "TYPE", 10);

        Assert.Equal(2, results.Count);
        Assert.Equal(["One.cs", "Two.cs"], results.Select(result => result.File));
    }

    [Fact]
    public async Task SearchSolutionAsync_OrdersProjectsAndHonorsLimit()
    {
        using var workspace = new AdhocWorkspace();
        var second = CreateProject(workspace, "Zulu", "public class MatchZulu { }");
        var firstId = ProjectId.CreateNewId();
        var solution = second.Solution
            .AddProject(firstId, "Alpha", "Alpha", LanguageNames.CSharp)
            .AddMetadataReference(firstId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddDocument(DocumentId.CreateNewId(firstId), "Alpha.cs", "public class MatchAlpha { }");

        var results = await SymbolSearchService.SearchSolutionAsync(
            solution, "Match", "type", 1);

        var result = Assert.Single(results);
        Assert.Equal("Alpha", result.Project);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("namespace")]
    [InlineData("banana")]
    public async Task SearchSolutionAsync_RejectsInvalidKind(string kind)
    {
        using var workspace = new AdhocWorkspace();
        var project = CreateProject(workspace, "Demo", "public class Demo { }");

        if (string.IsNullOrWhiteSpace(kind))
        {
            var results = await SymbolSearchService.SearchSolutionAsync(
                project.Solution, "Demo", kind, 10);
            Assert.Single(results);
        }
        else
        {
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                SymbolSearchService.SearchSolutionAsync(project.Solution, "Demo", kind, 10));
            Assert.Contains("Kind must be", exception.Message);
        }
    }

    [Fact]
    public async Task SearchSolutionAsync_RejectsInvalidLimit()
    {
        using var workspace = new AdhocWorkspace();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            SymbolSearchService.SearchSolutionAsync(workspace.CurrentSolution, "Demo", null, 0));
    }

    [Fact]
    public async Task SearchPathAsync_RejectsEmptyPatternBeforeLoading()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            SymbolSearchService.SearchPathAsync("demo.csproj", " ", null, 10));

        Assert.Contains("cannot be empty", exception.Message);
    }

    [Fact]
    public async Task SearchPathAsync_RejectsUnsupportedFileType()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            SymbolSearchService.SearchPathAsync("demo.txt", "Demo", null, 10));

        Assert.Contains(".slnx", exception.Message);
    }

    [Theory]
    [InlineData("src/RoslynCli/RoslynCli.csproj", "OutputWriter", "RoslynCli.OutputWriter")]
    [InlineData("RoslynCli.slnx", "OutputWriter", "RoslynCli.OutputWriter")]
    [InlineData("tests/RoslynCli.Tests/Fixtures/Classic/Classic.sln", "FixtureWorker", "Fixture.FixtureWorker")]
    public async Task SearchPathAsync_LoadsSupportedWorkspaceFiles(
        string relativePath,
        string pattern,
        string qualifiedName)
    {
        var path = Path.Combine(GetRepositoryRoot(), relativePath);

        var results = await SymbolSearchService.SearchPathAsync(
            path, pattern, "type", 10);

        Assert.Contains(results, result => result.QualifiedName == qualifiedName);
    }

    private static Project CreateProject(AdhocWorkspace workspace, string name, string source)
    {
        var project = workspace.AddProject(name, LanguageNames.CSharp)
            .WithMetadataReferences([
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Action).Assembly.Location)
            ]);
        return project.AddDocument($"{name}.cs", source).Project;
    }

    private static string GetRepositoryRoot() =>
        Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
}
