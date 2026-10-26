using System.Text.RegularExpressions;
using Api.Common;
using Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Catalogo;

public static partial class CatalogoEndpoints
{
    public static IEndpointRouteBuilder MapCatalogo(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalogo", GetCatalogo);
        app.MapGet("/api/plantillas/{slug}", GetTemplate);
        app.MapGet("/api/paquetes/{slug}", GetBundle);
        app.MapGet("/api/previews/{fileName}", GetPreview);
        return app;
    }

    private static async Task<Ok<CatalogoDto>> GetCatalogo(AppDbContext db, CancellationToken ct)
    {
        var bundles = await db.Bundles.AsNoTracking().AsSplitQuery()
            .Where(b => b.IsPublished)
            .Include(b => b.Templates).ThenInclude(bt => bt.Template).ThenInclude(t => t.Previews)
            .OrderBy(b => b.Position).ThenBy(b => b.Id)
            .ToListAsync(ct);

        var templates = await db.Templates.AsNoTracking()
            .Where(t => t.IsPublished)
            .Include(t => t.Previews)
            .OrderBy(t => t.Position).ThenBy(t => t.Id)
            .ToListAsync(ct);

        return TypedResults.Ok(new CatalogoDto(
            bundles.Select(b => b.ToCard()).ToList(),
            templates.Select(t => t.ToCard()).ToList()));
    }

    private static async Task<Results<Ok<TemplateDetailDto>, NotFound>> GetTemplate(
        string slug, AppDbContext db, CancellationToken ct)
    {
        var template = await db.Templates.AsNoTracking()
            .Include(t => t.Previews)
            .FirstOrDefaultAsync(t => t.Slug == slug && t.IsPublished, ct);

        return template is null ? TypedResults.NotFound() : TypedResults.Ok(template.ToDetail());
    }

    private static async Task<Results<Ok<BundleDetailDto>, NotFound>> GetBundle(
        string slug, AppDbContext db, CancellationToken ct)
    {
        var bundle = await db.Bundles.AsNoTracking().AsSplitQuery()
            .Include(b => b.Templates).ThenInclude(bt => bt.Template).ThenInclude(t => t.Previews)
            .FirstOrDefaultAsync(b => b.Slug == slug && b.IsPublished, ct);

        return bundle is null ? TypedResults.NotFound() : TypedResults.Ok(bundle.ToDetail());
    }

    private static IResult GetPreview(string fileName, IOptions<StorageOptions> storage, HttpContext context)
    {
        if (!PreviewFileName().IsMatch(fileName))
            return TypedResults.NotFound();

        var path = Path.Combine(storage.Value.PreviewsPath, fileName);
        if (!File.Exists(path))
            return TypedResults.NotFound();

        context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return TypedResults.PhysicalFile(path, "image/webp");
    }

    [GeneratedRegex("^[a-z0-9-]+\\.webp$")]
    private static partial Regex PreviewFileName();
}
