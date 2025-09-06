namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;
    using Microsoft.Extensions.Logging;
    using System.Text.RegularExpressions;

    public class SemanticSearchService : ISemanticSearchService
    {
        private readonly ILogger<SemanticSearchService> logger;
        private readonly IFileRetrievalService fileRetrievalService;
        private readonly IRepositoryService repositoryService;
        private readonly ICodeChunkingService codeChunkingService;

        public SemanticSearchService(
            ILogger<SemanticSearchService> logger,
            IFileRetrievalService fileRetrievalService,
            IRepositoryService repositoryService,
            ICodeChunkingService codeChunkingService)
        {
            this.logger = logger;
            this.fileRetrievalService = fileRetrievalService;
            this.repositoryService = repositoryService;
            this.codeChunkingService = codeChunkingService;
        }

        public async Task<List<CodeChunk>> GetRelevantContextAsync(string repositoryPath, string query, int maxTokens = 100000)
        {
            try
            {
                var relevantChunks = new List<CodeChunk>();
                var currentTokens = 0;

                var allFiles = await this.repositoryService.GetAllFilesAsync(repositoryPath);
                var codeFiles = allFiles.Where(f => this.IsCodeFile(f)).Take(50).ToList();

                this.logger.LogInformation(
                    "Searching for relevant context in {FileCount} code files for query: {Query}",
                    codeFiles.Count,
                    query);

                foreach (var filePath in codeFiles)
                {
                    if (currentTokens >= maxTokens)
                    {
                        break;
                    }

                    var fileContent = await this.fileRetrievalService.GetFileAsync(repositoryPath, filePath);
                    if (fileContent?.Content == null || fileContent.IsBinary)
                    {
                        continue;
                    }

                    var chunks = await this.codeChunkingService.ChunkCodeAsync(filePath, fileContent.Content);

                    foreach (var chunk in chunks)
                    {
                        if (currentTokens >= maxTokens)
                        {
                            break;
                        }

                        chunk.RelevanceScore = this.CalculateRelevanceScore(chunk, query);

                        if (chunk.RelevanceScore > 0.3)
                        {
                            chunk.TokenCount = await this.codeChunkingService.CountTokensAsync(chunk.Content);

                            if (currentTokens + chunk.TokenCount <= maxTokens)
                            {
                                relevantChunks.Add(chunk);
                                currentTokens += chunk.TokenCount;
                            }
                        }
                    }
                }

                relevantChunks = relevantChunks
                    .OrderByDescending(c => c.RelevanceScore)
                    .Take(100)
                    .ToList();

                this.logger.LogInformation(
                    "Found {ChunkCount} relevant code chunks using {TokenCount} tokens",
                    relevantChunks.Count,
                    currentTokens);

                return relevantChunks;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get relevant context for query: {Query}", query);
                return new List<CodeChunk>();
            }
        }

        public async Task<SemanticSearchResult> SearchSimilarCodeAsync(string repositoryPath, string codeSnippet, int topResults = 10)
        {
            try
            {
                var result = new SemanticSearchResult
                {
                    Query = codeSnippet,
                    Results = new List<CodeChunk>(),
                };

                var keywords = this.ExtractKeywords(codeSnippet);
                var contextChunks = await this.GetRelevantContextAsync(repositoryPath, string.Join(" ", keywords), 50000);

                result.Results = contextChunks
                    .Take(topResults)
                    .ToList();

                result.TotalResults = contextChunks.Count;
                result.MaxScore = result.Results.Any() ? result.Results.Max(r => r.RelevanceScore) : 0;
                result.MinScore = result.Results.Any() ? result.Results.Min(r => r.RelevanceScore) : 0;

                this.logger.LogDebug(
                    "Semantic search returned {ResultCount} results for code snippet",
                    result.Results.Count);

                return result;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to perform semantic search");
                return new SemanticSearchResult { Query = codeSnippet };
            }
        }

        public async Task IndexRepositoryAsync(string repositoryPath)
        {
            await Task.Run(() =>
            {
                this.logger.LogInformation("Repository indexing not yet implemented for {RepositoryPath}", repositoryPath);
            });
        }

        public async Task UpdateIndexAsync(string repositoryPath, IEnumerable<string> changedFiles)
        {
            await Task.Run(() =>
            {
                this.logger.LogDebug(
                    "Index update not yet implemented for {FileCount} changed files in {RepositoryPath}",
                    changedFiles.Count(),
                    repositoryPath);
            });
        }

        private double CalculateRelevanceScore(CodeChunk chunk, string query)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(chunk.Content))
            {
                return 0.0;
            }

            var queryWords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var chunkWords = chunk.Content.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var matches = queryWords.Count(qw => chunkWords.Contains(qw));
            var score = (double)matches / queryWords.Length;

            if (chunk.ChunkType == CodeChunkType.Class || chunk.ChunkType == CodeChunkType.Method)
            {
                score *= 1.5;
            }

            return Math.Min(1.0, score);
        }

        private List<string> ExtractKeywords(string text)
        {
            var words = Regex.Matches(text, @"\b[A-Za-z_][A-Za-z0-9_]*\b")
                .Cast<Match>()
                .Select(m => m.Value)
                .Where(w => w.Length > 2)
                .Distinct()
                .ToList();

            return words;
        }

        private bool IsCodeFile(string filePath)
        {
            var codeExtensions = new[] { ".cs", ".js", ".ts", ".jsx", ".tsx", ".py", ".java", ".cpp", ".c", ".h", ".hpp" };
            return codeExtensions.Any(ext => filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }
    }
}