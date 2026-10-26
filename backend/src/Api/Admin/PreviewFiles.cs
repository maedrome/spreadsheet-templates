using System.Text.RegularExpressions;

namespace Api.Admin;

/// <summary>
/// Sirve las Previews al panel, incluidas las de Plantillas sin publicar, sin depender del endpoint público del catálogo.
/// </summary>
public static partial class PreviewFiles
{
    public static string Url(string fileName) => $"/api/admin/previews/{fileName}";

    public static void MapAdminPreviewFiles(this RouteGroupBuilder group) =>
        group.MapGet("/previews/{fileName}", (string fileName, PreviewImages images) =>
        {
            var path = images.PathOf(fileName);
            return FileName().IsMatch(fileName) && File.Exists(path)
                ? Results.File(path, "image/webp")
                : Results.NotFound();
        });

    [GeneratedRegex("^[a-z0-9-]+\\.webp$")]
    private static partial Regex FileName();
}
