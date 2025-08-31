namespace AzureDevOpsPRReviewAI.Core.Models
{
    using System.Text.Json.Serialization;

    public class WebhookMessage
    {
        [JsonPropertyName("text")]
        public required string Text { get; set; }

        [JsonPropertyName("html")]
        public required string Html { get; set; }

        [JsonPropertyName("markdown")]
        public required string Markdown { get; set; }
    }
}