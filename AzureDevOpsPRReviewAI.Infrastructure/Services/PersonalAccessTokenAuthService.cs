namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class PersonalAccessTokenAuthService : IAuthenticationService
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<PersonalAccessTokenAuthService> logger;
        private readonly string? personalAccessToken;

        public PersonalAccessTokenAuthService(IConfiguration configuration, ILogger<PersonalAccessTokenAuthService> logger)
        {
            this.configuration = configuration;
            this.logger = logger;
            this.personalAccessToken = this.configuration["AzureDevOps:PersonalAccessToken"];

            if (string.IsNullOrEmpty(this.personalAccessToken))
            {
                this.logger.LogWarning("Personal Access Token not configured. Please set AzureDevOps:PersonalAccessToken in user secrets or configuration.");
            }
        }

        public Task<string?> GetAccessTokenAsync()
        {
            if (string.IsNullOrEmpty(this.personalAccessToken))
            {
                this.logger.LogError("Personal Access Token is not configured");
                return Task.FromResult<string?>(null);
            }

            this.logger.LogDebug("Retrieved Personal Access Token");
            return Task.FromResult<string?>(this.personalAccessToken);
        }

        public Task<bool> IsAuthenticatedAsync()
        {
            var isAuthenticated = !string.IsNullOrEmpty(this.personalAccessToken);
            this.logger.LogDebug("Personal Access Token authentication status: {IsAuthenticated}", isAuthenticated);
            return Task.FromResult(isAuthenticated);
        }

        public AuthenticationType GetAuthenticationType()
        {
            return AuthenticationType.PersonalAccessToken;
        }
    }
}