using System;
using System.Security.Cryptography;
using System.Text;

namespace SLSKDONET.Services;

/// <summary>
/// Provides methods to encrypt and decrypt data using the Windows Data Protection API (DPAPI).
/// The data is encrypted for the current user account, so only this user can decrypt it.
/// </summary>
public class ProtectedDataService
{
    // Secure implementation using Windows DPAPI
    public string? Protect(string? data)
    {
        if (string.IsNullOrEmpty(data))
            return null;
            
        try
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            // Protect bytes for Current User
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }
        catch (Exception)
        {
            // If encryption fails, fallback or return null
            return null;
        }
    }

    public string? Unprotect(string? encryptedData)
    {
        if (string.IsNullOrEmpty(encryptedData))
            return null;
            
        try
        {
            var bytes = Convert.FromBase64String(encryptedData);
            // Unprotect bytes
            var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            // Failed to decrypt (e.g. wrong user context or corrupted data)
            return null;
        }
    }
}