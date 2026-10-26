using Api.Common;
using Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Compras;

/// <param name="ProductType"><c>plantilla</c> o <c>paquete</c>.</param>
public record PurchaseDto(
    string ProductType, string ProductName, string Email, int PricePaidCents, DateTimeOffset PaidAt, bool IsRefunded,
    List<PurchaseTemplateDto> Templates);

/// <param name="GoogleSheetsCopyUrl">Nulo si la Compra está Reembolsada.</param>
/// <param name="DownloadUrl">Nulo si la Compra está Reembolsada.</param>
public record PurchaseTemplateDto(int Id, string Slug, string Name, string? GoogleSheetsCopyUrl, string? DownloadUrl);

/// <summary>Lo que da acceso un Enlace de compra: el resumen y la descarga del archivo vigente de cada Plantilla.</summary>
public static class PurchaseEndpoints
{
    public static void MapPurchases(this RouteGroupBuilder group)
    {
        group.MapGet("/{token}", Get).RequireRateLimiting(ComprasSetup.StatusRateLimit);
        group.MapGet("/{token}/plantillas/{templateId:int}/xlsx", Download).RequireRateLimiting(ComprasSetup.StatusRateLimit);
    }

    private static string DownloadUrl(string token, int templateId) =>
        $"/api/compras/{Uri.EscapeDataString(token)}/plantillas/{templateId}/xlsx";

    /// <summary>El archivo vigente de una Plantilla del snapshot de la Compra, no el que había al pagar.</summary>
    private static async Task<Results<PhysicalFileHttpResult, NotFound, StatusCodeHttpResult>> Download(
        string token, int templateId, AppDbContext db, IOptions<StorageOptions> storage, HttpContext context, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Robots-Tag"] = "noindex";

        var hash = PurchaseLink.HashToken(token);
        var found = await db.PurchaseLinks.AsNoTracking()
            .Where(pl => pl.TokenHash == hash)
            .SelectMany(pl => pl.Order.Templates, (pl, ot) => new { pl.Order.Status, ot.Template.Slug, ot.Template.XlsxPath, ot.TemplateId })
            .Where(x => x.TemplateId == templateId)
            .SingleOrDefaultAsync(ct);
        if (found is null)
            return TypedResults.NotFound();
        if (found.Status == OrderStatus.Refunded)
            return TypedResults.StatusCode(StatusCodes.Status410Gone);
        if (string.IsNullOrWhiteSpace(found.XlsxPath))
            return TypedResults.NotFound();

        // Defensa adicional: la ruta resuelta no puede salir de la carpeta de plantillas.
        var root = Path.GetFullPath(storage.Value.TemplatesPath);
        var path = Path.GetFullPath(found.XlsxPath, root);
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(path))
            return TypedResults.NotFound();

        return TypedResults.PhysicalFile(path, XlsxContentType, $"{found.Slug}.xlsx");
    }

    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static async Task<Results<Ok<PurchaseDto>, NotFound>> Get(string token, AppDbContext db, HttpContext context, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Robots-Tag"] = "noindex";

        var hash = PurchaseLink.HashToken(token);
        var order = await db.PurchaseLinks.AsNoTracking()
            .Where(pl => pl.TokenHash == hash)
            .Select(pl => pl.Order)
            .Select(o => new
            {
                o.Email, o.PricePaidCents, o.PaidAt, o.Status,
                TemplateName = o.Template != null ? o.Template.Name : null,
                BundleName = o.Bundle != null ? o.Bundle.Name : null,
                Templates = o.Templates
                    .OrderBy(ot => ot.Template.Name)
                    .Select(ot => new { ot.Template.Id, ot.Template.Slug, ot.Template.Name, ot.Template.GoogleSheetsCopyUrl })
                    .ToList(),
            })
            .SingleOrDefaultAsync(ct);
        if (order is null)
            return TypedResults.NotFound();

        var refunded = order.Status == OrderStatus.Refunded;
        var templates = order.Templates
            .Select(t => new PurchaseTemplateDto(
                t.Id, t.Slug, t.Name,
                refunded ? null : t.GoogleSheetsCopyUrl,
                refunded ? null : DownloadUrl(token, t.Id)))
            .ToList();
        return TypedResults.Ok(new PurchaseDto(
            order.TemplateName is not null ? ProductTypes.Template : ProductTypes.Bundle,
            order.TemplateName ?? order.BundleName ?? "",
            order.Email, order.PricePaidCents, order.PaidAt, refunded, templates));
    }
}
