namespace AzureDevOpsPRReviewAI.Core.Models
{
    using System.ComponentModel.DataAnnotations;

    public class PullRequestModel
    {
        public int PullRequestId { get; set; }

        public required string Title { get; set; }

        public string? Description { get; set; }

        public required string SourceBranch { get; set; }

        public required string TargetBranch { get; set; }

        public required string AuthorName { get; set; }

        public required string AuthorEmail { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? UpdatedDate { get; set; }

        public PullRequestStatus Status { get; set; }

        public required string RepositoryName { get; set; }

        public required string ProjectName { get; set; }

        public required string OrganizationName { get; set; }

        public List<string> ReviewerIds { get; set; } = new();

        public List<PullRequestFile> ChangedFiles { get; set; } = new();
    }
}