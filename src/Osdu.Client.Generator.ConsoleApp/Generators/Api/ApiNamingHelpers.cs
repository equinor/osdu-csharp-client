using Osdu.Client.Generator.ConsoleApp.Extensions;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Api;

/// <summary>
/// Naming and sanitization helpers for API client generation.
/// </summary>
public static class ApiNamingHelpers
{
    public static string SanitizePath(string path) =>
        string.Concat(path.Split(['/', '{', '}', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => char.ToUpperInvariant(s[0]) + s[1..]));

    public static string SanitizeParamName(string name)
    {
        var pascal = name.ToPascalCase();
        if (pascal.Length > 0)
            pascal = char.ToLowerInvariant(pascal[0]) + pascal[1..];
        // Avoid C# keywords
        return pascal switch
        {
            "string" or "object" or "default" or "class" or "new" => $"@{pascal}",
            _ => pascal
        };
    }

    public static string EscapeXml(string text)
        => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
