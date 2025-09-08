namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    using AzureDevOpsPRReviewAI.Core.Models;

    public interface IUserManagementService
    {
        /// <summary>
        /// Gets a user by ID
        /// </summary>
        Task<User?> GetUserAsync(string userId);

        /// <summary>
        /// Gets a user by username
        /// </summary>
        Task<User?> GetUserByUsernameAsync(string username);

        /// <summary>
        /// Gets a user by email
        /// </summary>
        Task<User?> GetUserByEmailAsync(string email);

        /// <summary>
        /// Gets all users
        /// </summary>
        Task<List<User>> GetUsersAsync();

        /// <summary>
        /// Gets users by organization
        /// </summary>
        Task<List<User>> GetUsersByOrganizationAsync(string organization);

        /// <summary>
        /// Creates or updates a user
        /// </summary>
        Task<User> SaveUserAsync(User user);

        /// <summary>
        /// Deletes a user
        /// </summary>
        Task<bool> DeleteUserAsync(string userId);

        /// <summary>
        /// Checks if a user has a specific permission
        /// </summary>
        Task<bool> HasPermissionAsync(string userId, string permission, string? organization = null, string? project = null, string? repository = null);

        /// <summary>
        /// Gets all permissions for a user in a specific scope
        /// </summary>
        Task<List<string>> GetUserPermissionsAsync(string userId, string? organization = null, string? project = null, string? repository = null);

        /// <summary>
        /// Assigns a role to a user
        /// </summary>
        Task<bool> AssignRoleAsync(string userId, SystemRole role, string? organization = null, string? project = null, string? repository = null, string? assignedBy = null);

        /// <summary>
        /// Removes a role from a user
        /// </summary>
        Task<bool> RemoveRoleAsync(string userId, SystemRole role, string? organization = null, string? project = null, string? repository = null);

        /// <summary>
        /// Gets users with a specific role
        /// </summary>
        Task<List<User>> GetUsersByRoleAsync(SystemRole role, string? organization = null, string? project = null, string? repository = null);

        /// <summary>
        /// Authenticates a user (basic implementation)
        /// </summary>
        Task<User?> AuthenticateAsync(string username, string password);

        /// <summary>
        /// Creates a new user account
        /// </summary>
        Task<User> CreateUserAsync(string username, string email, string? displayName = null, string? createdBy = null);

        /// <summary>
        /// Updates user preferences
        /// </summary>
        Task<bool> UpdateUserPreferencesAsync(string userId, UserPreferences preferences);

        /// <summary>
        /// Records user login
        /// </summary>
        Task RecordUserLoginAsync(string userId);

        /// <summary>
        /// Gets user activity/audit log
        /// </summary>
        Task<List<UserActivity>> GetUserActivityAsync(string userId, DateTime? from = null, DateTime? to = null);

        /// <summary>
        /// Validates user permissions for a specific action
        /// </summary>
        Task<PermissionValidationResult> ValidatePermissionsAsync(string userId, string requiredPermission, string? organization = null, string? project = null, string? repository = null);
    }

    public class UserActivity
    {
        public required string Id { get; set; }

        public required string UserId { get; set; }

        public required string Action { get; set; }

        public string? Description { get; set; }

        public string? Organization { get; set; }

        public string? Project { get; set; }

        public string? Repository { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class PermissionValidationResult
    {
        public bool HasPermission { get; set; }

        public string? DenialReason { get; set; }

        public List<string> RequiredRoles { get; set; } = new();

        public List<string> UserRoles { get; set; } = new();

        public List<string> UserPermissions { get; set; } = new();

        public static PermissionValidationResult Allow()
        {
            return new PermissionValidationResult { HasPermission = true };
        }

        public static PermissionValidationResult Deny(string reason)
        {
            return new PermissionValidationResult { HasPermission = false, DenialReason = reason };
        }
    }
}