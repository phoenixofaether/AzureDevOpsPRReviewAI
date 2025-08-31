namespace AzureDevOpsPRReviewAI.WebApi.Controllers
{
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly IAzureDevOpsService azureDevOpsService;
        private readonly IAuthenticationService authService;
        private readonly ILogger<TestController> logger;

        public TestController(
            IAzureDevOpsService azureDevOpsService,
            IAuthenticationService authService,
            ILogger<TestController> logger)
        {
            this.azureDevOpsService = azureDevOpsService;
            this.authService = authService;
            this.logger = logger;
        }

        [HttpGet("auth")]
        public async Task<IActionResult> TestAuthentication()
        {
            try
            {
                var isAuthenticated = await this.authService.IsAuthenticatedAsync();
                var authType = this.authService.GetAuthenticationType();

                this.logger.LogInformation(
                    "Authentication test - Type: {AuthType}, Authenticated: {IsAuthenticated}",
                    authType,
                    isAuthenticated);

                return this.Ok(new
                {
                    IsAuthenticated = isAuthenticated,
                    AuthenticationType = authType.ToString(),
                    Timestamp = DateTime.UtcNow,
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to test authentication");
                return this.StatusCode(500, "Authentication test failed");
            }
        }

        [HttpGet("connection")]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                var isValid = await this.azureDevOpsService.ValidateConnectionAsync();

                this.logger.LogInformation("Azure DevOps connection test result: {IsValid}", isValid);

                return this.Ok(new
                {
                    ConnectionValid = isValid,
                    Timestamp = DateTime.UtcNow,
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to test Azure DevOps connection");
                return this.StatusCode(500, "Connection test failed");
            }
        }

        [HttpGet("pullrequest/{organization}/{project}/{repository}/{pullRequestId}")]
        public async Task<IActionResult> TestPullRequest(string organization, string project, string repository, int pullRequestId)
        {
            try
            {
                var pr = await this.azureDevOpsService.GetPullRequestAsync(organization, project, repository, pullRequestId);

                if (pr == null)
                {
                    return this.NotFound("Pull request not found");
                }

                this.logger.LogInformation(
                    "Successfully retrieved pull request {PullRequestId}: {Title}",
                    pr.PullRequestId,
                    pr.Title);

                return this.Ok(pr);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to retrieve pull request {PullRequestId}", pullRequestId);
                return this.StatusCode(500, "Failed to retrieve pull request");
            }
        }
    }
}