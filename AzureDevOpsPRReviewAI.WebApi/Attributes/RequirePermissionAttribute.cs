namespace AzureDevOpsPRReviewAI.WebApi.Attributes
{
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string requiredPermission;

        public RequirePermissionAttribute(string requiredPermission)
        {
            this.requiredPermission = requiredPermission;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var userManagementService = context.HttpContext.RequestServices.GetService<IUserManagementService>();
            
            if (userManagementService == null)
            {
                context.Result = new StatusCodeResult(500); // Internal Server Error
                return;
            }

            // For now, use a simple approach - get user ID from header
            // In production, this should integrate with proper authentication middleware
            var userId = context.HttpContext.Request.Headers["X-User-Id"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(userId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Extract scope information from route values
            var organization = context.RouteData.Values["organization"]?.ToString();
            var project = context.RouteData.Values["project"]?.ToString();
            var repository = context.RouteData.Values["repository"]?.ToString();

            // Validate permissions
            var validationResult = await userManagementService.ValidatePermissionsAsync(
                userId, 
                this.requiredPermission, 
                organization, 
                project, 
                repository);

            if (!validationResult.HasPermission)
            {
                context.Result = new ForbidResult(validationResult.DenialReason ?? "Access denied");
                return;
            }

            // Store user information in HttpContext for use in controllers
            context.HttpContext.Items["UserId"] = userId;
            context.HttpContext.Items["UserPermissions"] = validationResult.UserPermissions;
        }
    }

    /// <summary>
    /// Extension methods for easier access to user information in controllers
    /// </summary>
    public static class HttpContextExtensions
    {
        public static string? GetCurrentUserId(this HttpContext httpContext)
        {
            return httpContext.Items["UserId"] as string;
        }

        public static List<string> GetCurrentUserPermissions(this HttpContext httpContext)
        {
            return (httpContext.Items["UserPermissions"] as List<string>) ?? new List<string>();
        }
    }
}