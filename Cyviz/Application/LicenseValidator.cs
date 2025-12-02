using System.Security.Cryptography;
using System.Text;

namespace Cyviz.Application;

public static class LicenseValidator
{
    private const string RequiredDeveloperLink = "https://www.linkedin.com/in/goran-bacvar-50345836/";
    private const string RequiredHash = "062E5A4DE6091D5B110776FBFF51E3BCA1AABF6937D814DD15EF7C2B014E1185";
    
    public static bool ValidateLicense(string linkToValidate)
    {
        if (string.IsNullOrWhiteSpace(linkToValidate))
        {
            return false;
        }
        
        var hash = ComputeHash(linkToValidate);
        return hash.Equals(RequiredHash, StringComparison.OrdinalIgnoreCase);
    }
    
    public static string GetDeveloperLink() => RequiredDeveloperLink;
    
    private static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hashBytes).Replace("-", "");
    }
    
    public static void ValidateOnStartup()
    {
        if (!ValidateLicense(RequiredDeveloperLink))
        {
            throw new InvalidOperationException(
                "Application license validation failed. Developer attribution has been removed or modified. " +
                "Application cannot start without proper attribution.");
        }
    }
}
