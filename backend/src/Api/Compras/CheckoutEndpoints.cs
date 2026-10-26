using Api.Catalogo;
using Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Compras;

public record CheckoutInput(string Tipo, string Slug);
public record CheckoutCreated(string Url);

/// <param name="Status"><c>pending</c> mientras Polar no confirme el pago; después, <c>paid</c>.</param>
/// <param name="Token">El token de un Enlace de compra nuevo, solo en la primera respuesta <c>paid</c>.</param>
public record CheckoutStatus(string Status, string? Token);

public static class CheckoutEndpoints
{
    public static void MapCheckout(this RouteGroupBuilder group)
    {
        group.MapPost("", Create).RequireRateLimiting(ComprasSetup.CheckoutRateLimit);
        group.MapGet("/{checkoutId}/estado", GetStatus).RequireRateLimiting(ComprasSetup.StatusRateLimit);
    }

    /// <summary>Inicia el pago de un Producto publicado y devuelve la URL del checkout alojado de Polar.</summary>
    private static async Task<Results<Ok<CheckoutCreated>, ProblemHttpResult, ValidationProblem>> Create(
        CheckoutInput input, AppDbContext db, IPaymentProvider payments, IOptions<SiteOptions> site, CancellationToken ct)
    {
        string? productId;
        int id;
        string returnPath;
        switch (input.Tipo)
        {
            case ProductTypes.Template:
                var template = await db.Templates.SingleOrDefaultAsync(t => t.Slug == input.Slug && t.IsPublished, ct);
                if (template is null)
                    return NotAvailable();
                // Publicadas antes de integrar Polar: el producto se crea en la primera compra.
                if (template.PolarProductId is null)
                {
                    await payments.SyncAsync(template, ct);
                    await db.SaveChangesAsync(ct);
                }
                (productId, id, returnPath) = (template.PolarProductId, template.Id, $"/plantillas/{template.Slug}");
                break;

            case ProductTypes.Bundle:
                var bundle = await db.Bundles.SingleOrDefaultAsync(b => b.Slug == input.Slug && b.IsPublished, ct);
                if (bundle is null)
                    return NotAvailable();
                if (bundle.PolarProductId is null)
                {
                    await payments.SyncAsync(bundle, ct);
                    await db.SaveChangesAsync(ct);
                }
                (productId, id, returnPath) = (bundle.PolarProductId, bundle.Id, $"/paquetes/{bundle.Slug}");
                break;

            default:
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["tipo"] = [$"El tipo tiene que ser \"{ProductTypes.Template}\" o \"{ProductTypes.Bundle}\"."],
                });
        }

        if (productId is null)
            throw new PaymentProviderException("El Producto todavía no tiene producto en Polar.");

        var session = await payments.CreateCheckoutAsync(new CheckoutSessionRequest(
            productId,
            site.Value.AbsoluteUrl($"/compra/confirmando?checkout={CheckoutSessionRequest.CheckoutIdPlaceholder}"),
            site.Value.AbsoluteUrl(returnPath),
            new Dictionary<string, string> { ["tipo"] = input.Tipo, ["producto_id"] = id.ToString(), ["slug"] = input.Slug }), ct);

        return TypedResults.Ok(new CheckoutCreated(session.Url));
    }

    /// <summary>
    /// Lo consulta la página de confirmación hasta que llega el webhook. La base de datos no guarda tokens en claro,
    /// así que la primera respuesta <c>paid</c> emite un Enlace de compra nuevo y las siguientes ya no traen token.
    /// </summary>
    private static async Task<Results<Ok<CheckoutStatus>, NotFound>> GetStatus(
        string checkoutId, AppDbContext db, TimeProvider time, HttpContext context, CancellationToken ct)
    {
        if (!Guid.TryParse(checkoutId, out _))
            return TypedResults.NotFound();
        context.Response.Headers.CacheControl = "no-store";

        var order = await db.Orders.SingleOrDefaultAsync(o => o.PolarCheckoutId == checkoutId, ct);
        if (order is null)
            return TypedResults.Ok(new CheckoutStatus("pending", null));

        var token = order.IssueConfirmationLink(time.GetUtcNow());
        if (token is not null)
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Otra consulta simultánea ganó y se llevó el Enlace.
                token = null;
            }
        }
        return TypedResults.Ok(new CheckoutStatus("paid", token));
    }

    private static ProblemHttpResult NotAvailable() =>
        TypedResults.Problem(detail: "El Producto no existe o no está a la venta.", statusCode: StatusCodes.Status404NotFound);
}
