namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using System.Diagnostics;

    public class RepositoryQueryStrategyService : IRepositoryQueryStrategyService
    {
        private readonly ILogger<RepositoryQueryStrategyService> logger;
        private readonly ISemanticSearchService semanticSearchService;
        private readonly IDirectRepositoryQueryService directQueryService;
        private readonly IMemoryCache cache;
        private readonly Dictionary<string, QueryMethodStatistics> statistics;

        public RepositoryQueryStrategyService(
            ILogger<RepositoryQueryStrategyService> logger,
            ISemanticSearchService semanticSearchService,
            IDirectRepositoryQueryService directQueryService,
            IMemoryCache cache)
        {
            this.logger = logger;
            this.semanticSearchService = semanticSearchService;
            this.directQueryService = directQueryService;
            this.cache = cache;
            this.statistics = new Dictionary<string, QueryMethodStatistics>();
        }

        public async Task<List<CodeChunk>> GetRelevantContextAsync(string repositoryPath, string query, QuerySettings querySettings, int maxTokens = 100000)
        {
            var stopwatch = Stopwatch.StartNew();
            var stats = this.GetOrCreateStatistics(repositoryPath);

            try
            {
                this.logger.LogDebug("Getting relevant context using strategy {Strategy} for query '{Query}' in repository {RepositoryPath}",
                    querySettings.Strategy, query, repositoryPath);

                List<CodeChunk> results;

                switch (querySettings.Strategy)
                {
                    case QueryStrategy.VectorOnly:
                        results = await this.GetVectorContextAsync(repositoryPath, query, maxTokens, stats, stopwatch);
                        break;

                    case QueryStrategy.DirectOnly:
                        results = await this.GetDirectContextAsync(repositoryPath, query, maxTokens, stats, stopwatch);
                        break;

                    case QueryStrategy.Hybrid:
                        results = await this.GetHybridContextAsync(repositoryPath, query, maxTokens, stats, stopwatch);
                        break;

                    case QueryStrategy.DirectFallback:
                    default:
                        results = await this.GetDirectFallbackContextAsync(repositoryPath, query, maxTokens, stats, stopwatch);
                        break;
                }

                this.UpdatePerformanceRecommendation(stats);
                return results;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get relevant context using strategy {Strategy}", querySettings.Strategy);
                stats.DirectSearchFailures++;
                return new List<CodeChunk>();
            }
        }

        public async Task<SemanticSearchResult> SearchSimilarCodeAsync(string repositoryPath, string codeSnippet, QuerySettings querySettings, int topResults = 10)
        {
            var stats = this.GetOrCreateStatistics(repositoryPath);

            try
            {
                this.logger.LogDebug("Searching similar code using strategy {Strategy} in repository {RepositoryPath}",
                    querySettings.Strategy, repositoryPath);

                switch (querySettings.Strategy)
                {
                    case QueryStrategy.VectorOnly:
                    case QueryStrategy.Hybrid:
                    case QueryStrategy.DirectFallback:
                        if (querySettings.EnableVectorSearch)
                        {
                            try
                            {
                                var stopwatch = Stopwatch.StartNew();
                                var result = await this.semanticSearchService.SearchSimilarCodeAsync(repositoryPath, codeSnippet, topResults);
                                stopwatch.Stop();

                                stats.VectorSearchCount++;
                                stats.AverageVectorSearchTime = this.UpdateAverageTime(stats.AverageVectorSearchTime, stats.VectorSearchCount, stopwatch.Elapsed);
                                
                                return result;
                            }
                            catch (Exception ex)
                            {
                                this.logger.LogWarning(ex, "Vector search failed, falling back to direct search");
                                stats.VectorSearchFailures++;
                                stats.FallbackToDirectCount++;
                            }
                        }

                        // Fallback to direct search
                        return await this.SearchSimilarCodeDirectAsync(repositoryPath, codeSnippet, topResults, stats);

                    case QueryStrategy.DirectOnly:
                    default:
                        return await this.SearchSimilarCodeDirectAsync(repositoryPath, codeSnippet, topResults, stats);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to search similar code using strategy {Strategy}", querySettings.Strategy);
                return new SemanticSearchResult { Query = codeSnippet };
            }
        }

        public Task<DirectFileReadResult> ReadFileAsync(string repositoryPath, string filePath, int? startLine = null, int? endLine = null, string? branch = null)
        {
            return this.directQueryService.ReadFileAsync(repositoryPath, filePath, startLine, endLine, branch);
        }

        public Task<DirectSearchResult> SearchFilesAsync(string repositoryPath, string pattern, DirectSearchOptions options)
        {
            return this.directQueryService.SearchFilesAsync(repositoryPath, pattern, options);
        }

        public Task<DirectFileSearchResult> FindFilesAsync(string repositoryPath, string namePattern, DirectFileSearchOptions options)
        {
            return this.directQueryService.FindFilesAsync(repositoryPath, namePattern, options);
        }

        public Task<DirectoryStructureResult> GetFileStructureAsync(string repositoryPath, string? subPath = null, int maxDepth = 3)
        {
            return this.directQueryService.GetFileStructureAsync(repositoryPath, subPath, maxDepth);
        }

        public async Task IndexRepositoryAsync(string repositoryPath, QuerySettings querySettings)
        {
            if (!querySettings.EnableVectorSearch)
            {
                this.logger.LogDebug("Vector search is disabled, skipping repository indexing for {RepositoryPath}", repositoryPath);
                return;
            }

            try
            {
                this.logger.LogInformation("Indexing repository {RepositoryPath} for vector search", repositoryPath);
                await this.semanticSearchService.IndexRepositoryAsync(repositoryPath);

                var stats = this.GetOrCreateStatistics(repositoryPath);
                stats.IsVectorIndexAvailable = true;
                stats.LastVectorIndexUpdate = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to index repository {RepositoryPath}", repositoryPath);
                var stats = this.GetOrCreateStatistics(repositoryPath);
                stats.IsVectorIndexAvailable = false;
            }
        }

        public async Task UpdateIndexAsync(string repositoryPath, IEnumerable<string> changedFiles, QuerySettings querySettings)
        {
            if (!querySettings.EnableVectorSearch)
            {
                this.logger.LogDebug("Vector search is disabled, skipping index update for {RepositoryPath}", repositoryPath);
                return;
            }

            try
            {
                this.logger.LogDebug("Updating repository index for {FileCount} changed files in {RepositoryPath}",
                    changedFiles.Count(), repositoryPath);
                await this.semanticSearchService.UpdateIndexAsync(repositoryPath, changedFiles);

                var stats = this.GetOrCreateStatistics(repositoryPath);
                stats.LastVectorIndexUpdate = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to update repository index for {RepositoryPath}", repositoryPath);
                var stats = this.GetOrCreateStatistics(repositoryPath);
                stats.IsVectorIndexAvailable = false;
            }
        }

        public Task<QueryMethodStatistics> GetQueryStatisticsAsync(string repositoryPath)
        {
            var stats = this.GetOrCreateStatistics(repositoryPath);
            return Task.FromResult(stats);
        }

        private async Task<List<CodeChunk>> GetVectorContextAsync(string repositoryPath, string query, int maxTokens, QueryMethodStatistics stats, Stopwatch stopwatch)
        {
            try
            {
                var result = await this.semanticSearchService.GetRelevantContextAsync(repositoryPath, query, maxTokens);
                stopwatch.Stop();

                stats.VectorSearchCount++;
                stats.AverageVectorSearchTime = this.UpdateAverageTime(stats.AverageVectorSearchTime, stats.VectorSearchCount, stopwatch.Elapsed);

                return result;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Vector search failed for repository {RepositoryPath}", repositoryPath);
                stats.VectorSearchFailures++;
                throw;
            }
        }

        private async Task<List<CodeChunk>> GetDirectContextAsync(string repositoryPath, string query, int maxTokens, QueryMethodStatistics stats, Stopwatch stopwatch)
        {
            try
            {
                var result = await this.directQueryService.GetRelevantContextAsync(repositoryPath, query, maxTokens);
                stopwatch.Stop();

                stats.DirectSearchCount++;
                stats.AverageDirectSearchTime = this.UpdateAverageTime(stats.AverageDirectSearchTime, stats.DirectSearchCount, stopwatch.Elapsed);

                return result;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Direct search failed for repository {RepositoryPath}", repositoryPath);
                stats.DirectSearchFailures++;
                throw;
            }
        }

        private async Task<List<CodeChunk>> GetHybridContextAsync(string repositoryPath, string query, int maxTokens, QueryMethodStatistics stats, Stopwatch stopwatch)
        {
            var vectorResults = new List<CodeChunk>();
            var directResults = new List<CodeChunk>();

            // Try both methods in parallel
            var vectorTask = Task.Run(async () =>
            {
                try
                {
                    if (stats.IsVectorIndexAvailable)
                    {
                        return await this.semanticSearchService.GetRelevantContextAsync(repositoryPath, query, maxTokens / 2);
                    }
                    return new List<CodeChunk>();
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Vector search failed in hybrid mode");
                    stats.VectorSearchFailures++;
                    return new List<CodeChunk>();
                }
            });

            var directTask = Task.Run(async () =>
            {
                try
                {
                    return await this.directQueryService.GetRelevantContextAsync(repositoryPath, query, maxTokens / 2);
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Direct search failed in hybrid mode");
                    stats.DirectSearchFailures++;
                    return new List<CodeChunk>();
                }
            });

            await Task.WhenAll(vectorTask, directTask);
            stopwatch.Stop();

            vectorResults = await vectorTask;
            directResults = await directTask;

            // Update statistics
            if (vectorResults.Any())
            {
                stats.VectorSearchCount++;
            }
            if (directResults.Any())
            {
                stats.DirectSearchCount++;
            }

            // Merge and deduplicate results
            var combinedResults = this.MergeAndDeduplicateResults(vectorResults, directResults, maxTokens);
            
            this.logger.LogDebug("Hybrid search completed: {VectorCount} vector results, {DirectCount} direct results, {CombinedCount} final results",
                vectorResults.Count, directResults.Count, combinedResults.Count);

            return combinedResults;
        }

        private async Task<List<CodeChunk>> GetDirectFallbackContextAsync(string repositoryPath, string query, int maxTokens, QueryMethodStatistics stats, Stopwatch stopwatch)
        {
            // Try vector search first
            if (stats.IsVectorIndexAvailable)
            {
                try
                {
                    var vectorResult = await this.semanticSearchService.GetRelevantContextAsync(repositoryPath, query, maxTokens);
                    stopwatch.Stop();

                    stats.VectorSearchCount++;
                    stats.AverageVectorSearchTime = this.UpdateAverageTime(stats.AverageVectorSearchTime, stats.VectorSearchCount, stopwatch.Elapsed);

                    if (vectorResult.Any())
                    {
                        return vectorResult;
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Vector search failed, falling back to direct search");
                    stats.VectorSearchFailures++;
                    stats.FallbackToDirectCount++;
                }
            }

            // Fallback to direct search
            var directResult = await this.directQueryService.GetRelevantContextAsync(repositoryPath, query, maxTokens);
            stopwatch.Stop();

            stats.DirectSearchCount++;
            stats.AverageDirectSearchTime = this.UpdateAverageTime(stats.AverageDirectSearchTime, stats.DirectSearchCount, stopwatch.Elapsed);

            return directResult;
        }

        private async Task<SemanticSearchResult> SearchSimilarCodeDirectAsync(string repositoryPath, string codeSnippet, int topResults, QueryMethodStatistics stats)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Use direct search to find similar code patterns
                var keywords = this.ExtractKeywords(codeSnippet);
                var searchPattern = string.Join("|", keywords.Select(k => System.Text.RegularExpressions.Regex.Escape(k)));

                var searchOptions = new DirectSearchOptions
                {
                    IsRegex = true,
                    CaseSensitive = false,
                    MaxResults = topResults * 2, // Get more to filter and rank
                    ContextLines = 2,
                    FileExtensions = new List<string> { ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".h" }
                };

                var searchResult = await this.directQueryService.SearchFilesAsync(repositoryPath, searchPattern, searchOptions);
                stopwatch.Stop();

                stats.DirectSearchCount++;
                stats.AverageDirectSearchTime = this.UpdateAverageTime(stats.AverageDirectSearchTime, stats.DirectSearchCount, stopwatch.Elapsed);

                // Convert to semantic search result format
                var result = new SemanticSearchResult
                {
                    Query = codeSnippet,
                    TotalResults = searchResult.Matches.Count
                };

                foreach (var match in searchResult.Matches.Take(topResults))
                {
                    var content = string.Join("\n",
                        match.ContextBefore.Concat(new[] { match.LineContent }).Concat(match.ContextAfter));

                    var chunk = new CodeChunk
                    {
                        FilePath = match.FilePath,
                        Content = content,
                        StartLine = match.LineNumber - match.ContextBefore.Count,
                        EndLine = match.LineNumber + match.ContextAfter.Count,
                        ChunkType = this.DetermineChunkType(content),
                        // Language property removed from CodeChunk model
                        RelevanceScore = this.CalculateRelevanceScore(match, keywords)
                    };

                    result.Results.Add(chunk);
                }

                result.MaxScore = result.Results.Any() ? result.Results.Max(r => r.RelevanceScore) : 0;
                result.MinScore = result.Results.Any() ? result.Results.Min(r => r.RelevanceScore) : 0;

                return result;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Direct code search failed for repository {RepositoryPath}", repositoryPath);
                stats.DirectSearchFailures++;
                return new SemanticSearchResult { Query = codeSnippet };
            }
        }

        private List<CodeChunk> MergeAndDeduplicateResults(List<CodeChunk> vectorResults, List<CodeChunk> directResults, int maxTokens)
        {
            var merged = new List<CodeChunk>();
            var seen = new HashSet<string>();
            var currentTokens = 0;

            // Add vector results first (usually higher quality)
            foreach (var chunk in vectorResults.OrderByDescending(c => c.RelevanceScore))
            {
                if (currentTokens >= maxTokens) break;

                var key = $"{chunk.FilePath}:{chunk.StartLine}-{chunk.EndLine}";
                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    merged.Add(chunk);
                    currentTokens += chunk.TokenCount;
                }
            }

            // Add direct results that aren't duplicates
            foreach (var chunk in directResults.OrderByDescending(c => c.RelevanceScore))
            {
                if (currentTokens >= maxTokens) break;

                var key = $"{chunk.FilePath}:{chunk.StartLine}-{chunk.EndLine}";
                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    merged.Add(chunk);
                    currentTokens += chunk.TokenCount;
                }
            }

            return merged;
        }

        private QueryMethodStatistics GetOrCreateStatistics(string repositoryPath)
        {
            if (!this.statistics.TryGetValue(repositoryPath, out var stats))
            {
                stats = new QueryMethodStatistics
                {
                    RepositoryPath = repositoryPath,
                    IsVectorIndexAvailable = false // Will be updated when we try to use vector search
                };
                this.statistics[repositoryPath] = stats;
            }

            return stats;
        }

        private TimeSpan UpdateAverageTime(TimeSpan averageTime, int count, TimeSpan newTime)
        {
            if (count == 1)
            {
                return newTime;
            }
            else
            {
                var totalTicks = (averageTime.Ticks * (count - 1)) + newTime.Ticks;
                return new TimeSpan(totalTicks / count);
            }
        }

        private void UpdatePerformanceRecommendation(QueryMethodStatistics stats)
        {
            var totalSearches = stats.VectorSearchCount + stats.DirectSearchCount;
            if (totalSearches < 10) return; // Need enough data points

            var vectorFailureRate = stats.VectorSearchCount > 0 ? (double)stats.VectorSearchFailures / stats.VectorSearchCount : 1.0;
            var directFailureRate = stats.DirectSearchCount > 0 ? (double)stats.DirectSearchFailures / stats.DirectSearchCount : 1.0;

            if (vectorFailureRate > 0.3)
            {
                stats.PreferredStrategy = QueryStrategy.DirectOnly;
                stats.PerformanceRecommendation = "High vector search failure rate detected. Consider using DirectOnly strategy.";
            }
            else if (stats.AverageVectorSearchTime > stats.AverageDirectSearchTime.Add(TimeSpan.FromSeconds(2)))
            {
                stats.PreferredStrategy = QueryStrategy.DirectOnly;
                stats.PerformanceRecommendation = "Vector search is significantly slower. Consider using DirectOnly strategy.";
            }
            else if (vectorFailureRate < 0.1 && stats.IsVectorIndexAvailable)
            {
                stats.PreferredStrategy = QueryStrategy.Hybrid;
                stats.PerformanceRecommendation = "Both methods performing well. Hybrid strategy recommended for best results.";
            }
            else
            {
                stats.PreferredStrategy = QueryStrategy.DirectFallback;
                stats.PerformanceRecommendation = "DirectFallback strategy provides good balance of reliability and performance.";
            }
        }

        private List<string> ExtractKeywords(string text)
        {
            var words = System.Text.RegularExpressions.Regex.Matches(text, @"\b[A-Za-z_][A-Za-z0-9_]*\b")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Value)
                .Where(w => w.Length > 2)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return words;
        }

        private double CalculateRelevanceScore(DirectSearchMatch match, List<string> keywords)
        {
            var content = match.LineContent.ToLower();
            var matchCount = keywords.Count(keyword => content.Contains(keyword.ToLower()));
            
            var baseScore = (double)matchCount / keywords.Count;
            
            // Boost for certain file types or locations
            if (match.FilePath.EndsWith(".cs") || match.FilePath.EndsWith(".ts"))
            {
                baseScore *= 1.2;
            }
            
            // Boost for interface or class definitions
            if (content.Contains("class ") || content.Contains("interface ") || content.Contains("public "))
            {
                baseScore *= 1.3;
            }

            return Math.Min(1.0, baseScore);
        }

        private CodeChunkType DetermineChunkType(string content)
        {
            var lowerContent = content.ToLower();
            
            if (lowerContent.Contains("class "))
                return CodeChunkType.Class;
            if (lowerContent.Contains("interface "))
                return CodeChunkType.Interface;
            if (lowerContent.Contains("public ") || lowerContent.Contains("private ") || lowerContent.Contains("protected "))
                return CodeChunkType.Method;
            if (lowerContent.Contains("//") || lowerContent.Contains("/*"))
                return CodeChunkType.Comment;
            
            return CodeChunkType.Other;
        }

        private string DetermineLanguageFromFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".cs" => "csharp",
                ".js" => "javascript",
                ".ts" => "typescript",
                ".py" => "python",
                ".java" => "java",
                ".cpp" or ".c" => "cpp",
                ".h" or ".hpp" => "cpp",
                _ => "text"
            };
        }
    }
}