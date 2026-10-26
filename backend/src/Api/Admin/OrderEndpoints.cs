using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Admin;

public record OrderListItem(
    int Id, string Email, string ProductType, string ProductName, int PricePaidCents, DateTimeOffset PaidAt, string Status);

/// <summary>Compras de solo lectura. El reenvío del Enlace de compra llega con el envío de emails (fase 4).</summary>
public static class OrderEndpoints
{
    public static void MapAdminOrders(this RouteGroupBuilder group) =>
        group.MapGet("", async (AppDbContext db) =>
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
        });
}
