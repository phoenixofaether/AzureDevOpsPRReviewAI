namespace AzureDevOpsPRReviewAI.Core.Models
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class AzureDevOpsWebhookEvent
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("eventType")]
        public required string EventType { get; set; }

        [JsonPropertyName("publisherId")]
        public required string PublisherId { get; set; }

        [JsonPropertyName("scope")]
        public required string Scope { get; set; }

        [JsonPropertyName("message")]
        public WebhookMessage? Message { get; set; }

        [JsonPropertyName("detailedMessage")]
        public WebhookMessage? DetailedMessage { get; set; }

        [JsonPropertyName("resource")]
        public object? Resource { get; set; }

        // Helper methods to get typed resources
        public PullRequestWebhookResource? GetPullRequestResource()
        {
            if (Resource is JsonElement element)
            {
                return JsonSerializer.Deserialize<PullRequestWebhookResource>(element.GetRawText());
            }
            return Resource as PullRequestWebhookResource;
        }

        public PullRequestCommentWebhookResource? GetCommentResource()
        {
            if (Resource is JsonElement element)
            {
                return JsonSerializer.Deserialize<PullRequestCommentWebhookResource>(element.GetRawText());
            }
            return Resource as PullRequestCommentWebhookResource;
        }

        [JsonPropertyName("createdDate")]
        public DateTime CreatedDate { get; set; }
    }
}