namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    public class NomicEmbeddingService : IEmbeddingService
    {
        private readonly ILogger<NomicEmbeddingService> logger;
        private readonly HttpClient httpClient;
        private readonly string nomicApiUrl;
        private readonly string modelName;
        private readonly int batchSize;
        private readonly int maxTokens;

        public NomicEmbeddingService(ILogger<NomicEmbeddingService> logger, IConfiguration configuration, HttpClient httpClient)
        {
            this.logger = logger;
            this.httpClient = httpClient;
            
            // Configuration for self-hosted Nomic API
            this.nomicApiUrl = configuration["NomicEmbedding:ApiUrl"] ?? "http://localhost:8000";
            this.modelName = configuration["NomicEmbedding:Model"] ?? "nomic-ai/nomic-embed-code";
            this.batchSize = int.TryParse(configuration["NomicEmbedding:BatchSize"], out var batchSize) ? batchSize : 32;
            this.maxTokens = int.TryParse(configuration["NomicEmbedding:MaxTokens"], out var maxTokens) ? maxTokens : 8192;

            // Configure HTTP client
            this.httpClient.Timeout = TimeSpan.FromMinutes(5);
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return Array.Empty<float>();
                }

                var preprocessedText = this.PreprocessCodeForEmbedding(text);
                var embedding = await this.CallNomicEmbeddingApiAsync(preprocessedText);

                this.logger.LogDebug(
                    "Generated embedding of size {EmbeddingSize} for code text of length {TextLength}",
                    embedding.Length,
                    text.Length);

                return embedding;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to generate embedding for text using Nomic API");
                return Array.Empty<float>();
            }
        }

        public async Task<List<float[]>> GenerateBatchEmbeddingsAsync(List<string> texts)
        {
            try
            {
                var embeddings = new List<float[]>();

                for (int i = 0; i < texts.Count; i += this.batchSize)
                {
                    var batch = texts.Skip(i).Take(this.batchSize).ToList();
                    var batchEmbeddings = await this.ProcessBatchAsync(batch);
                    embeddings.AddRange(batchEmbeddings);

                    this.logger.LogDebug(
                        "Processed embedding batch {BatchNumber}/{TotalBatches} with {BatchSize} items",
                        (i / this.batchSize) + 1,
                        (int)Math.Ceiling((double)texts.Count / this.batchSize),
                        batch.Count);

                    // Add small delay to avoid overwhelming the API
                    if (i + this.batchSize < texts.Count)
                    {
                        await Task.Delay(50);
                    }
                }

                this.logger.LogInformation(
                    "Generated {EmbeddingCount} embeddings from {TextCount} code texts using Nomic",
                    embeddings.Count,
                    texts.Count);

                return embeddings;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to generate batch embeddings using Nomic API");
                return new List<float[]>();
            }
        }

        public async Task<double> CalculateSimilarityAsync(float[] embedding1, float[] embedding2)
        {
            return await Task.Run(() =>
            {
                if (embedding1.Length == 0 || embedding2.Length == 0 || embedding1.Length != embedding2.Length)
                {
                    return 0.0;
                }

                // Calculate cosine similarity
                var dotProduct = 0.0;
                var magnitude1 = 0.0;
                var magnitude2 = 0.0;

                for (int i = 0; i < embedding1.Length; i++)
                {
                    dotProduct += embedding1[i] * embedding2[i];
                    magnitude1 += embedding1[i] * embedding1[i];
                    magnitude2 += embedding2[i] * embedding2[i];
                }

                if (magnitude1 == 0.0 || magnitude2 == 0.0)
                {
                    return 0.0;
                }

                var similarity = dotProduct / (Math.Sqrt(magnitude1) * Math.Sqrt(magnitude2));
                return Math.Max(-1.0, Math.Min(1.0, similarity)); // Cosine similarity can be negative
            });
        }

        private async Task<List<float[]>> ProcessBatchAsync(List<string> batch)
        {
            try
            {
                // Try batch processing first if the API supports it
                var batchEmbeddings = await this.CallNomicBatchEmbeddingApiAsync(batch);
                if (batchEmbeddings.Count == batch.Count)
                {
                    return batchEmbeddings;
                }

                this.logger.LogWarning("Batch API failed or returned incorrect count, falling back to individual requests");
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Batch embedding failed, falling back to individual requests");
            }

            // Fallback to individual requests
            var embeddings = new List<float[]>();
            foreach (var text in batch)
            {
                var embedding = await this.GenerateEmbeddingAsync(text);
                embeddings.Add(embedding);
            }

            return embeddings;
        }

        private async Task<float[]> CallNomicEmbeddingApiAsync(string text)
        {
            try
            {
                var requestBody = new
                {
                    model = this.modelName,
                    input = text,
                    task_type = "search_document",
                    dimensionality = 768 // nomic-embed-code standard dimensionality
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await this.httpClient.PostAsync($"{this.nomicApiUrl}/v1/embeddings", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    this.logger.LogError(
                        "Nomic API returned error {StatusCode}: {ErrorContent}",
                        response.StatusCode,
                        errorContent);
                    return Array.Empty<float>();
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseData = JsonSerializer.Deserialize<NomicEmbeddingResponse>(responseContent);

                if (responseData?.Data?.FirstOrDefault()?.Embedding != null)
                {
                    return responseData.Data[0].Embedding;
                }

                this.logger.LogWarning("Empty or invalid response from Nomic API");
                return Array.Empty<float>();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to call Nomic embedding API");
                return Array.Empty<float>();
            }
        }

        private async Task<List<float[]>> CallNomicBatchEmbeddingApiAsync(List<string> texts)
        {
            try
            {
                var requestBody = new
                {
                    model = this.modelName,
                    input = texts.ToArray(),
                    task_type = "search_document",
                    dimensionality = 768
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await this.httpClient.PostAsync($"{this.nomicApiUrl}/v1/embeddings", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    this.logger.LogError(
                        "Nomic batch API returned error {StatusCode}: {ErrorContent}",
                        response.StatusCode,
                        errorContent);
                    return new List<float[]>();
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseData = JsonSerializer.Deserialize<NomicEmbeddingResponse>(responseContent);

                if (responseData?.Data != null)
                {
                    return responseData.Data.Select(d => d.Embedding).ToList();
                }

                this.logger.LogWarning("Empty or invalid batch response from Nomic API");
                return new List<float[]>();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to call Nomic batch embedding API");
                return new List<float[]>();
            }
        }

        private string PreprocessCodeForEmbedding(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            // Normalize whitespace while preserving code structure
            code = Regex.Replace(code, @"[ \t]+", " ");
            code = Regex.Replace(code, @"\n\s*\n", "\n");
            code = code.Trim();

            // Truncate if too long (respecting token limits)
            if (code.Length > this.maxTokens * 4) // Rough estimate: 1 token ≈ 4 characters
            {
                code = code.Substring(0, this.maxTokens * 4) + "\n// ... truncated";
                this.logger.LogDebug("Truncated code text to fit within token limits");
            }

            return code;
        }
    }

    // Response models for Nomic API
    internal class NomicEmbeddingResponse
    {
        public List<NomicEmbeddingData>? Data { get; set; }
        public NomicUsage? Usage { get; set; }
    }

    internal class NomicEmbeddingData
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public int Index { get; set; }
    }

    internal class NomicUsage
    {
        public int TotalTokens { get; set; }
    }
}