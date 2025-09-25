namespace AzureDevOpsPRReviewAI.WebApi
{
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Infrastructure.Services;
    using Microsoft.AspNetCore.Builder;
    using Serilog;

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Add services to the container
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            // Add application services
            services.AddScoped<IAuthenticationService, PersonalAccessTokenAuthService>();
            services.AddScoped<IAzureDevOpsService, AzureDevOpsService>();
            services.AddScoped<ICommandParserService, CommandParserService>();
            services.AddScoped<IClaudeApiService, ClaudeApiService>();
            services.AddScoped<ICommentFormatterService, CommentFormatterService>();
            services.AddScoped<IPullRequestCommentService, PullRequestCommentService>();

            // Add repository management services
            services.AddScoped<IRepositoryService, RepositoryService>();
            services.AddScoped<IFileRetrievalService, FileRetrievalService>();
            services.AddScoped<ICodeContextService, CodeContextService>();

            // Add new code analysis services
            services.AddScoped<ITokenizerService, TokenizerService>();
            services.AddScoped<ICodeChunkingService, CodeChunkingService>();
            services.AddScoped<IDependencyAnalysisService, DependencyAnalysisService>();

            // Add vector database and embedding services
            services.AddHttpClient<NomicEmbeddingService>();
            services.AddScoped<IEmbeddingService, NomicEmbeddingService>();
            services.AddScoped<IVectorDatabaseService, QdrantVectorDatabaseService>();
            services.AddScoped<ISemanticSearchService, SemanticSearchService>();

            // Add direct repository query services
            services.AddScoped<IDirectRepositoryQueryService, DirectRepositoryQueryService>();
            services.AddScoped<IRepositoryQueryStrategyService, RepositoryQueryStrategyService>();

            // Add configuration services
            services.AddScoped<IRepositoryConfigurationService, RepositoryConfigurationService>();
            services.AddScoped<IUserManagementService, UserManagementService>();

            // Add caching
            services.AddMemoryCache();

            var redisConnectionString = this.Configuration.GetConnectionString("Redis");
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnectionString;
                });
            }
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Configure the HTTP request pipeline
            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                app.UseHttpsRedirection();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
