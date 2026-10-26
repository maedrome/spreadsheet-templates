using Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Api.Compras;

/// <param name="ProductType"><c>plantilla</c> o <c>paquete</c>.</param>
public record PurchaseDto(
    string ProductType, string ProductName, string Email, int PricePaidCents, DateTimeOffset PaidAt, bool IsRefunded,
    List<PurchaseTemplateDto> Templates);

public record PurchaseTemplateDto(int Id, string Slug, string Name);

/// <summary>Lo que da acceso un Enlace de compra. Las descargas llegan en la fase 5.</summary>
public static class PurchaseEndpoints
{
    public static void MapPurchases(this RouteGroupBuilder group) =>
        group.MapGet("/{token}", Get).RequireRateLimiting(ComprasSetup.StatusRateLimit);

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
                    .Select(ot => new PurchaseTemplateDto(ot.Template.Id, ot.Template.Slug, ot.Template.Name))
                    .ToList(),
            })
            .SingleOrDefaultAsync(ct);
        if (order is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(new PurchaseDto(
            order.TemplateName is not null ? ProductTypes.Template : ProductTypes.Bundle,
            order.TemplateName ?? order.BundleName ?? "",
            order.Email, order.PricePaidCents, order.PaidAt, order.Status == OrderStatus.Refunded, order.Templates));
    }
}
