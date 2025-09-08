namespace AzureDevOpsPRReviewAI.Core.Models
{
    public class RepositoryConfiguration
    {
        public required string Id { get; set; }

        public required string Organization { get; set; }

        public required string Project { get; set; }

        public required string Repository { get; set; }

        public bool IsEnabled { get; set; } = true;

        public List<ReviewRule> ReviewRules { get; set; } = new();

        public List<FileExclusionRule> FileExclusionRules { get; set; } = new();

        public List<CustomPrompt> CustomPrompts { get; set; } = new();

        public WebhookSettings WebhookSettings { get; set; } = new();

        public CommentSettings CommentSettings { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string? CreatedBy { get; set; }

        public string? UpdatedBy { get; set; }

        public int Version { get; set; } = 1;
    }

    public class WebhookSettings
    {
        public bool AutoReviewOnCreate { get; set; } = false;

        public bool AutoReviewOnUpdate { get; set; } = true;

        public bool RequireCommentTrigger { get; set; } = true;

        public List<string> AllowedTriggerUsers { get; set; } = new();

        public int MaxFilesForAutoReview { get; set; } = 50;

        public long MaxDiffSizeBytes { get; set; } = 1024 * 1024; // 1MB
    }

    public class CommentSettings
    {
        public bool EnableLineComments { get; set; } = true;

        public bool EnableSummaryComment { get; set; } = true;

        public bool GroupSimilarIssues { get; set; } = true;

        public bool IncludeConfidenceScore { get; set; } = false;

        public string CommentPrefix { get; set; } = "🤖 AI Code Review";

        public bool EnableReplyToComments { get; set; } = false;

        public int MaxCommentsPerFile { get; set; } = 10;
    }
}