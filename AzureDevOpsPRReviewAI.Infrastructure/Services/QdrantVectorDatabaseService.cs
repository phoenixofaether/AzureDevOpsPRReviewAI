namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Qdrant.Client;
    using Qdrant.Client.Grpc;

    public class QdrantVectorDatabaseService : IVectorDatabaseService
    {
        private readonly ILogger<QdrantVectorDatabaseService> logger;
        private readonly QdrantClient qdrantClient;
        private readonly string collectionName;
        private readonly uint vectorSize;
        private readonly Distance distance;
        private bool isInitialized = false;

        public QdrantVectorDatabaseService(ILogger<QdrantVectorDatabaseService> logger, IConfiguration configuration)
        {
            this.logger = logger;
            
            var connectionString = configuration["VectorDatabase:ConnectionString"] ?? "http://localhost:6333";
            this.collectionName = configuration["VectorDatabase:CollectionName"] ?? "code-embeddings";
            this.vectorSize = (uint)(int.TryParse(configuration["VectorDatabase:VectorSize"], out var size) ? size : 1024);
            
            var distanceStr = configuration["VectorDatabase:Distance"] ?? "Cosine";
            this.distance = distanceStr.ToLower() switch
            {
                "cosine" => Distance.Cosine,
                "dot" => Distance.Dot,
                "euclid" => Distance.Euclid,
                _ => Distance.Cosine
            };

            this.qdrantClient = new QdrantClient(connectionString);
        }

        public async Task InitializeAsync()
        {
            try
            {
                if (this.isInitialized)
                {
                    return;
                }

                // Check if collection exists
                var collections = await this.qdrantClient.ListCollectionsAsync();
                var collectionExists = collections.Any(c => c == this.collectionName);

                if (!collectionExists)
                {
                    this.logger.LogInformation("Creating Qdrant collection: {CollectionName}", this.collectionName);
                    
                    var vectorsConfig = new VectorParamsMap
                    {
                        Map =
                        {
                            ["default"] = new VectorParams
                            {
                                Size = this.vectorSize,
                                Distance = this.distance,
                            }
                        }
                    };

                    await this.qdrantClient.CreateCollectionAsync(this.collectionName, vectorsConfig);
                    this.logger.LogInformation("Successfully created Qdrant collection: {CollectionName}", this.collectionName);
                }
                else
                {
                    this.logger.LogInformation("Qdrant collection already exists: {CollectionName}", this.collectionName);
                }

                this.isInitialized = true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to initialize Qdrant vector database");
                throw;
            }
        }

        public async Task UpsertEmbeddingsAsync(List<CodeChunkEmbedding> embeddings)
        {
            try
            {
                await this.EnsureInitializedAsync();

                if (!embeddings.Any())
                {
                    return;
                }

                var points = new List<PointStruct>();

                foreach (var embedding in embeddings)
                {
                    var point = new PointStruct
                    {
                        Id = new PointId { Uuid = embedding.Id },
                        Vectors = new Vectors
                        {
                            Vector = new Vector 
                            { 
                                Data = { embedding.Embedding.Select(x => (float)x) }
                            }
                        },
                        Payload =
                        {
                            ["repository_path"] = new Value { StringValue = embedding.RepositoryPath },
                            ["file_path"] = new Value { StringValue = embedding.FilePath },
                            ["chunk_type"] = new Value { StringValue = embedding.Chunk.ChunkType.ToString() },
                            ["class_name"] = new Value { StringValue = embedding.Chunk.ClassName ?? string.Empty },
                            ["method_name"] = new Value { StringValue = embedding.Chunk.MethodName ?? string.Empty },
                            ["namespace"] = new Value { StringValue = embedding.Chunk.Namespace ?? string.Empty },
                            ["start_line"] = new Value { IntegerValue = embedding.Chunk.StartLine },
                            ["end_line"] = new Value { IntegerValue = embedding.Chunk.EndLine },
                            ["token_count"] = new Value { IntegerValue = embedding.Chunk.TokenCount },
                            ["indexed_at"] = new Value { StringValue = embedding.IndexedAt.ToString("yyyy-MM-ddTHH:mm:ssZ") },
                            ["content_preview"] = new Value { StringValue = embedding.Chunk.Content.Length > 200 
                                ? embedding.Chunk.Content.Substring(0, 200) + "..."
                                : embedding.Chunk.Content }
                        }
                    };

                    // Add custom metadata
                    foreach (var metadata in embedding.Metadata)
                    {
                        if (!point.Payload.ContainsKey(metadata.Key))
                        {
                            point.Payload[metadata.Key] = new Value { StringValue = metadata.Value?.ToString() ?? string.Empty };
                        }
                    }

                    points.Add(point);
                }

                await this.qdrantClient.UpsertAsync(this.collectionName, points);
                
                this.logger.LogInformation("Successfully upserted {EmbeddingCount} embeddings to Qdrant collection: {CollectionName}",
                    embeddings.Count, this.collectionName);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to upsert embeddings to Qdrant");
                throw;
            }
        }

        public async Task<List<SimilarityResult>> SearchSimilarAsync(float[] queryEmbedding, int topK = 10, double threshold = 0.7)
        {
            try
            {
                await this.EnsureInitializedAsync();

                if (queryEmbedding.Length == 0)
                {
                    return new List<SimilarityResult>();
                }

                var searchResult = await this.qdrantClient.SearchAsync(
                    this.collectionName,
                    queryEmbedding,
                    limit: (ulong)topK,
                    scoreThreshold: (float)threshold
                );

                var results = new List<SimilarityResult>();

                foreach (var point in searchResult)
                {
                    var payload = point.Payload;
                    
                    var codeChunk = new CodeChunk
                    {
                        FilePath = payload.GetValueOrDefault("file_path")?.StringValue ?? string.Empty,
                        Content = payload.GetValueOrDefault("content_preview")?.StringValue ?? string.Empty,
                        StartLine = (int)(payload.GetValueOrDefault("start_line")?.IntegerValue ?? 0),
                        EndLine = (int)(payload.GetValueOrDefault("end_line")?.IntegerValue ?? 0),
                        ClassName = payload.GetValueOrDefault("class_name")?.StringValue,
                        MethodName = payload.GetValueOrDefault("method_name")?.StringValue,
                        Namespace = payload.GetValueOrDefault("namespace")?.StringValue,
                        TokenCount = (int)(payload.GetValueOrDefault("token_count")?.IntegerValue ?? 0),
                        RelevanceScore = (double)point.Score
                    };

                    if (Enum.TryParse<CodeChunkType>(payload.GetValueOrDefault("chunk_type")?.StringValue, out var chunkType))
                    {
                        codeChunk.ChunkType = chunkType;
                    }

                    var metadata = new Dictionary<string, object>();
                    foreach (var kvp in payload)
                    {
                        if (!kvp.Key.StartsWith("repository_path") && 
                            !kvp.Key.StartsWith("file_path") && 
                            !kvp.Key.StartsWith("chunk_type") &&
                            !kvp.Key.StartsWith("class_name") &&
                            !kvp.Key.StartsWith("method_name") &&
                            !kvp.Key.StartsWith("namespace") &&
                            !kvp.Key.StartsWith("start_line") &&
                            !kvp.Key.StartsWith("end_line") &&
                            !kvp.Key.StartsWith("token_count") &&
                            !kvp.Key.StartsWith("indexed_at") &&
                            !kvp.Key.StartsWith("content_preview"))
                        {
                            metadata[kvp.Key] = kvp.Value;
                        }
                    }

                    results.Add(new SimilarityResult
                    {
                        CodeChunk = codeChunk,
                        SimilarityScore = (double)point.Score,
                        Id = point.Id?.ToString() ?? string.Empty,
                        Metadata = metadata
                    });
                }

                this.logger.LogDebug("Found {ResultCount} similar embeddings with threshold {Threshold}",
                    results.Count, threshold);

                return results.OrderByDescending(r => r.SimilarityScore).ToList();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to search similar embeddings in Qdrant");
                return new List<SimilarityResult>();
            }
        }

        public async Task DeleteByRepositoryAsync(string repositoryPath)
        {
            try
            {
                await this.EnsureInitializedAsync();

                var filter = new Filter
                {
                    Must =
                    {
                        new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = "repository_path",
                                Match = new Match
                                {
                                    Text = repositoryPath
                                }
                            }
                        }
                    }
                };

                await this.qdrantClient.DeleteAsync(this.collectionName, filter);
                
                this.logger.LogInformation("Successfully deleted embeddings for repository: {RepositoryPath}",
                    repositoryPath);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to delete embeddings for repository: {RepositoryPath}", repositoryPath);
                throw;
            }
        }

        public async Task UpdateEmbeddingsAsync(List<CodeChunkEmbedding> embeddings)
        {
            // For Qdrant, update is the same as upsert
            await this.UpsertEmbeddingsAsync(embeddings);
        }

        public async Task<bool> ExistsAsync(string embeddingId)
        {
            try
            {
                await this.EnsureInitializedAsync();

                var points = await this.qdrantClient.RetrieveAsync(
                    this.collectionName,
                    new[] { new PointId { Uuid = embeddingId } },
                    withPayload: false,
                    withVectors: false
                );

                return points.Any();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to check if embedding exists: {EmbeddingId}", embeddingId);
                return false;
            }
        }

        public async Task<int> GetEmbeddingCountAsync(string? repositoryPath = null)
        {
            try
            {
                await this.EnsureInitializedAsync();

                Filter? filter = null;
                if (!string.IsNullOrEmpty(repositoryPath))
                {
                    filter = new Filter
                    {
                        Must =
                        {
                            new Condition
                            {
                                Field = new FieldCondition
                                {
                                    Key = "repository_path",
                                    Match = new Match
                                    {
                                        Text = repositoryPath
                                    }
                                }
                            }
                        }
                    };
                }

                var countResult = await this.qdrantClient.CountAsync(this.collectionName, filter);
                return (int)countResult;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get embedding count for repository: {RepositoryPath}", repositoryPath);
                return 0;
            }
        }

        private async Task EnsureInitializedAsync()
        {
            if (!this.isInitialized)
            {
                await this.InitializeAsync();
            }
        }
    }
}