namespace AzureDevOpsPRReviewAI.Core.Models
{
    public class FormattedComment
    {
        public required string Content { get; set; }

        public string? FilePath { get; set; }

        public int? LineNumber { get; set; }

        public ReviewSeverity Severity { get; set; }

        public ReviewCategory Category { get; set; }

        public bool IsThreadRoot { get; set; } = true;

        public string? ParentCommentId { get; set; }

        public double? ConfidenceScore { get; set; }

        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class CommentPostResult
    {
        public required string CommentId { get; set; }

        public bool IsSuccessful { get; set; }

        public string? ErrorMessage { get; set; }

        public DateTime PostedAt { get; set; } = DateTime.UtcNow;
    }
}