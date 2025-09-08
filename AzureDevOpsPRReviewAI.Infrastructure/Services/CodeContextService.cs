namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;
    using Microsoft.Extensions.Logging;

    public class CodeContextService : ICodeContextService
    {
        private readonly ILogger<CodeContextService> logger;
        private readonly IFileRetrievalService fileRetrievalService;
        private readonly IRepositoryService repositoryService;
        private readonly ICodeChunkingService codeChunkingService;
        private readonly IDependencyAnalysisService dependencyAnalysisService;
        private readonly ISemanticSearchService semanticSearchService;
        private readonly IRepositoryQueryStrategyService queryStrategyService;

        public CodeContextService(
            ILogger<CodeContextService> logger,
            IFileRetrievalService fileRetrievalService,
            IRepositoryService repositoryService,
            ICodeChunkingService codeChunkingService,
            IDependencyAnalysisService dependencyAnalysisService,
            ISemanticSearchService semanticSearchService,
            IRepositoryQueryStrategyService queryStrategyService)
        {
            this.logger = logger;
            this.fileRetrievalService = fileRetrievalService;
            this.repositoryService = repositoryService;
            this.codeChunkingService = codeChunkingService;
            this.dependencyAnalysisService = dependencyAnalysisService;
            this.semanticSearchService = semanticSearchService;
            this.queryStrategyService = queryStrategyService;
        }

        public async Task<List<CodeChunk>> ChunkCodeAsync(string filePath, string content)
        {
            return await this.codeChunkingService.ChunkCodeAsync(filePath, content);
        }

        public async Task<List<CodeChunk>> GetRelevantContextAsync(string repositoryPath, string query, int maxTokens = 100000)
        {
            // Use default DirectFallback strategy for backward compatibility
            var defaultQuerySettings = new QuerySettings
            {
                Strategy = QueryStrategy.DirectFallback,
                EnableDirectFileAccess = true,
                EnableVectorSearch = true
            };
            return await this.GetRelevantContextAsync(repositoryPath, query, defaultQuerySettings, maxTokens);
        }

        public async Task<List<CodeChunk>> GetRelevantContextAsync(string repositoryPath, string query, QuerySettings querySettings, int maxTokens = 100000)
        {
            return await this.queryStrategyService.GetRelevantContextAsync(repositoryPath, query, querySettings, maxTokens);
        }

        public async Task<ContextResult> BuildAnalysisContextAsync(string repositoryPath, PullRequestAnalysisRequest request)
        {
            try
            {
                var result = new ContextResult();
                var currentTokens = 0;

                this.logger.LogInformation(
                    "Building analysis context for PR in {RepositoryPath} with {FileCount} changed files",
                    repositoryPath,
                    request.ChangedFiles.Count);

                var diffResult = await this.repositoryService.GetPullRequestDiffAsync(
                    repositoryPath,
                    request.SourceBranch,
                    request.TargetBranch);

                if (!diffResult.IsSuccessful)
                {
                    result.TruncationReason = $"Failed to generate diff: {diffResult.ErrorMessage}";
                    return result;
                }

                result.PrimaryDiff = diffResult;

                foreach (var filePath in request.ChangedFiles.Take(20))
                {
                    if (currentTokens >= request.MaxContextTokens * 0.6)
                    {
                        break;
                    }

                    var fileContent = await this.fileRetrievalService.GetFileAsync(repositoryPath, filePath);
                    if (fileContent?.Content != null && !fileContent.IsBinary)
                    {
                        var chunks = await this.codeChunkingService.ChunkCodeAsync(filePath, fileContent.Content);

                        foreach (var chunk in chunks)
                        {
                            chunk.TokenCount = await this.codeChunkingService.CountTokensAsync(chunk.Content);

                            if (currentTokens + chunk.TokenCount <= request.MaxContextTokens * 0.6)
                            {
                                result.RelevantChunks.Add(chunk);
                                currentTokens += chunk.TokenCount;
                            }
                        }

                        result.RelevantFiles.Add(fileContent);
                    }
                }

                var remainingTokens = request.MaxContextTokens - currentTokens;
                if (remainingTokens > 0)
                {
                    var relatedFiles = await this.dependencyAnalysisService.FindRelatedFilesAsync(repositoryPath, request.ChangedFiles);

                    foreach (var relatedFile in relatedFiles.Take(10))
                    {
                        if (currentTokens >= request.MaxContextTokens)
                        {
                            break;
                        }

                        if (!result.RelevantFiles.Any(f => f.FilePath == relatedFile))
                        {
                            var fileContent = await this.fileRetrievalService.GetFileAsync(repositoryPath, relatedFile);
                            if (fileContent?.Content != null && !fileContent.IsBinary)
                            {
                                var chunks = await this.codeChunkingService.ChunkCodeAsync(relatedFile, fileContent.Content);
                                var importantChunks = chunks
                                    .Where(c => c.ChunkType == CodeChunkType.Class || 
                                               c.ChunkType == CodeChunkType.Interface ||
                                               c.ChunkType == CodeChunkType.Method)
                                    .Take(5)
                                    .ToList();

                                foreach (var chunk in importantChunks)
                                {
                                    chunk.TokenCount = await this.codeChunkingService.CountTokensAsync(chunk.Content);

                                    if (currentTokens + chunk.TokenCount <= request.MaxContextTokens)
                                    {
                                        result.RelevantChunks.Add(chunk);
                                        currentTokens += chunk.TokenCount;
                                    }
                                }

                                result.RelevantFiles.Add(fileContent);
                                result.RelatedFiles.Add(relatedFile);
                            }
                        }
                    }
                }

                result.TotalTokens = currentTokens;
                result.IsContextTruncated = currentTokens >= request.MaxContextTokens * 0.95;

                if (result.IsContextTruncated)
                {
                    result.TruncationReason = $"Context truncated to stay within {request.MaxContextTokens} token limit";
                }

                this.logger.LogInformation(
                    "Built analysis context: {ChunkCount} chunks, {FileCount} files, {TokenCount} tokens, Truncated: {IsTruncated}",
                    result.RelevantChunks.Count,
                    result.RelevantFiles.Count,
                    result.TotalTokens,
                    result.IsContextTruncated);

                return result;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to build analysis context");
                return new ContextResult
                {
                    TruncationReason = $"Error building context: {ex.Message}",
                };
            }
        }

        public async Task<int> CountTokensAsync(string text)
        {
            return await this.codeChunkingService.CountTokensAsync(text);
        }

        public async Task<List<string>> AnalyzeFileDependenciesAsync(string repositoryPath, string filePath)
        {
            return await this.dependencyAnalysisService.AnalyzeFileDependenciesAsync(repositoryPath, filePath);
        }

        public async Task<SemanticSearchResult> SearchSimilarCodeAsync(string repositoryPath, string codeSnippet, int topResults = 10)
        {
            // Use default DirectFallback strategy for backward compatibility
            var defaultQuerySettings = new QuerySettings
            {
                Strategy = QueryStrategy.DirectFallback,
                EnableDirectFileAccess = true,
                EnableVectorSearch = true
            };
            return await this.queryStrategyService.SearchSimilarCodeAsync(repositoryPath, codeSnippet, defaultQuerySettings, topResults);
        }

        public async Task IndexRepositoryAsync(string repositoryPath)
        {
            // Use default settings that enable vector search
            var defaultQuerySettings = new QuerySettings
            {
                Strategy = QueryStrategy.DirectFallback,
                EnableDirectFileAccess = true,
                EnableVectorSearch = true
            };
            await this.queryStrategyService.IndexRepositoryAsync(repositoryPath, defaultQuerySettings);
        }

        public async Task UpdateIndexAsync(string repositoryPath, IEnumerable<string> changedFiles)
        {
            // Use default settings that enable vector search
            var defaultQuerySettings = new QuerySettings
            {
                Strategy = QueryStrategy.DirectFallback,
                EnableDirectFileAccess = true,
                EnableVectorSearch = true
            };
            await this.queryStrategyService.UpdateIndexAsync(repositoryPath, changedFiles, defaultQuerySettings);
        }
    }
}