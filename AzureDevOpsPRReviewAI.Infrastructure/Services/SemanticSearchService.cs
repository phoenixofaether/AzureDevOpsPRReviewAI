namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;
    using Microsoft.Extensions.Logging;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;

    public class SemanticSearchService : ISemanticSearchService
    {
        private readonly ILogger<SemanticSearchService> logger;
        private readonly IFileRetrievalService fileRetrievalService;
        private readonly IRepositoryService repositoryService;
        private readonly ICodeChunkingService codeChunkingService;
        private readonly IEmbeddingService embeddingService;
        private readonly IVectorDatabaseService vectorDatabaseService;

        public SemanticSearchService(
            ILogger<SemanticSearchService> logger,
            IFileRetrievalService fileRetrievalService,
            IRepositoryService repositoryService,
            ICodeChunkingService codeChunkingService,
            IEmbeddingService embeddingService,
            IVectorDatabaseService vectorDatabaseService)
        {
            this.logger = logger;
            this.fileRetrievalService = fileRetrievalService;
            this.repositoryService = repositoryService;
            this.codeChunkingService = codeChunkingService;
            this.embeddingService = embeddingService;
            this.vectorDatabaseService = vectorDatabaseService;
        }

        public async Task<List<CodeChunk>> GetRelevantContextAsync(string repositoryPath, string query, int maxTokens = 100000)
        {
            try
            {
                this.logger.LogInformation("Searching for relevant context using vector embeddings for query: {Query}", query);
                
                // Generate embedding for the query
                var queryEmbedding = await this.embeddingService.GenerateEmbeddingAsync(query);
                if (queryEmbedding.Length == 0)
                {
                    this.logger.LogWarning("Failed to generate embedding for query, falling back to keyword search");
                    return await this.GetRelevantContextFallbackAsync(repositoryPath, query, maxTokens);
                }

                // Search for similar code chunks using vector similarity
                var similarResults = await this.vectorDatabaseService.SearchSimilarAsync(
                    queryEmbedding, 
                    topK: 100, 
                    threshold: 0.3);

                if (!similarResults.Any())
                {
                    this.logger.LogInformation("No similar embeddings found, falling back to keyword search");
                    return await this.GetRelevantContextFallbackAsync(repositoryPath, query, maxTokens);
                }

                // Filter results by repository path and convert to CodeChunk
                var relevantChunks = new List<CodeChunk>();
                var currentTokens = 0;

                foreach (var result in similarResults.Where(r => r.Metadata.ContainsKey("repository_path") && 
                    r.Metadata["repository_path"].ToString() == repositoryPath))
                {
                    if (currentTokens >= maxTokens)
                    {
                        break;
                    }

                    var chunk = result.CodeChunk;
                    chunk.RelevanceScore = result.SimilarityScore;
                    
                    // Ensure we have the full content for chunks that were truncated in the database
                    if (chunk.Content.EndsWith("..."))
                    {
                        try
                        {
                            var fileContent = await this.fileRetrievalService.GetFileAsync(repositoryPath, chunk.FilePath);
                            if (fileContent?.Content != null && !fileContent.IsBinary)
                            {
                                var fullChunks = await this.codeChunkingService.ChunkCodeAsync(chunk.FilePath, fileContent.Content);
                                var matchingChunk = fullChunks.FirstOrDefault(c => 
                                    c.StartLine == chunk.StartLine && c.EndLine == chunk.EndLine);
                                
                                if (matchingChunk != null)
                                {
                                    chunk.Content = matchingChunk.Content;
                                    chunk.TokenCount = await this.codeChunkingService.CountTokensAsync(chunk.Content);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            this.logger.LogWarning(ex, "Failed to retrieve full content for chunk in file: {FilePath}", chunk.FilePath);
                        }
                    }

                    if (currentTokens + chunk.TokenCount <= maxTokens)
                    {
                        relevantChunks.Add(chunk);
                        currentTokens += chunk.TokenCount;
                    }
                }

                this.logger.LogInformation(
                    "Found {ChunkCount} relevant code chunks using {TokenCount} tokens via vector similarity",
                    relevantChunks.Count,
                    currentTokens);

                return relevantChunks;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get relevant context using vector embeddings, falling back to keyword search");
                return await this.GetRelevantContextFallbackAsync(repositoryPath, query, maxTokens);
            }
        }

        public async Task<SemanticSearchResult> SearchSimilarCodeAsync(string repositoryPath, string codeSnippet, int topResults = 10)
        {
            try
            {
                this.logger.LogDebug("Performing semantic search for code snippet in repository: {RepositoryPath}", repositoryPath);

                var result = new SemanticSearchResult
                {
                    Query = codeSnippet,
                    Results = new List<CodeChunk>(),
                };

                // Generate embedding for the code snippet
                var snippetEmbedding = await this.embeddingService.GenerateEmbeddingAsync(codeSnippet);
                if (snippetEmbedding.Length == 0)
                {
                    this.logger.LogWarning("Failed to generate embedding for code snippet, falling back to keyword search");
                    return await this.SearchSimilarCodeFallbackAsync(repositoryPath, codeSnippet, topResults);
                }

                // Search for similar code chunks using vector similarity
                var similarResults = await this.vectorDatabaseService.SearchSimilarAsync(
                    snippetEmbedding, 
                    topK: topResults * 2, // Get more results to filter by repository
                    threshold: 0.4);

                // Filter by repository path and convert to CodeChunk
                var repositoryResults = similarResults.Where(r => 
                    r.Metadata.ContainsKey("repository_path") && 
                    r.Metadata["repository_path"].ToString() == repositoryPath)
                    .Take(topResults)
                    .ToList();

                foreach (var similarResult in repositoryResults)
                {
                    result.Results.Add(similarResult.CodeChunk);
                }

                result.TotalResults = repositoryResults.Count;
                result.MaxScore = result.Results.Any() ? result.Results.Max(r => r.RelevanceScore) : 0;
                result.MinScore = result.Results.Any() ? result.Results.Min(r => r.RelevanceScore) : 0;

                this.logger.LogDebug(
                    "Vector semantic search returned {ResultCount} results for code snippet",
                    result.Results.Count);

                return result;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to perform vector semantic search, falling back to keyword search");
                return await this.SearchSimilarCodeFallbackAsync(repositoryPath, codeSnippet, topResults);
            }
        }

        public async Task IndexRepositoryAsync(string repositoryPath)
        {
            try
            {
                this.logger.LogInformation("Starting vector database indexing for repository: {RepositoryPath}", repositoryPath);
                
                // Initialize vector database
                await this.vectorDatabaseService.InitializeAsync();

                // Clear existing embeddings for this repository
                await this.vectorDatabaseService.DeleteByRepositoryAsync(repositoryPath);

                // Get all code files in the repository
                var allFiles = await this.repositoryService.GetAllFilesAsync(repositoryPath);
                var codeFiles = allFiles.Where(f => this.IsCodeFile(f)).ToList();

                this.logger.LogInformation("Found {CodeFileCount} code files to index in repository: {RepositoryPath}",
                    codeFiles.Count, repositoryPath);

                var allEmbeddings = new List<CodeChunkEmbedding>();
                var processedFiles = 0;

                foreach (var filePath in codeFiles)
                {
                    try
                    {
                        var fileContent = await this.fileRetrievalService.GetFileAsync(repositoryPath, filePath);
                        if (fileContent?.Content == null || fileContent.IsBinary)
                        {
                            continue;
                        }

                        // Chunk the file content
                        var chunks = await this.codeChunkingService.ChunkCodeAsync(filePath, fileContent.Content);
                        
                        foreach (var chunk in chunks)
                        {
                            // Generate embedding for the chunk
                            var embedding = await this.embeddingService.GenerateEmbeddingAsync(chunk.Content);
                            if (embedding.Length > 0)
                            {
                                var codeChunkEmbedding = new CodeChunkEmbedding
                                {
                                    Id = this.GenerateEmbeddingId(repositoryPath, chunk),
                                    RepositoryPath = repositoryPath,
                                    FilePath = filePath,
                                    Chunk = chunk,
                                    Embedding = embedding,
                                    Metadata = new Dictionary<string, object>
                                    {
                                        ["repository_path"] = repositoryPath,
                                        ["file_extension"] = Path.GetExtension(filePath).ToLower()
                                    },
                                    IndexedAt = DateTime.UtcNow
                                };

                                allEmbeddings.Add(codeChunkEmbedding);
                            }
                        }

                        processedFiles++;
                        if (processedFiles % 10 == 0)
                        {
                            this.logger.LogInformation("Processed {ProcessedFiles}/{TotalFiles} files for indexing",
                                processedFiles, codeFiles.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogWarning(ex, "Failed to process file for indexing: {FilePath}", filePath);
                    }
                }

                // Batch upsert embeddings to vector database
                if (allEmbeddings.Any())
                {
                    this.logger.LogInformation("Upserting {EmbeddingCount} embeddings to vector database", allEmbeddings.Count);
                    await this.vectorDatabaseService.UpsertEmbeddingsAsync(allEmbeddings);
                }

                this.logger.LogInformation(
                    "Successfully indexed repository {RepositoryPath} with {EmbeddingCount} embeddings from {FileCount} files",
                    repositoryPath, allEmbeddings.Count, processedFiles);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to index repository: {RepositoryPath}", repositoryPath);
                throw;
            }
        }

        public async Task UpdateIndexAsync(string repositoryPath, IEnumerable<string> changedFiles)
        {
            try
            {
                var changedFilesList = changedFiles.ToList();
                this.logger.LogInformation(
                    "Updating vector database index for {FileCount} changed files in repository: {RepositoryPath}",
                    changedFilesList.Count,
                    repositoryPath);

                // Initialize vector database
                await this.vectorDatabaseService.InitializeAsync();

                var updatedEmbeddings = new List<CodeChunkEmbedding>();

                foreach (var filePath in changedFilesList.Where(f => this.IsCodeFile(f)))
                {
                    try
                    {
                        var fileContent = await this.fileRetrievalService.GetFileAsync(repositoryPath, filePath);
                        if (fileContent?.Content == null || fileContent.IsBinary)
                        {
                            continue;
                        }

                        // Chunk the file content
                        var chunks = await this.codeChunkingService.ChunkCodeAsync(filePath, fileContent.Content);
                        
                        foreach (var chunk in chunks)
                        {
                            // Generate embedding for the chunk
                            var embedding = await this.embeddingService.GenerateEmbeddingAsync(chunk.Content);
                            if (embedding.Length > 0)
                            {
                                var codeChunkEmbedding = new CodeChunkEmbedding
                                {
                                    Id = this.GenerateEmbeddingId(repositoryPath, chunk),
                                    RepositoryPath = repositoryPath,
                                    FilePath = filePath,
                                    Chunk = chunk,
                                    Embedding = embedding,
                                    Metadata = new Dictionary<string, object>
                                    {
                                        ["repository_path"] = repositoryPath,
                                        ["file_extension"] = Path.GetExtension(filePath).ToLower()
                                    },
                                    IndexedAt = DateTime.UtcNow
                                };

                                updatedEmbeddings.Add(codeChunkEmbedding);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogWarning(ex, "Failed to process changed file for index update: {FilePath}", filePath);
                    }
                }

                // Update embeddings in vector database
                if (updatedEmbeddings.Any())
                {
                    await this.vectorDatabaseService.UpdateEmbeddingsAsync(updatedEmbeddings);
                    this.logger.LogInformation("Successfully updated {EmbeddingCount} embeddings for {FileCount} changed files",
                        updatedEmbeddings.Count, changedFilesList.Count);
                }
                else
                {
                    this.logger.LogInformation("No embeddings to update for changed files");
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to update index for changed files in repository: {RepositoryPath}", repositoryPath);
                throw;
            }
        }

        private string GenerateEmbeddingId(string repositoryPath, CodeChunk chunk)
        {
            var input = $"{repositoryPath}|{chunk.FilePath}|{chunk.StartLine}|{chunk.EndLine}|{chunk.ChunkType}";
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hash).ToLower();
        }

        private async Task<List<CodeChunk>> GetRelevantContextFallbackAsync(string repositoryPath, string query, int maxTokens)
        {
            try
            {
                var relevantChunks = new List<CodeChunk>();
                var currentTokens = 0;

                var allFiles = await this.repositoryService.GetAllFilesAsync(repositoryPath);
                var codeFiles = allFiles.Where(f => this.IsCodeFile(f)).Take(50).ToList();

                this.logger.LogInformation(
                    "Fallback: Searching for relevant context in {FileCount} code files for query: {Query}",
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

                return relevantChunks.OrderByDescending(c => c.RelevanceScore).Take(100).ToList();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed fallback context search for query: {Query}", query);
                return new List<CodeChunk>();
            }
        }

        private async Task<SemanticSearchResult> SearchSimilarCodeFallbackAsync(string repositoryPath, string codeSnippet, int topResults)
        {
            try
            {
                var result = new SemanticSearchResult
                {
                    Query = codeSnippet,
                    Results = new List<CodeChunk>(),
                };

                var keywords = this.ExtractKeywords(codeSnippet);
                var contextChunks = await this.GetRelevantContextFallbackAsync(repositoryPath, string.Join(" ", keywords), 50000);

                result.Results = contextChunks
                    .Take(topResults)
                    .ToList();

                result.TotalResults = contextChunks.Count;
                result.MaxScore = result.Results.Any() ? result.Results.Max(r => r.RelevanceScore) : 0;
                result.MinScore = result.Results.Any() ? result.Results.Min(r => r.RelevanceScore) : 0;

                return result;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed fallback semantic search");
                return new SemanticSearchResult { Query = codeSnippet };
            }
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