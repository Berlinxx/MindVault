using System.Text.RegularExpressions;

namespace mindvault.Services;

/// <summary>
/// Validates password strength and provides feedback for export file protection.
/// </summary>
public static class PasswordValidationService
{
    // Minimum requirements
    public const int MinLength = 8;
    public const int MaxLength = 128;
    
    /// <summary>
    /// Password strength levels
    /// </summary>
    public enum PasswordStrength
    {
        TooShort,
        Weak,
        Fair,
        Strong,
        VeryStrong
    }
    
    /// <summary>
    /// Validation result with detailed feedback
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public PasswordStrength Strength { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Suggestions { get; set; } = new();
        public string StrengthText => GetStrengthText(Strength);
        public string StrengthColor => GetStrengthColor(Strength);
        
        private static string GetStrengthText(PasswordStrength strength) => strength switch
        {
            PasswordStrength.TooShort => "Too Short",
            PasswordStrength.Weak => "Weak",
            PasswordStrength.Fair => "Fair",
            PasswordStrength.Strong => "Strong",
            PasswordStrength.VeryStrong => "Very Strong",
            _ => "Unknown"
        };
        
        private static string GetStrengthColor(PasswordStrength strength) => strength switch
        {
            PasswordStrength.TooShort => "#E74C3C", // Red
            PasswordStrength.Weak => "#E74C3C",     // Red
            PasswordStrength.Fair => "#F39C12",     // Orange
            PasswordStrength.Strong => "#27AE60",   // Green
            PasswordStrength.VeryStrong => "#16A085", // Teal
            _ => "#666666"
        };
    }
    
    /// <summary>
    /// Validates a password and returns detailed results
    /// </summary>
    public static ValidationResult Validate(string? password)
    {
        var result = new ValidationResult();
        
        if (string.IsNullOrEmpty(password))
        {
            result.IsValid = false;
            result.Strength = PasswordStrength.TooShort;
            result.Errors.Add("Password cannot be empty");
            return result;
        }
        
        // Check minimum length
        if (password.Length < MinLength)
        {
            result.IsValid = false;
            result.Strength = PasswordStrength.TooShort;
            result.Errors.Add($"Password must be at least {MinLength} characters");
            result.Suggestions.Add($"Add {MinLength - password.Length} more character{(MinLength - password.Length > 1 ? "s" : "")}");
            return result;
        }
        
        // Check maximum length
        if (password.Length > MaxLength)
        {
            result.IsValid = false;
            result.Strength = PasswordStrength.TooShort;
            result.Errors.Add($"Password cannot exceed {MaxLength} characters");
            return result;
        }
        
        // Calculate strength score
        int score = 0;
        var suggestions = new List<string>();
        
        // Length bonuses
        if (password.Length >= 8) score++;
        if (password.Length >= 12) score++;
        if (password.Length >= 16) score++;
        
        // Character type checks
        bool hasLower = Regex.IsMatch(password, "[a-z]");
        bool hasUpper = Regex.IsMatch(password, "[A-Z]");
        bool hasDigit = Regex.IsMatch(password, "[0-9]");
        bool hasSpecial = Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?~`]");
        
        if (hasLower) score++;
        else suggestions.Add("Add lowercase letters (a-z)");
        
        if (hasUpper) score++;
        else suggestions.Add("Add uppercase letters (A-Z)");
        
        if (hasDigit) score++;
        else suggestions.Add("Add numbers (0-9)");
        
        if (hasSpecial) score++;
        else suggestions.Add("Add special characters (!@#$%^&*)");
        
        // Check for common patterns (weak)
        bool hasCommonPattern = false;
        
        // Sequential characters
        if (Regex.IsMatch(password.ToLower(), @"(abc|bcd|cde|def|efg|fgh|ghi|hij|ijk|jkl|klm|lmn|mno|nop|opq|pqr|qrs|rst|stu|tuv|uvw|vwx|wxy|xyz)"))
        {
            hasCommonPattern = true;
            suggestions.Add("Avoid sequential letters (abc, xyz)");
        }
        
        // Sequential numbers
        if (Regex.IsMatch(password, @"(012|123|234|345|456|567|678|789|890)"))
        {
            hasCommonPattern = true;
            suggestions.Add("Avoid sequential numbers (123, 456)");
        }
        
        // Repeated characters
        if (Regex.IsMatch(password, @"(.)\1{2,}"))
        {
            hasCommonPattern = true;
            suggestions.Add("Avoid repeated characters (aaa, 111)");
        }
        
        // Common words (basic check)
        var lowerPassword = password.ToLower();
        string[] commonWords = { "password", "123456", "qwerty", "admin", "letmein", "welcome", "monkey", "dragon", "master", "login" };
        if (commonWords.Any(w => lowerPassword.Contains(w)))
        {
            hasCommonPattern = true;
            score = Math.Max(0, score - 2);
            suggestions.Add("Avoid common words like 'password'");
        }
        
        if (hasCommonPattern) score = Math.Max(0, score - 1);
        
        // Determine strength
        result.Strength = score switch
        {
            <= 2 => PasswordStrength.Weak,
            3 or 4 => PasswordStrength.Fair,
            5 or 6 => PasswordStrength.Strong,
            _ => PasswordStrength.VeryStrong
        };
        
        // Password is valid if it meets minimum requirements
        // We require at least Fair strength for export protection
        result.IsValid = result.Strength >= PasswordStrength.Fair;
        
        if (!result.IsValid)
        {
            result.Errors.Add("Password is too weak for secure export protection");
        }
        
        result.Suggestions = suggestions.Take(3).ToList(); // Limit suggestions
        
        return result;
    }
    
    /// <summary>
    /// Gets a formatted requirements string for display
    /// </summary>
    public static string GetRequirementsText()
    {
        return $"• At least {MinLength} characters\n" +
               "• Mix of uppercase and lowercase letters\n" +
               "• At least one number\n" +
               "• Special characters recommended (!@#$%^&*)";
    }
    
    /// <summary>
    /// Gets a short hint for the password field
    /// </summary>
    public static string GetShortHint()
    {
        return $"Min {MinLength} chars, include uppercase, lowercase & numbers";
    }
}
