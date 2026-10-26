using System.Text.Json;
using Api.Catalogo;
using Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Compras;

/// <summary>Un evento de Polar: <c>{ "type": "order.paid", "timestamp": "...", "data": { ... } }</c>.</summary>
public record PolarEvent(string Type, JsonElement Data);

/// <summary>Los campos de la orden de Polar que usa la tienda.</summary>
/// <param name="NetAmount">Lo pagado por el Producto, con descuentos y sin impuestos (que se quedan con Polar como Merchant of Record).</param>
/// <param name="Status"><c>paid</c>, <c>refunded</c> o <c>partially_refunded</c>, entre otros.</param>
public record PolarOrder(
    string Id, string Status, int NetAmount, string? CheckoutId, string? ProductId,
    Dictionary<string, JsonElement>? Metadata, PolarCustomer? Customer);

public record PolarCustomer(string? Email);

public static class PolarWebhookEndpoints
{
    private const int MaxBodyBytes = 1024 * 1024;

    public static void MapPolarWebhook(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/webhooks/polar", Receive);

    private static async Task<IResult> Receive(HttpRequest request, StandardWebhook webhook, PolarWebhookHandler handler, ILogger<PolarWebhookHandler> logger, CancellationToken ct)
    {
        if (request.ContentLength > MaxBodyBytes)
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);

        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, ct);
        if (buffer.Length > MaxBodyBytes)
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        var body = buffer.ToArray();

        var headers = request.Headers;
        if (!webhook.IsValid(headers[StandardWebhook.IdHeader], headers[StandardWebhook.TimestampHeader], headers[StandardWebhook.SignatureHeader], body))
        {
            logger.LogWarning("Webhook de Polar con firma inválida o vencida ({WebhookId}).", headers[StandardWebhook.IdHeader].ToString());
            return TypedResults.Problem("Firma inválida.", statusCode: StatusCodes.Status401Unauthorized);
        }

        PolarEvent? polarEvent;
        try
        {
            polarEvent = JsonSerializer.Deserialize<PolarEvent>(body, PolarPaymentProvider.Json);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Webhook de Polar con JSON inválido.");
            return TypedResults.BadRequest();
        }

        switch (polarEvent?.Type)
        {
            case "order.paid":
                await handler.OrderPaidAsync(polarEvent.Data.Deserialize<PolarOrder>(PolarPaymentProvider.Json)!, ct);
                break;
            case "order.refunded":
                await handler.OrderRefundedAsync(polarEvent.Data.Deserialize<PolarOrder>(PolarPaymentProvider.Json)!, ct);
                break;
        }

        // Cualquier otra respuesta hace que Polar reintente y, tras 10 fallos seguidos, desactive el endpoint.
        return TypedResults.Accepted((string?)null);
    }
}

public sealed class PolarWebhookHandler(AppDbContext db, PurchaseEmails emails, TimeProvider time, ILogger<PolarWebhookHandler> logger)
{
    /// <summary>
    /// Crea la Compra una sola vez por orden de Polar, la guarda y recién después envía el email: si el email falla,
    /// la Compra queda y se puede reenviar desde el admin.
    /// </summary>
    public async Task OrderPaidAsync(PolarOrder polar, CancellationToken ct)
    {
        if (await db.Orders.AnyAsync(o => o.PolarOrderId == polar.Id, ct))
        {
            logger.LogInformation("La orden de Polar {PolarOrderId} ya tiene Compra: se ignora el webhook repetido.", polar.Id);
            return;
        }

        var email = polar.Customer?.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogError("La orden de Polar {PolarOrderId} no trae el email del cliente: no se creó la Compra.", polar.Id);
            return;
        }

        var now = time.GetUtcNow();
        Order order;
        string productName;
        switch (await FindProductAsync(polar, ct))
        {
            case (Template template, _):
                order = Order.ForTemplate(email, template, polar.NetAmount, polar.Id, now, polar.CheckoutId);
                productName = template.Name;
                break;
            case (_, Bundle bundle):
                order = Order.ForBundle(email, bundle, polar.NetAmount, polar.Id, now, polar.CheckoutId);
                productName = bundle.Name;
                break;
            default:
                logger.LogError(
                    "La orden de Polar {PolarOrderId} (producto {PolarProductId}) no corresponde a ningún Producto: no se creó la Compra.",
                    polar.Id, polar.ProductId);
                return;
        }

        var token = order.IssuePurchaseLink(now);
        db.Orders.Add(order);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            logger.LogInformation("La orden de Polar {PolarOrderId} llegó dos veces a la vez: se guardó una sola Compra.", polar.Id);
            return;
        }
        logger.LogInformation("Compra {OrderId} creada para la orden de Polar {PolarOrderId}.", order.Id, polar.Id);

        try
        {
            await emails.SendPurchaseLinkAsync(order, productName, token, ct);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "No se pudo enviar el email de la Compra {OrderId}. Reenvíalo desde el admin.", order.Id);
        }
    }

    /// <summary>Solo un reembolso total deja la Compra Reembolsada; uno parcial mantiene el acceso.</summary>
    public async Task OrderRefundedAsync(PolarOrder polar, CancellationToken ct)
    {
        var order = await db.Orders.SingleOrDefaultAsync(o => o.PolarOrderId == polar.Id, ct);
        if (order is null)
        {
            logger.LogWarning("Reembolso de la orden de Polar {PolarOrderId}, que no tiene Compra.", polar.Id);
            return;
        }
        if (polar.Status != "refunded")
        {
            logger.LogInformation("Reembolso parcial de la Compra {OrderId}: sigue con acceso.", order.Id);
            return;
        }

        order.Refund(time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Compra {OrderId} Reembolsada.", order.Id);
    }

    /// <summary>
    /// El Producto según la metadata que el checkout copia a la orden, o si falta, según el producto de Polar.
    /// No exige que siga publicado: pudo despublicarse entre el checkout y el pago.
    /// </summary>
    private async Task<(Template?, Bundle?)> FindProductAsync(PolarOrder polar, CancellationToken ct)
    {
        var bundles = db.Bundles.Include(b => b.Templates).ThenInclude(bt => bt.Template);
        var metadata = polar.Metadata ?? [];
        var tipo = metadata.GetValueOrDefault("tipo") is { ValueKind: JsonValueKind.String } t ? t.GetString() : null;
        var id = metadata.GetValueOrDefault("producto_id") switch
        {
            { ValueKind: JsonValueKind.Number } n when n.TryGetInt32(out var number) => number,
            { ValueKind: JsonValueKind.String } s when int.TryParse(s.GetString(), out var number) => number,
            _ => (int?)null,
        };

        if (tipo == ProductTypes.Template && id is not null)
            return (await db.Templates.SingleOrDefaultAsync(x => x.Id == id, ct), null);
        if (tipo == ProductTypes.Bundle && id is not null)
            return (null, await bundles.SingleOrDefaultAsync(x => x.Id == id, ct));
        if (polar.ProductId is null)
            return (null, null);
        return (await db.Templates.SingleOrDefaultAsync(x => x.PolarProductId == polar.ProductId, ct),
            await bundles.SingleOrDefaultAsync(x => x.PolarProductId == polar.ProductId, ct));
    }
}
