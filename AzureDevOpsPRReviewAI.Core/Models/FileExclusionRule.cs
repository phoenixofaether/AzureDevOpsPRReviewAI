namespace AzureDevOpsPRReviewAI.Core.Models
{
    public class FileExclusionRule
    {
        public required string Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public required string Pattern { get; set; }

        public ExclusionType Type { get; set; }

        public bool IsEnabled { get; set; } = true;

        public bool CaseSensitive { get; set; } = false;

        public long? MaxFileSizeBytes { get; set; }

        public List<string> FileExtensions { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum ExclusionType
    {
        /// <summary>
        /// Glob pattern (e.g., "**/*.min.js", "dist/**", "node_modules/**")
        /// </summary>
        Glob,

        /// <summary>
        /// Regular expression pattern
        /// </summary>
        Regex,

        /// <summary>
        /// Exact file path match
        /// </summary>
        ExactPath,

        /// <summary>
        /// Directory path match
        /// </summary>
        Directory,

        /// <summary>
        /// File extension match (e.g., ".dll", ".exe", ".bin")
        /// </summary>
        Extension,

        /// <summary>
        /// Files larger than specified size
        /// </summary>
        FileSize,

        /// <summary>
        /// Binary files (detected by content)
        /// </summary>
        BinaryFiles
    }
}