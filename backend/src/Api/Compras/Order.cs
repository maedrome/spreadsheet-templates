using Api.Catalogo;
using Api.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Compras;

public enum OrderStatus
{
    Paid,
    Refunded,
}

/// <summary>Un email pagó un Producto. Solo se crea cuando el pago está confirmado.</summary>
public class Order
{
    private Order() { }

    private Order(string email, int pricePaidCents, string polarOrderId, string? polarCheckoutId, DateTimeOffset paidAt)
    {
        Email = NormalizeEmail(email);
        PricePaidCents = pricePaidCents;
        PolarOrderId = polarOrderId;
        PolarCheckoutId = polarCheckoutId;
        Status = OrderStatus.Paid;
        PaidAt = paidAt;
    }

    public int Id { get; private set; }
    public string Email { get; private set; } = null!;

    /// <summary>El Producto comprado: exactamente uno de <see cref="TemplateId"/> o <see cref="BundleId"/>.</summary>
    public int? TemplateId { get; private set; }
    public Template? Template { get; private set; }
    public int? BundleId { get; private set; }
    public Bundle? Bundle { get; private set; }

    public int PricePaidCents { get; private set; }
    public string PolarOrderId { get; private set; } = null!;

    /// <summary>La sesión de checkout de Polar que generó la Compra, con la que la página de confirmación la encuentra.</summary>
    public string? PolarCheckoutId { get; private set; }

    /// <summary>Cuándo la página de confirmación recibió su Enlace de compra. Se emite una sola vez.</summary>
    public DateTimeOffset? ConfirmationLinkIssuedAt { get; private set; }

    public OrderStatus Status { get; private set; }
    public DateTimeOffset PaidAt { get; private set; }
    public DateTimeOffset? RefundedAt { get; private set; }

    /// <summary>Snapshot de las Plantillas que incluía el Producto al pagar.</summary>
    public List<OrderTemplate> Templates { get; } = [];
    public List<PurchaseLink> PurchaseLinks { get; } = [];

    public static Order ForTemplate(
        string email, Template template, int pricePaidCents, string polarOrderId, DateTimeOffset paidAt, string? polarCheckoutId = null)
    {
        var order = new Order(email, pricePaidCents, polarOrderId, polarCheckoutId, paidAt) { Template = template };
        order.Templates.Add(new OrderTemplate(order, template));
        return order;
    }

    /// <summary>Requiere <see cref="Bundle.Templates"/> cargados con su Plantilla.</summary>
    public static Order ForBundle(
        string email, Bundle bundle, int pricePaidCents, string polarOrderId, DateTimeOffset paidAt, string? polarCheckoutId = null)
    {
        if (bundle.Templates.Count == 0)
            throw new DomainException("El Paquete no tiene Plantillas para incluir en la Compra.");

        var order = new Order(email, pricePaidCents, polarOrderId, polarCheckoutId, paidAt) { Bundle = bundle };
        foreach (var bundleTemplate in bundle.Templates.OrderBy(bt => bt.Position))
            order.Templates.Add(new OrderTemplate(order, bundleTemplate.Template));
        return order;
    }

    /// <summary>Emite un Enlace de compra nuevo. El token en claro solo existe en el valor devuelto.</summary>
    public string IssuePurchaseLink(DateTimeOffset now)
    {
        var (link, token) = PurchaseLink.Create(this, now);
        PurchaseLinks.Add(link);
        return token;
    }

    /// <summary>
    /// El Enlace de compra para la página de confirmación, o <c>null</c> si ya se emitió: la base de datos solo guarda
    /// hashes, así que el enlace del email no se puede devolver, y uno nuevo solo se entrega una vez.
    /// </summary>
    public string? IssueConfirmationLink(DateTimeOffset now)
    {
        if (ConfirmationLinkIssuedAt is not null) return null;
        ConfirmationLinkIssuedAt = now;
        return IssuePurchaseLink(now);
    }

    public void Refund(DateTimeOffset now)
    {
        if (Status == OrderStatus.Refunded) return;
        Status = OrderStatus.Refunded;
        RefundedAt = now;
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}

/// <summary>Una Plantilla incluida en una Compra al momento de pagar.</summary>
public class OrderTemplate
{
    private OrderTemplate() { }

    internal OrderTemplate(Order order, Template template)
    {
        Order = order;
        Template = template;
    }

    public int OrderId { get; private set; }
    public Order Order { get; private set; } = null!;
    public int TemplateId { get; private set; }
    public Template Template { get; private set; } = null!;
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.Email).HasMaxLength(320);
        builder.HasIndex(o => o.Email);
        builder.Property(o => o.PolarOrderId).HasMaxLength(100);
        builder.HasIndex(o => o.PolarOrderId).IsUnique();
        builder.Property(o => o.PolarCheckoutId).HasMaxLength(100);
        builder.HasIndex(o => o.PolarCheckoutId).IsUnique();
        // xmin de PostgreSQL: dos consultas simultáneas de la confirmación no pueden emitir dos Enlaces.
        builder.Property<uint>("Version").IsRowVersion();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.ToTable(o => o.HasCheckConstraint(
            "CK_Orders_Product",
            "(\"TemplateId\" IS NULL) <> (\"BundleId\" IS NULL)"));

        // Un Producto con Compras no se puede borrar.
        builder.HasOne(o => o.Template).WithMany().HasForeignKey(o => o.TemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Bundle).WithMany().HasForeignKey(o => o.BundleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Templates).WithOne(ot => ot.Order).HasForeignKey(ot => ot.OrderId);
        builder.HasMany(o => o.PurchaseLinks).WithOne(pl => pl.Order).HasForeignKey(pl => pl.OrderId);
    }
}

public class OrderTemplateConfiguration : IEntityTypeConfiguration<OrderTemplate>
{
    public void Configure(EntityTypeBuilder<OrderTemplate> builder)
    {
        builder.HasKey(ot => new { ot.OrderId, ot.TemplateId });
        // Una Plantilla incluida en una Compra (sola o en un Paquete) no se puede borrar.
        builder.HasOne(ot => ot.Template).WithMany().HasForeignKey(ot => ot.TemplateId).OnDelete(DeleteBehavior.Restrict);
    }
}
