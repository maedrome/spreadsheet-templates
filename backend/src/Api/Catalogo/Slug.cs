using System.Text.RegularExpressions;
using Api.Common;

namespace Api.Catalogo;

public static partial class Slug
{
    public const int MaxLength = 100;

    public static string EnsureValid(string slug)
    {
        if (slug.Length > MaxLength || !Format().IsMatch(slug))
            throw new DomainException($"El slug \"{slug}\" no es válido: usa solo minúsculas, números y guiones.");
        return slug;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Format();
}
