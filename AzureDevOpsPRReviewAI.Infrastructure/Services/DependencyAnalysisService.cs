namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.Extensions.Logging;
    using System.Text.RegularExpressions;

    public class DependencyAnalysisService : IDependencyAnalysisService
    {
        private readonly ILogger<DependencyAnalysisService> logger;
        private readonly IFileRetrievalService fileRetrievalService;
        private readonly IRepositoryService repositoryService;

        public DependencyAnalysisService(
            ILogger<DependencyAnalysisService> logger,
            IFileRetrievalService fileRetrievalService,
            IRepositoryService repositoryService)
        {
            this.logger = logger;
            this.fileRetrievalService = fileRetrievalService;
            this.repositoryService = repositoryService;
        }

        public async Task<List<string>> AnalyzeFileDependenciesAsync(string repositoryPath, string filePath)
        {
            try
            {
                var dependencies = new List<string>();

                var fileContent = await this.fileRetrievalService.GetFileAsync(repositoryPath, filePath);
                if (fileContent?.Content == null || fileContent.IsBinary)
                {
                    return dependencies;
                }

                var extension = Path.GetExtension(filePath);

                if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    dependencies = await this.AnalyzeCSharpDependenciesAsync(repositoryPath, fileContent.Content);
                }
                else
                {
                    dependencies = this.AnalyzeGenericDependencies(repositoryPath, fileContent.Content);
                }

                this.logger.LogDebug(
                    "Found {DependencyCount} dependencies for file {FilePath}",
                    dependencies.Count,
                    filePath);

                return dependencies;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to analyze dependencies for file {FilePath}", filePath);
                return new List<string>();
            }
        }

        public async Task<List<string>> FindRelatedFilesAsync(string repositoryPath, List<string> changedFiles)
        {
            var relatedFiles = new List<string>();

            foreach (var changedFile in changedFiles.Take(5))
            {
                var dependencies = await this.AnalyzeFileDependenciesAsync(repositoryPath, changedFile);
                relatedFiles.AddRange(dependencies);
            }

            return relatedFiles.Distinct().Where(f => !changedFiles.Contains(f)).ToList();
        }

        private async Task<List<string>> AnalyzeCSharpDependenciesAsync(string repositoryPath, string content)
        {
            var dependencies = new List<string>();

            try
            {
                var tree = CSharpSyntaxTree.ParseText(content);
                var root = await tree.GetRootAsync();

                var usingDirectives = root.DescendantNodes().OfType<UsingDirectiveSyntax>();
                foreach (var usingDir in usingDirectives)
                {
                    var namespaceName = usingDir.Name?.ToString();
                    if (!string.IsNullOrEmpty(namespaceName))
                    {
                        var possibleFiles = await this.FindFilesByNamespace(repositoryPath, namespaceName);
                        dependencies.AddRange(possibleFiles);
                    }
                }

                var identifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>();
                var typeNames = identifiers.Select(i => i.Identifier.ValueText).Distinct().ToList();

                foreach (var typeName in typeNames)
                {
                    var possibleFiles = await this.FindFilesByTypeName(repositoryPath, typeName);
                    dependencies.AddRange(possibleFiles);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to analyze C# dependencies");
            }

            return dependencies.Distinct().ToList();
        }

        private List<string> AnalyzeGenericDependencies(string repositoryPath, string content)
        {
            var dependencies = new List<string>();

            var importPatterns = new[]
            {
                @"import\s+.*?from\s+['""]([^'""]+)['""]",  // JavaScript/TypeScript
                @"#include\s+[<""]([^>""]+)[>""]",         // C/C++
                @"require\s*\(\s*['""]([^'""]+)['""]",     // JavaScript/Node.js
                @"using\s+([^\s;]+)",                      // C#
                @"import\s+([^\s;]+)",                     // Various languages
            };

            foreach (var pattern in importPatterns)
            {
                var matches = Regex.Matches(content, pattern);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var dependency = match.Groups[1].Value;
                        var potentialPath = dependency.Replace(".", "/") + ".cs";
                        if (File.Exists(Path.Combine(repositoryPath, potentialPath)))
                        {
                            dependencies.Add(potentialPath);
                        }
                    }
                }
            }

            return dependencies;
        }

        private async Task<List<string>> FindFilesByNamespace(string repositoryPath, string namespaceName)
        {
            var files = new List<string>();

            var allFiles = await this.repositoryService.GetAllFilesAsync(repositoryPath);
            var csFiles = allFiles.Where(f => f.EndsWith(".cs")).Take(20).ToList();

            foreach (var file in csFiles)
            {
                var content = await this.repositoryService.GetFileContentAsync(repositoryPath, file);
                if (content?.Contains($"namespace {namespaceName}") == true)
                {
                    files.Add(file);
                }
            }

            return files;
        }

        private async Task<List<string>> FindFilesByTypeName(string repositoryPath, string typeName)
        {
            var files = new List<string>();

            var allFiles = await this.repositoryService.GetAllFilesAsync(repositoryPath);
            var csFiles = allFiles.Where(f => f.EndsWith(".cs") && 
                                        f.Contains(typeName, StringComparison.OrdinalIgnoreCase))
                             .Take(5)
                             .ToList();

            return csFiles;
        }
    }
}