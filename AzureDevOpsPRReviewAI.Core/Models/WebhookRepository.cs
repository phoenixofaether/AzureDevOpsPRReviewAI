namespace AzureDevOpsPRReviewAI.Core.Models
{
    using System.Text.Json.Serialization;

    public class WebhookRepository
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("project")]
        public WebhookProject? Project { get; set; }
    }
}