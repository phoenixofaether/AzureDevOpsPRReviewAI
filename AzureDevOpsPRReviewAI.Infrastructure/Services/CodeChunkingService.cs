namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.Extensions.Logging;

    public class CodeChunkingService : ICodeChunkingService
    {
        private readonly ILogger<CodeChunkingService> logger;
        private readonly ITokenizerService tokenizerService;

        public CodeChunkingService(
            ILogger<CodeChunkingService> logger,
            ITokenizerService tokenizerService)
        {
            this.logger = logger;
            this.tokenizerService = tokenizerService;
        }

        public async Task<List<CodeChunk>> ChunkCodeAsync(string filePath, string content)
        {
            try
            {
                var chunks = new List<CodeChunk>();
                var extension = Path.GetExtension(filePath);

                if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    chunks = await this.ChunkCSharpCodeAsync(filePath, content);
                }
                else
                {
                    chunks = this.ChunkGenericCodeAsync(filePath, content);
                }

                this.logger.LogDebug(
                    "Chunked file {FilePath} into {ChunkCount} chunks",
                    filePath,
                    chunks.Count);

                return chunks;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to chunk code for file {FilePath}", filePath);
                return this.ChunkGenericCodeAsync(filePath, content);
            }
        }

        public async Task<int> CountTokensAsync(string text)
        {
            return await this.tokenizerService.CountTokensAsync(text);
        }

        private async Task<List<CodeChunk>> ChunkCSharpCodeAsync(string filePath, string content)
        {
            var chunks = new List<CodeChunk>();

            try
            {
                var tree = CSharpSyntaxTree.ParseText(content);
                var root = await tree.GetRootAsync();

                var usingDirectives = root.DescendantNodes().OfType<UsingDirectiveSyntax>();
                if (usingDirectives.Any())
                {
                    var usingsText = string.Join("\n", usingDirectives.Select(u => u.ToString()));
                    chunks.Add(new CodeChunk
                    {
                        FilePath = filePath,
                        Content = usingsText,
                        StartLine = 1,
                        EndLine = usingDirectives.Count(),
                        ChunkType = CodeChunkType.UsingDirectives,
                    });
                }

                foreach (var namespaceDecl in root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
                {
                    chunks.Add(new CodeChunk
                    {
                        FilePath = filePath,
                        Content = namespaceDecl.ToString(),
                        StartLine = namespaceDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        EndLine = namespaceDecl.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                        ChunkType = CodeChunkType.Namespace,
                        Namespace = namespaceDecl.Name?.ToString(),
                    });
                }

                foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    chunks.Add(new CodeChunk
                    {
                        FilePath = filePath,
                        Content = classDecl.ToString(),
                        StartLine = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        EndLine = classDecl.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                        ChunkType = CodeChunkType.Class,
                        ClassName = classDecl.Identifier.ValueText,
                        Namespace = this.GetContainingNamespace(classDecl),
                    });
                }

                foreach (var interfaceDecl in root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
                {
                    chunks.Add(new CodeChunk
                    {
                        FilePath = filePath,
                        Content = interfaceDecl.ToString(),
                        StartLine = interfaceDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        EndLine = interfaceDecl.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                        ChunkType = CodeChunkType.Interface,
                        ClassName = interfaceDecl.Identifier.ValueText,
                        Namespace = this.GetContainingNamespace(interfaceDecl),
                    });
                }

                foreach (var methodDecl in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    chunks.Add(new CodeChunk
                    {
                        FilePath = filePath,
                        Content = methodDecl.ToString(),
                        StartLine = methodDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        EndLine = methodDecl.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                        ChunkType = CodeChunkType.Method,
                        MethodName = methodDecl.Identifier.ValueText,
                        ClassName = this.GetContainingClass(methodDecl)?.Identifier.ValueText,
                        Namespace = this.GetContainingNamespace(methodDecl),
                    });
                }

                foreach (var propertyDecl in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
                {
                    chunks.Add(new CodeChunk
                    {
                        FilePath = filePath,
                        Content = propertyDecl.ToString(),
                        StartLine = propertyDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        EndLine = propertyDecl.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                        ChunkType = CodeChunkType.Property,
                        MethodName = propertyDecl.Identifier.ValueText,
                        ClassName = this.GetContainingClass(propertyDecl)?.Identifier.ValueText,
                        Namespace = this.GetContainingNamespace(propertyDecl),
                    });
                }

                foreach (var enumDecl in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
                {
                    chunks.Add(new CodeChunk
                    {
                        FilePath = filePath,
                        Content = enumDecl.ToString(),
                        StartLine = enumDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        EndLine = enumDecl.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                        ChunkType = CodeChunkType.Enum,
                        ClassName = enumDecl.Identifier.ValueText,
                        Namespace = this.GetContainingNamespace(enumDecl),
                    });
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to parse C# syntax for file {FilePath}", filePath);
                chunks = this.ChunkGenericCodeAsync(filePath, content);
            }

            return chunks;
        }

        private List<CodeChunk> ChunkGenericCodeAsync(string filePath, string content)
        {
            var chunks = new List<CodeChunk>();
            var lines = content.Split('\n');

            const int LINES_PER_CHUNK = 50;

            for (var i = 0; i < lines.Length; i += LINES_PER_CHUNK)
            {
                var endIndex = Math.Min(i + LINES_PER_CHUNK, lines.Length);
                var chunkLines = lines[i..endIndex];

                chunks.Add(new CodeChunk
                {
                    FilePath = filePath,
                    Content = string.Join("\n", chunkLines),
                    StartLine = i + 1,
                    EndLine = endIndex,
                    ChunkType = CodeChunkType.Other,
                });
            }

            return chunks;
        }

        private string? GetContainingNamespace(SyntaxNode node)
        {
            var namespaceDecl = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
            return namespaceDecl?.Name?.ToString();
        }

        private ClassDeclarationSyntax? GetContainingClass(SyntaxNode node)
        {
            return node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        }
    }
}