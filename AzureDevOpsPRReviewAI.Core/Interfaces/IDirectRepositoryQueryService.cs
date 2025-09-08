namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    using AzureDevOpsPRReviewAI.Core.Models;

    /// <summary>
    /// Provides Claude Code-like direct repository querying capabilities
    /// without requiring vector databases or embeddings.
    /// </summary>
    public interface IDirectRepositoryQueryService
    {
        /// <summary>
        /// Read specific lines from a file, similar to Claude Code's Read tool
        /// </summary>
        /// <param name="repositoryPath">Path to the cloned repository</param>
        /// <param name="filePath">Relative path to the file within the repository</param>
        /// <param name="startLine">Starting line number (1-based), null for beginning of file</param>
        /// <param name="endLine">Ending line number (1-based), null for end of file</param>
        /// <param name="branch">Git branch to read from, null for current branch</param>
        /// <returns>File content with line numbers and metadata</returns>
        Task<DirectFileReadResult> ReadFileAsync(string repositoryPath, string filePath, int? startLine = null, int? endLine = null, string? branch = null);

        /// <summary>
        /// Search for text patterns across repository files, similar to Claude Code's Grep tool
        /// </summary>
        /// <param name="repositoryPath">Path to the cloned repository</param>
        /// <param name="pattern">Search pattern (supports regex if isRegex is true)</param>
        /// <param name="options">Search options including file filters and limits</param>
        /// <returns>Search results with file locations and context</returns>
        Task<DirectSearchResult> SearchFilesAsync(string repositoryPath, string pattern, DirectSearchOptions options);

        /// <summary>
        /// Find files by name patterns, similar to Claude Code's Glob tool
        /// </summary>
        /// <param name="repositoryPath">Path to the cloned repository</param>
        /// <param name="namePattern">File name pattern (supports wildcards)</param>
        /// <param name="options">Search options for file filtering</param>
        /// <returns>List of matching file paths with metadata</returns>
        Task<DirectFileSearchResult> FindFilesAsync(string repositoryPath, string namePattern, DirectFileSearchOptions options);

        /// <summary>
        /// Get repository directory structure, similar to ls command
        /// </summary>
        /// <param name="repositoryPath">Path to the cloned repository</param>
        /// <param name="subPath">Subdirectory to explore, null for root</param>
        /// <param name="maxDepth">Maximum depth to traverse</param>
        /// <returns>Directory tree structure</returns>
        Task<DirectoryStructureResult> GetFileStructureAsync(string repositoryPath, string? subPath = null, int maxDepth = 3);

        /// <summary>
        /// Get context lines around a specific line number
        /// </summary>
        /// <param name="repositoryPath">Path to the cloned repository</param>
        /// <param name="filePath">Relative path to the file within the repository</param>
        /// <param name="lineNumber">Target line number (1-based)</param>
        /// <param name="contextLines">Number of lines before and after to include</param>
        /// <returns>File content with context around the specified line</returns>
        Task<DirectFileReadResult> GetNearbyContextAsync(string repositoryPath, string filePath, int lineNumber, int contextLines = 5);

        /// <summary>
        /// Get relevant context for a query using direct file operations
        /// </summary>
        /// <param name="repositoryPath">Path to the cloned repository</param>
        /// <param name="query">Search query or context description</param>
        /// <param name="maxTokens">Maximum tokens to return</param>
        /// <returns>List of relevant code chunks found via direct search</returns>
        Task<List<CodeChunk>> GetRelevantContextAsync(string repositoryPath, string query, int maxTokens = 100000);
    }
}