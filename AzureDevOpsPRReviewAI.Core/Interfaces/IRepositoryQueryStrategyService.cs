namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    using AzureDevOpsPRReviewAI.Core.Models;

    /// <summary>
    /// Unified service that provides repository querying capabilities using either vector-based
    /// or direct file access methods, based on configuration.
    /// </summary>
    public interface IRepositoryQueryStrategyService
    {
        /// <summary>
        /// Get relevant context using the configured query strategy
        /// </summary>
        /// <param name="repositoryPath">Path to the cloned repository</param>
        /// <param name="query">Search query or context description</param>
        /// <param name="querySettings">Query configuration settings</param>
        /// <param name="maxTokens">Maximum tokens to return</param>
        /// <returns>List of relevant code chunks</returns>
        Task<List<CodeChunk>> GetRelevantContextAsync(string repositoryPath, string query, QuerySettings querySettings, int maxTokens = 100000);

        /// <summary>
        /// Search for similar code using the configured strategy
        /// </summary>
        /// <param name="repositoryPath">Path to the cloned repository</param>
        /// <param name="codeSnippet">Code snippet to search for</param>
        /// <param name="querySettings">Query configuration settings</param>
        /// <param name="topResults">Maximum number of results to return</param>
        /// <returns>Semantic search results</returns>
        Task<SemanticSearchResult> SearchSimilarCodeAsync(string repositoryPath, string codeSnippet, QuerySettings querySettings, int topResults = 10);

        /// <summary>
        /// Direct file reading capabilities (available regardless of strategy)
        /// </summary>
        /// <param name="repositoryPath">Path to the cloned repository</param>
        /// <param name="filePath">Relative path to the file</param>
        /// <param name="startLine">Starting line number</param>
        /// <param name="endLine">Ending line number</param>
        /// <param name="branch">Git branch</param>
        /// <returns>File read result</returns>
        Task<DirectFileReadResult> ReadFileAsync(string repositoryPath, string filePath, int? startLine = null, int? endLine = null, string? branch = null);

        /// <summary>
        /// Search files by pattern (available regardless of strategy)
        /// </summary>
        /// <param name="repositoryPath">Path to the cloned repository</param>
        /// <param name="pattern">Search pattern</param>
        /// <param name="options">Search options</param>
        /// <returns>Search results</returns>
        Task<DirectSearchResult> SearchFilesAsync(string repositoryPath, string pattern, DirectSearchOptions options);

        /// <summary>
        /// Find files by name pattern (available regardless of strategy)
        /// </summary>
        /// <param name="repositoryPath">Path to the cloned repository</param>
        /// <param name="namePattern">File name pattern</param>
        /// <param name="options">File search options</param>
        /// <returns>File search results</returns>
        Task<DirectFileSearchResult> FindFilesAsync(string repositoryPath, string namePattern, DirectFileSearchOptions options);

        /// <summary>
        /// Get directory structure (available regardless of strategy)
        /// </summary>
        /// <param name="repositoryPath">Path to the cloned repository</param>
        /// <param name="subPath">Subdirectory to explore</param>
        /// <param name="maxDepth">Maximum depth to traverse</param>
        /// <returns>Directory structure</returns>
        Task<DirectoryStructureResult> GetFileStructureAsync(string repositoryPath, string? subPath = null, int maxDepth = 3);

        /// <summary>
        /// Index repository using the vector-based method (if enabled)
        /// </summary>
        /// <param name="repositoryPath">Path to the cloned repository</param>
        /// <param name="querySettings">Query configuration settings</param>
        /// <returns>Task representing the async operation</returns>
        Task IndexRepositoryAsync(string repositoryPath, QuerySettings querySettings);

        /// <summary>
        /// Update repository index for changed files (if using vector-based method)
        /// </summary>
        /// <param name="repositoryPath">Path to the cloned repository</param>
        /// <param name="changedFiles">List of changed files</param>
        /// <param name="querySettings">Query configuration settings</param>
        /// <returns>Task representing the async operation</returns>
        Task UpdateIndexAsync(string repositoryPath, IEnumerable<string> changedFiles, QuerySettings querySettings);

        /// <summary>
        /// Get query method statistics for monitoring and optimization
        /// </summary>
        /// <param name="repositoryPath">Path to the cloned repository</param>
        /// <returns>Query statistics</returns>
        Task<QueryMethodStatistics> GetQueryStatisticsAsync(string repositoryPath);
    }
}