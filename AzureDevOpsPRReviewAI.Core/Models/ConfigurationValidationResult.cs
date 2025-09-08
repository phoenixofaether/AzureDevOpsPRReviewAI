namespace AzureDevOpsPRReviewAI.Core.Models
{
    public class ConfigurationValidationResult
    {
        public bool IsValid { get; set; }

        public List<ConfigurationValidationError> Errors { get; set; } = new();

        public List<ConfigurationValidationWarning> Warnings { get; set; } = new();

        public string? Summary { get; set; }

        public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;

        public static ConfigurationValidationResult Success()
        {
            return new ConfigurationValidationResult { IsValid = true };
        }

        public static ConfigurationValidationResult Failure(params ConfigurationValidationError[] errors)
        {
            return new ConfigurationValidationResult
            {
                IsValid = false,
                Errors = errors.ToList()
            };
        }

        public static ConfigurationValidationResult Failure(params string[] errorMessages)
        {
            return new ConfigurationValidationResult
            {
                IsValid = false,
                Errors = errorMessages.Select(msg => new ConfigurationValidationError
                {
                    Message = msg,
                    Severity = ValidationSeverity.Error
                }).ToList()
            };
        }
    }

    public class ConfigurationValidationError
    {
        public required string Message { get; set; }

        public string? PropertyPath { get; set; }

        public string? ErrorCode { get; set; }

        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;

        public object? Value { get; set; }

        public Dictionary<string, object> Context { get; set; } = new();
    }

    public class ConfigurationValidationWarning
    {
        public required string Message { get; set; }

        public string? PropertyPath { get; set; }

        public string? WarningCode { get; set; }

        public object? Value { get; set; }

        public Dictionary<string, object> Context { get; set; } = new();
    }

    public enum ValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Critical = 3
    }
}