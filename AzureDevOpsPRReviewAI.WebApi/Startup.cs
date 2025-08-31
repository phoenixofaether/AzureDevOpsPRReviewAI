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
