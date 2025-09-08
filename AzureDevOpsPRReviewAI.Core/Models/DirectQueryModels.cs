namespace AzureDevOpsPRReviewAI.Core.Models
{
    /// <summary>
    /// Result of reading a file with line-based access
    /// </summary>
    public class DirectFileReadResult
    {
        public required string FilePath { get; set; }
        public required string Content { get; set; }
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public int TotalLines { get; set; }
        public bool IsPartialRead { get; set; }
        public bool IsBinary { get; set; }
        public string? Encoding { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime LastModified { get; set; }
        public string? TruncationReason { get; set; }
    }

    /// <summary>
    /// Options for text search across repository files
    /// </summary>
    public class DirectSearchOptions
    {
        public bool IsRegex { get; set; } = false;
        public bool CaseSensitive { get; set; } = false;
        public List<string> FileExtensions { get; set; } = new();
        public List<string> ExcludeFilePatterns { get; set; } = new();
        public List<string> IncludeDirectories { get; set; } = new();
        public List<string> ExcludeDirectories { get; set; } = new();
        public int MaxResults { get; set; } = 100;
        public int MaxFileSizeKB { get; set; } = 500;
        public int ContextLines { get; set; } = 2;
        public bool IncludeBinaryFiles { get; set; } = false;
    }

    /// <summary>
    /// Result of searching for text patterns across files
    /// </summary>
    public class DirectSearchResult
    {
        public required string Query { get; set; }
        public List<DirectSearchMatch> Matches { get; set; } = new();
        public int TotalMatches { get; set; }
        public int FilesSearched { get; set; }
        public int FilesSkipped { get; set; }
        public TimeSpan SearchDuration { get; set; }
        public bool WasTruncated { get; set; }
        public string? TruncationReason { get; set; }
    }

    /// <summary>
    /// Individual match result from text search
    /// </summary>
    public class DirectSearchMatch
    {
        public required string FilePath { get; set; }
        public int LineNumber { get; set; }
        public required string LineContent { get; set; }
        public List<string> ContextBefore { get; set; } = new();
        public List<string> ContextAfter { get; set; } = new();
        public int MatchStartIndex { get; set; }
        public int MatchLength { get; set; }
        public string? MatchedText { get; set; }
    }

    /// <summary>
    /// Options for finding files by name patterns
    /// </summary>
    public class DirectFileSearchOptions
    {
        public List<string> FileExtensions { get; set; } = new();
        public List<string> ExcludeFilePatterns { get; set; } = new();
        public List<string> IncludeDirectories { get; set; } = new();
        public List<string> ExcludeDirectories { get; set; } = new();
        public int MaxResults { get; set; } = 100;
        public bool IncludeHiddenFiles { get; set; } = false;
        public bool SortByModificationDate { get; set; } = true;
    }

    /// <summary>
    /// Result of finding files by name patterns
    /// </summary>
    public class DirectFileSearchResult
    {
        public required string Pattern { get; set; }
        public List<DirectFileInfo> Files { get; set; } = new();
        public int TotalFiles { get; set; }
        public bool WasTruncated { get; set; }
        public string? TruncationReason { get; set; }
        public TimeSpan SearchDuration { get; set; }
    }

    /// <summary>
    /// File information from file search
    /// </summary>
    public class DirectFileInfo
    {
        public required string FilePath { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime LastModified { get; set; }
        public string? FileExtension { get; set; }
        public bool IsBinary { get; set; }
        public bool IsHidden { get; set; }
    }

    /// <summary>
    /// Result of getting directory structure
    /// </summary>
    public class DirectoryStructureResult
    {
        public required string RootPath { get; set; }
        public List<DirectoryNode> Nodes { get; set; } = new();
        public int TotalFiles { get; set; }
        public int TotalDirectories { get; set; }
        public bool WasTruncated { get; set; }
        public string? TruncationReason { get; set; }
    }

    /// <summary>
    /// Node in the directory tree
    /// </summary>
    public class DirectoryNode
    {
        public required string Name { get; set; }
        public required string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime LastModified { get; set; }
        public List<DirectoryNode> Children { get; set; } = new();
        public int Level { get; set; }
        public bool IsBinary { get; set; }
        public string? FileExtension { get; set; }
    }

    /// <summary>
    /// Strategy for repository querying
    /// </summary>
    public enum QueryStrategy
    {
        /// <summary>
        /// Use vector-based semantic search only
        /// </summary>
        VectorOnly,

        /// <summary>
        /// Use direct file access only
        /// </summary>
        DirectOnly,

        /// <summary>
        /// Use both methods and combine results
        /// </summary>
        Hybrid,

        /// <summary>
        /// Use vector search with direct fallback if vector fails
        /// </summary>
        DirectFallback
    }

    /// <summary>
    /// Configuration for repository query methods
    /// </summary>
    public class QuerySettings
    {
        public QueryStrategy Strategy { get; set; } = QueryStrategy.DirectFallback;
        public bool EnableDirectFileAccess { get; set; } = true;
        public bool EnableVectorSearch { get; set; } = true;
        public int MaxDirectSearchResults { get; set; } = 100;
        public int MaxFileReadSizeKB { get; set; } = 500;
        public int DefaultContextLines { get; set; } = 5;
        public List<string> DefaultExcludePatterns { get; set; } = new() { "*.min.js", "*.bundle.js", "*.map", "*.lock" };
        public List<string> DefaultExcludeDirectories { get; set; } = new() { "node_modules", "bin", "obj", ".git", ".vs" };
        public bool EnableSearchResultCaching { get; set; } = true;
        public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(30);
    }

    /// <summary>
    /// Statistics about query method performance and usage
    /// </summary>
    public class QueryMethodStatistics
    {
        public string RepositoryPath { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        // Vector search statistics
        public int VectorSearchCount { get; set; }
        public TimeSpan AverageVectorSearchTime { get; set; }
        public int VectorSearchFailures { get; set; }
        public bool IsVectorIndexAvailable { get; set; }
        public DateTime? LastVectorIndexUpdate { get; set; }
        
        // Direct search statistics
        public int DirectSearchCount { get; set; }
        public TimeSpan AverageDirectSearchTime { get; set; }
        public int DirectSearchFailures { get; set; }
        
        // Fallback statistics
        public int FallbackToDirectCount { get; set; }
        public int FallbackToVectorCount { get; set; }
        
        // Cache statistics
        public int CacheHitCount { get; set; }
        public int CacheMissCount { get; set; }
        public double CacheHitRatio => (CacheHitCount + CacheMissCount) > 0 ? 
            (double)CacheHitCount / (CacheHitCount + CacheMissCount) : 0;
        
        // Performance metrics
        public QueryStrategy PreferredStrategy { get; set; } = QueryStrategy.DirectFallback;
        public string? PerformanceRecommendation { get; set; }
    }
}