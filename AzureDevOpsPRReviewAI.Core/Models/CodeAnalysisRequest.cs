namespace AzureDevOpsPRReviewAI.Core.Models
{
    public class CodeAnalysisRequest
    {
        public required string PullRequestId { get; set; }

        public required string Organization { get; set; }

        public required string Project { get; set; }

        public required string Repository { get; set; }

        public required string SourceBranch { get; set; }

        public required string TargetBranch { get; set; }

        public string? Title { get; set; }

        public string? Description { get; set; }

        public List<string> ChangedFiles { get; set; } = new();

        public string? DiffContent { get; set; }

        public AIReviewCommand? Command { get; set; }
    }
}