namespace AzureDevOpsPRReviewAI.Core.Models
{
    public class CodeAnalysisResult
    {
        public required string RequestId { get; set; }

        public required string PullRequestId { get; set; }

        public bool IsSuccessful { get; set; }

        public string? ErrorMessage { get; set; }

        public List<ReviewComment> Comments { get; set; } = new();

        public AnalysisMetadata Metadata { get; set; } = new();
    }

    public class ReviewComment
    {
        public required string Content { get; set; }

        public string? FilePath { get; set; }

        public int? LineNumber { get; set; }

        public ReviewSeverity Severity { get; set; } = ReviewSeverity.Info;

        public ReviewCategory Category { get; set; } = ReviewCategory.General;
    }

    public class AnalysisMetadata
    {
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

        public int TokensUsed { get; set; }

        public TimeSpan ProcessingTime { get; set; }

        public string? ModelUsed { get; set; }

        public int FilesAnalyzed { get; set; }

        public int LinesAnalyzed { get; set; }
    }

    public enum ReviewSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Critical = 3
    }

    public enum ReviewCategory
    {
        General = 0,
        CodeQuality = 1,
        Security = 2,
        Performance = 3,
        Documentation = 4,
        Testing = 5,
        BestPractices = 6
    }
}