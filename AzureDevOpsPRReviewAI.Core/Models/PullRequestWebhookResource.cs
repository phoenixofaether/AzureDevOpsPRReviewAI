namespace AzureDevOpsPRReviewAI.Core.Models
{
    using System.Text.Json.Serialization;

    public class PullRequestWebhookResource
    {
        [JsonPropertyName("pullRequestId")]
        public int PullRequestId { get; set; }

        [JsonPropertyName("title")]
        public required string Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("sourceRefName")]
        public required string SourceRefName { get; set; }

        [JsonPropertyName("targetRefName")]
        public required string TargetRefName { get; set; }

        [JsonPropertyName("status")]
        public required string Status { get; set; }

        [JsonPropertyName("creationDate")]
        public DateTime CreationDate { get; set; }

        [JsonPropertyName("createdBy")]
        public WebhookUser? CreatedBy { get; set; }

        [JsonPropertyName("repository")]
        public WebhookRepository? Repository { get; set; }
    }
}