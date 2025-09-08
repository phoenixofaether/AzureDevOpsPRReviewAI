namespace AzureDevOpsPRReviewAI.Core.Models
{
    public class CustomPrompt
    {
        public required string Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public PromptType Type { get; set; }

        public required string Template { get; set; }

        public bool IsEnabled { get; set; } = true;

        public List<string> SupportedLanguages { get; set; } = new();

        public List<string> SupportedFileExtensions { get; set; } = new();

        public Dictionary<string, string> Variables { get; set; } = new();

        public PromptScope Scope { get; set; } = PromptScope.Repository;

        public int Priority { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string? CreatedBy { get; set; }

        public string? UpdatedBy { get; set; }
    }

    public enum PromptType
    {
        /// <summary>
        /// Main analysis prompt for code review
        /// </summary>
        CodeAnalysis,

        /// <summary>
        /// Security-focused analysis prompt
        /// </summary>
        SecurityAnalysis,

        /// <summary>
        /// Performance analysis prompt
        /// </summary>
        PerformanceAnalysis,

        /// <summary>
        /// Documentation review prompt
        /// </summary>
        DocumentationReview,

        /// <summary>
        /// Test coverage analysis prompt
        /// </summary>
        TestAnalysis,

        /// <summary>
        /// Architecture review prompt
        /// </summary>
        ArchitectureReview,

        /// <summary>
        /// Code style and formatting prompt
        /// </summary>
        StyleReview,

        /// <summary>
        /// Summary generation prompt
        /// </summary>
        Summary,

        /// <summary>
        /// Custom user-defined prompt
        /// </summary>
        Custom
    }

    public enum PromptScope
    {
        /// <summary>
        /// Applied to entire organization
        /// </summary>
        Organization,

        /// <summary>
        /// Applied to entire project
        /// </summary>
        Project,

        /// <summary>
        /// Applied to specific repository
        /// </summary>
        Repository,

        /// <summary>
        /// Applied to specific file types
        /// </summary>
        FileType
    }
}