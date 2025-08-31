namespace AzureDevOpsPRReviewAI.Core.Models
{
    using System.Text.Json.Serialization;

    public class WebhookProject
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }
    }
}