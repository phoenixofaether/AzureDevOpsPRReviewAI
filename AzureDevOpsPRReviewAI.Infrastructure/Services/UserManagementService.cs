namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class UserManagementService : IUserManagementService
    {
        private readonly ILogger<UserManagementService> logger;
        private readonly string userStoragePath;
        private readonly string activityStoragePath;
        private readonly JsonSerializerOptions jsonSerializerOptions;

        public UserManagementService(ILogger<UserManagementService> logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.userStoragePath = configuration["UserManagement:UserStoragePath"] ?? "./users";
            this.activityStoragePath = configuration["UserManagement:ActivityStoragePath"] ?? "./activity";
            
            this.jsonSerializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            this.EnsureStorageDirectoriesExist();
        }

        public async Task<User?> GetUserAsync(string userId)
        {
            try
            {
                var filePath = Path.Combine(this.userStoragePath, $"{userId}.json");
                
                if (!File.Exists(filePath))
                {
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var user = JsonSerializer.Deserialize<User>(json, this.jsonSerializerOptions);
                
                this.logger.LogDebug("Loaded user {UserId}", userId);
                return user;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to load user {UserId}", userId);
                return null;
            }
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            try
            {
                var users = await this.GetUsersAsync();
                return users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to find user by username {Username}", username);
                return null;
            }
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            try
            {
                var users = await this.GetUsersAsync();
                return users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to find user by email {Email}", email);
                return null;
            }
        }

        public async Task<List<User>> GetUsersAsync()
        {
            try
            {
                var users = new List<User>();
                var userFiles = Directory.GetFiles(this.userStoragePath, "*.json");
                
                foreach (var filePath in userFiles)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(filePath);
                        var user = JsonSerializer.Deserialize<User>(json, this.jsonSerializerOptions);
                        
                        if (user != null)
                        {
                            users.Add(user);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogWarning(ex, "Failed to load user from file: {FilePath}", filePath);
                    }
                }

                this.logger.LogDebug("Loaded {UserCount} users", users.Count);
                return users;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to load users");
                return new List<User>();
            }
        }

        public async Task<List<User>> GetUsersByOrganizationAsync(string organization)
        {
            try
            {
                var users = await this.GetUsersAsync();
                return users.Where(u => u.Organizations.Contains(organization, StringComparer.OrdinalIgnoreCase)).ToList();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to load users for organization {Organization}", organization);
                return new List<User>();
            }
        }

        public async Task<User> SaveUserAsync(User user)
        {
            try
            {
                // Generate ID if not provided
                if (string.IsNullOrEmpty(user.Id))
                {
                    user.Id = Guid.NewGuid().ToString();
                }

                user.UpdatedAt = DateTime.UtcNow;

                var filePath = Path.Combine(this.userStoragePath, $"{user.Id}.json");
                var json = JsonSerializer.Serialize(user, this.jsonSerializerOptions);
                
                await File.WriteAllTextAsync(filePath, json);

                this.logger.LogInformation("Saved user {UserId} ({Username})", user.Id, user.Username);
                return user;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to save user {UserId}", user.Id);
                throw;
            }
        }

        public async Task<bool> DeleteUserAsync(string userId)
        {
            try
            {
                var filePath = Path.Combine(this.userStoragePath, $"{userId}.json");
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    this.logger.LogInformation("Deleted user {UserId}", userId);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to delete user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> HasPermissionAsync(string userId, string permission, string? organization = null, string? project = null, string? repository = null)
        {
            var permissions = await this.GetUserPermissionsAsync(userId, organization, project, repository);
            return permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<List<string>> GetUserPermissionsAsync(string userId, string? organization = null, string? project = null, string? repository = null)
        {
            try
            {
                var user = await this.GetUserAsync(userId);
                if (user == null || !user.IsActive)
                {
                    return new List<string>();
                }

                var permissions = new List<string>();
                var defaultPermissions = Permissions.GetDefaultPermissions();

                foreach (var userRole in user.Roles)
                {
                    // Check if role applies to the requested scope
                    if (!this.RoleAppliesTo(userRole, organization, project, repository))
                    {
                        continue;
                    }

                    // Add role-specific permissions
                    permissions.AddRange(userRole.Permissions);

                    // Add default permissions for the role
                    if (Enum.TryParse<SystemRole>(userRole.Role, out var systemRole))
                    {
                        if (defaultPermissions.ContainsKey(systemRole))
                        {
                            permissions.AddRange(defaultPermissions[systemRole]);
                        }
                    }
                }

                return permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get user permissions for {UserId}", userId);
                return new List<string>();
            }
        }

        public async Task<bool> AssignRoleAsync(string userId, SystemRole role, string? organization = null, string? project = null, string? repository = null, string? assignedBy = null)
        {
            try
            {
                var user = await this.GetUserAsync(userId);
                if (user == null)
                {
                    return false;
                }

                // Check if role already exists
                var existingRole = user.Roles.FirstOrDefault(r => 
                    r.Role.Equals(role.ToString(), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.Organization, organization, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.Project, project, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.Repository, repository, StringComparison.OrdinalIgnoreCase));

                if (existingRole != null)
                {
                    this.logger.LogWarning("Role {Role} already exists for user {UserId} in scope {Organization}/{Project}/{Repository}", 
                        role, userId, organization, project, repository);
                    return true; // Already exists
                }

                var defaultPermissions = Permissions.GetDefaultPermissions();
                var permissions = defaultPermissions.ContainsKey(role) ? defaultPermissions[role] : new List<string>();

                var userRole = new UserRole
                {
                    Role = role.ToString(),
                    Organization = organization,
                    Project = project,
                    Repository = repository,
                    Permissions = permissions,
                    GrantedBy = assignedBy
                };

                user.Roles.Add(userRole);
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = assignedBy;

                await this.SaveUserAsync(user);

                this.logger.LogInformation("Assigned role {Role} to user {UserId} in scope {Organization}/{Project}/{Repository}", 
                    role, userId, organization, project, repository);

                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to assign role {Role} to user {UserId}", role, userId);
                return false;
            }
        }

        public async Task<bool> RemoveRoleAsync(string userId, SystemRole role, string? organization = null, string? project = null, string? repository = null)
        {
            try
            {
                var user = await this.GetUserAsync(userId);
                if (user == null)
                {
                    return false;
                }

                var roleToRemove = user.Roles.FirstOrDefault(r => 
                    r.Role.Equals(role.ToString(), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.Organization, organization, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.Project, project, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.Repository, repository, StringComparison.OrdinalIgnoreCase));

                if (roleToRemove == null)
                {
                    return false;
                }

                user.Roles.Remove(roleToRemove);
                user.UpdatedAt = DateTime.UtcNow;

                await this.SaveUserAsync(user);

                this.logger.LogInformation("Removed role {Role} from user {UserId} in scope {Organization}/{Project}/{Repository}", 
                    role, userId, organization, project, repository);

                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to remove role {Role} from user {UserId}", role, userId);
                return false;
            }
        }

        public async Task<List<User>> GetUsersByRoleAsync(SystemRole role, string? organization = null, string? project = null, string? repository = null)
        {
            try
            {
                var users = await this.GetUsersAsync();
                return users.Where(u => u.Roles.Any(r => 
                    r.Role.Equals(role.ToString(), StringComparison.OrdinalIgnoreCase) &&
                    this.RoleAppliesTo(r, organization, project, repository))).ToList();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get users by role {Role}", role);
                return new List<User>();
            }
        }

        public async Task<User?> AuthenticateAsync(string username, string password)
        {
            try
            {
                var user = await this.GetUserByUsernameAsync(username);
                if (user == null || !user.IsActive)
                {
                    return null;
                }

                // For file-based implementation, we'll use a simple hash check
                // In production, use proper password hashing (bcrypt, scrypt, etc.)
                var hashedPassword = this.HashPassword(password);
                var storedHash = user.Metadata.GetValueOrDefault("PasswordHash") as string;

                if (storedHash == hashedPassword)
                {
                    await this.RecordUserLoginAsync(user.Id);
                    return user;
                }

                return null;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Authentication failed for user {Username}", username);
                return null;
            }
        }

        public async Task<User> CreateUserAsync(string username, string email, string? displayName = null, string? createdBy = null)
        {
            try
            {
                // Check if user already exists
                var existingUser = await this.GetUserByUsernameAsync(username);
                if (existingUser != null)
                {
                    throw new ArgumentException($"User with username '{username}' already exists");
                }

                existingUser = await this.GetUserByEmailAsync(email);
                if (existingUser != null)
                {
                    throw new ArgumentException($"User with email '{email}' already exists");
                }

                var user = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = username,
                    Email = email,
                    DisplayName = displayName ?? username,
                    IsActive = true,
                    CreatedBy = createdBy,
                    UpdatedBy = createdBy
                };

                return await this.SaveUserAsync(user);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to create user {Username}", username);
                throw;
            }
        }

        public async Task<bool> UpdateUserPreferencesAsync(string userId, UserPreferences preferences)
        {
            try
            {
                var user = await this.GetUserAsync(userId);
                if (user == null)
                {
                    return false;
                }

                user.Preferences = preferences;
                user.UpdatedAt = DateTime.UtcNow;

                await this.SaveUserAsync(user);
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to update preferences for user {UserId}", userId);
                return false;
            }
        }

        public async Task RecordUserLoginAsync(string userId)
        {
            try
            {
                var user = await this.GetUserAsync(userId);
                if (user != null)
                {
                    user.LastLoginAt = DateTime.UtcNow;
                    await this.SaveUserAsync(user);
                }

                // Record activity
                await this.RecordUserActivityAsync(userId, "login", "User logged in");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to record login for user {UserId}", userId);
            }
        }

        public async Task<List<UserActivity>> GetUserActivityAsync(string userId, DateTime? from = null, DateTime? to = null)
        {
            try
            {
                var activities = new List<UserActivity>();
                var activityFiles = Directory.GetFiles(this.activityStoragePath, $"{userId}_*.json");
                
                foreach (var filePath in activityFiles)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(filePath);
                        var activity = JsonSerializer.Deserialize<UserActivity>(json, this.jsonSerializerOptions);
                        
                        if (activity != null)
                        {
                            if (from.HasValue && activity.Timestamp < from.Value) continue;
                            if (to.HasValue && activity.Timestamp > to.Value) continue;
                            
                            activities.Add(activity);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogWarning(ex, "Failed to load activity from file: {FilePath}", filePath);
                    }
                }

                return activities.OrderByDescending(a => a.Timestamp).ToList();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get user activity for {UserId}", userId);
                return new List<UserActivity>();
            }
        }

        public async Task<PermissionValidationResult> ValidatePermissionsAsync(string userId, string requiredPermission, string? organization = null, string? project = null, string? repository = null)
        {
            try
            {
                var user = await this.GetUserAsync(userId);
                if (user == null)
                {
                    return PermissionValidationResult.Deny("User not found");
                }

                if (!user.IsActive)
                {
                    return PermissionValidationResult.Deny("User account is inactive");
                }

                var userPermissions = await this.GetUserPermissionsAsync(userId, organization, project, repository);
                var hasPermission = userPermissions.Contains(requiredPermission, StringComparer.OrdinalIgnoreCase);

                if (hasPermission)
                {
                    return PermissionValidationResult.Allow();
                }

                return PermissionValidationResult.Deny($"User does not have required permission: {requiredPermission}");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to validate permissions for user {UserId}", userId);
                return PermissionValidationResult.Deny("Permission validation failed");
            }
        }

        private bool RoleAppliesTo(UserRole userRole, string? organization, string? project, string? repository)
        {
            // Global roles (no scope restrictions) apply everywhere
            if (string.IsNullOrEmpty(userRole.Organization) && string.IsNullOrEmpty(userRole.Project) && string.IsNullOrEmpty(userRole.Repository))
            {
                return true;
            }

            // Organization-specific roles
            if (!string.IsNullOrEmpty(userRole.Organization) && string.IsNullOrEmpty(userRole.Project) && string.IsNullOrEmpty(userRole.Repository))
            {
                return string.Equals(userRole.Organization, organization, StringComparison.OrdinalIgnoreCase);
            }

            // Project-specific roles
            if (!string.IsNullOrEmpty(userRole.Organization) && !string.IsNullOrEmpty(userRole.Project) && string.IsNullOrEmpty(userRole.Repository))
            {
                return string.Equals(userRole.Organization, organization, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(userRole.Project, project, StringComparison.OrdinalIgnoreCase);
            }

            // Repository-specific roles
            if (!string.IsNullOrEmpty(userRole.Organization) && !string.IsNullOrEmpty(userRole.Project) && !string.IsNullOrEmpty(userRole.Repository))
            {
                return string.Equals(userRole.Organization, organization, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(userRole.Project, project, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(userRole.Repository, repository, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private async Task RecordUserActivityAsync(string userId, string action, string? description = null, string? organization = null, string? project = null, string? repository = null)
        {
            try
            {
                var activity = new UserActivity
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Action = action,
                    Description = description,
                    Organization = organization,
                    Project = project,
                    Repository = repository
                };

                var fileName = $"{userId}_{DateTime.UtcNow:yyyyMMdd}_{activity.Id}.json";
                var filePath = Path.Combine(this.activityStoragePath, fileName);
                var json = JsonSerializer.Serialize(activity, this.jsonSerializerOptions);
                
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to record user activity for {UserId}", userId);
            }
        }

        private string HashPassword(string password)
        {
            // Simple hash for demonstration - use proper password hashing in production
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "SALT_STRING"));
            return Convert.ToBase64String(hashedBytes);
        }

        private void EnsureStorageDirectoriesExist()
        {
            if (!Directory.Exists(this.userStoragePath))
            {
                Directory.CreateDirectory(this.userStoragePath);
                this.logger.LogInformation("Created user storage directory: {Path}", this.userStoragePath);
            }

            if (!Directory.Exists(this.activityStoragePath))
            {
                Directory.CreateDirectory(this.activityStoragePath);
                this.logger.LogInformation("Created activity storage directory: {Path}", this.activityStoragePath);
            }
        }
    }
}