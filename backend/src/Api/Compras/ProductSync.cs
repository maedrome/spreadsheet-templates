using Api.Catalogo;

namespace Api.Compras;

public static class ProductTypes
{
    public const string Template = "plantilla";
    public const string Bundle = "paquete";
}

/// <summary>Mantiene el producto del procesador de pagos al día con el Producto: nombre, descripción y precio.</summary>
public static class ProductSync
{
    public static async Task SyncAsync(this IPaymentProvider payments, Template template, CancellationToken cancellationToken) =>
        template.PolarProductId = await payments.UpsertProductAsync(
            new PaymentProduct(template.PolarProductId, template.Name, template.Description, template.PriceCents, ProductTypes.Template, template.Id),
            cancellationToken);

    public static async Task SyncAsync(this IPaymentProvider payments, Bundle bundle, CancellationToken cancellationToken) =>
        bundle.PolarProductId = await payments.UpsertProductAsync(
            new PaymentProduct(bundle.PolarProductId, bundle.Name, bundle.Description, bundle.PriceCents, ProductTypes.Bundle, bundle.Id),
            cancellationToken);
}
