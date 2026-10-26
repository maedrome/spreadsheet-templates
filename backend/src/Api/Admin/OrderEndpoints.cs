using Api.Common;
using Api.Compras;
using Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Api.Admin;

public record OrderListItem(
    int Id, string Email, string ProductType, string ProductName, int PricePaidCents, DateTimeOffset PaidAt, string Status);

/// <summary>La lista de Compras y el reenvío del Enlace de compra.</summary>
public static class OrderEndpoints
{
    public static void MapAdminOrders(this RouteGroupBuilder group)
    {
        group.MapGet("", List);
        group.MapPost("/{id:int}/reenviar", ResendLink);
    }

    private static async Task<List<OrderListItem>> List(AppDbContext db)
    {
        var orders = await db.Orders.AsNoTracking()
            .OrderByDescending(o => o.PaidAt)
            .Select(o => new
            {
                o.Id, o.Email, o.PricePaidCents, o.PaidAt, o.Status,
                TemplateName = o.Template != null ? o.Template.Name : null,
                BundleName = o.Bundle != null ? o.Bundle.Name : null,
            })
            .ToListAsync();

        return orders.Select(o => new OrderListItem(
            o.Id, o.Email,
            o.TemplateName is not null ? "plantilla" : "paquete",
            o.TemplateName ?? o.BundleName ?? "",
            o.PricePaidCents, o.PaidAt, o.Status.ToString())).ToList();
    }

    /// <summary>Emite un Enlace de compra nuevo, sin invalidar los anteriores, y lo envía al email de la Compra.</summary>
    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> ResendLink(
        int id, AppDbContext db, PurchaseEmails emails, TimeProvider time, ILogger<PurchaseEmails> logger, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Template).Include(o => o.Bundle).SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return TypedResults.NotFound();
        if (order.Status == OrderStatus.Refunded)
            throw new DomainException("La Compra está Reembolsada: su Enlace de compra ya no da acceso.");

        var token = order.IssuePurchaseLink(time.GetUtcNow());
        await db.SaveChangesAsync(ct);

        try
        {
            await emails.SendPurchaseLinkAsync(order, order.Template?.Name ?? order.Bundle?.Name ?? "", token, ct);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "No se pudo reenviar el email de la Compra {OrderId}.", order.Id);
            return TypedResults.Problem(
                detail: "No se pudo enviar el email. Revisa la configuración de Resend e inténtalo de nuevo.",
                statusCode: StatusCodes.Status502BadGateway);
        }
        return TypedResults.NoContent();
    }
}
