namespace AzureDevOpsPRReviewAI.Core.Models
{
    using System.Text.Json.Serialization;

    public class PullRequestCommentWebhookResource
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }

        [JsonPropertyName("publishedDate")]
        public DateTime PublishedDate { get; set; }

        [JsonPropertyName("lastUpdatedDate")]
        public DateTime LastUpdatedDate { get; set; }

        [JsonPropertyName("lastContentUpdatedDate")]
        public DateTime LastContentUpdatedDate { get; set; }

        [JsonPropertyName("author")]
        public WebhookUser? Author { get; set; }

        [JsonPropertyName("pullRequest")]
        public PullRequestReference? PullRequest { get; set; }
    }

    public class PullRequestReference
    {
        [JsonPropertyName("pullRequestId")]
        public int PullRequestId { get; set; }

        [JsonPropertyName("title")]
        public required string Title { get; set; }

        [JsonPropertyName("sourceRefName")]
        public required string SourceRefName { get; set; }

        [JsonPropertyName("targetRefName")]
        public required string TargetRefName { get; set; }

        [JsonPropertyName("repository")]
        public WebhookRepository? Repository { get; set; }
    }
}