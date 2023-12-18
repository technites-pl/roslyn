using System.Xml.Linq;
using Basic.Reference.Assemblies;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Analyzer;

public static class MyAnalyzer
{
    static MyAnalyzer() => MSBuildLocator.RegisterDefaults();

    public static async Task Analyse(string slnPath)
    {
        var workspace = MSBuildWorkspace.Create();
        workspace.SkipUnrecognizedProjects = true;
        workspace.WorkspaceFailed += (_, args) => Console.WriteLine($"Workspace diagnostic: {args.Diagnostic.Message}");
        var solution = await workspace.OpenSolutionAsync(slnPath);

        foreach (var project in solution.Projects)
        {
            try
            {
                var compilation = await Compile(project);
                Console.WriteLine(compilation is null
                    ? $"Compilation failed for: {project.Name}"
                    : $"Compilation succeeded for: {project.Name}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.WriteLine($"Compilation failed for: {project.Name}");
            }
        }
    }

    private static async Task<Compilation?> Compile(Project project)
    {
        var targetFramework = XDocument.Load(project.FilePath!).Descendants("TargetFramework").First().Value;
        var references = targetFramework switch
        {
            "net6.0" => Net60.References.All,
            "net7.0" => Net70.References.All,
            _ => throw new ArgumentOutOfRangeException()
        };
        project = project.AddMetadataReferences(references);
        var compilation = await project.GetCompilationAsync();
        if (compilation is null)
        {
            Console.WriteLine($"Can not compile project: {project.Name}");
            return null;
        }

        var diagnostics = compilation.GetDiagnostics();
        foreach (var diagnostic in diagnostics.Where(d => d.Severity != DiagnosticSeverity.Hidden))
            Console.WriteLine($"Compilation diagnostic: [{diagnostic.Severity}] {diagnostic.GetMessage()}");
        return diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)
            ? null
            : compilation;
    }
}