using System.Security.Cryptography;
using System.Text;

namespace SubscriptionTracker.AvaloniaApp;

public static class NameKeyUtil
{
    public static string Normalize(string name) => name.Trim().ToLowerInvariant();

    public static string HashNormalized(string name)
    {
        var norm = Normalize(name);
        var bytes = Encoding.UTF8.GetBytes(norm);
        var hash = SHA256.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
