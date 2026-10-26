namespace Api.Compras;

/// <summary>El procesador de pagos (ADR 0003). Hoy es Polar; Lemon Squeezy queda como respaldo detrás de esta interfaz.</summary>
public interface IPaymentProvider
{
    /// <summary>Crea el producto, o lo actualiza si <see cref="PaymentProduct.Id"/> ya existe, y devuelve su id.</summary>
    Task<string?> UpsertProductAsync(PaymentProduct product, CancellationToken cancellationToken);

    Task<CheckoutSession> CreateCheckoutAsync(CheckoutSessionRequest request, CancellationToken cancellationToken);
}

/// <summary>Un Producto de la tienda tal como se cobra en el procesador.</summary>
/// <param name="Id">El id en el procesador, o <c>null</c> si todavía no existe.</param>
/// <param name="Tipo"><c>plantilla</c> o <c>paquete</c>.</param>
public record PaymentProduct(string? Id, string Name, string Description, int PriceCents, string Tipo, int ProductId);

/// <param name="SuccessUrl">Puede incluir <see cref="CheckoutIdPlaceholder"/>, que el procesador reemplaza por el id de la sesión.</param>
public record CheckoutSessionRequest(string ProductId, string SuccessUrl, string ReturnUrl, IReadOnlyDictionary<string, string> Metadata)
{
    public const string CheckoutIdPlaceholder = "{CHECKOUT_ID}";
}

public record CheckoutSession(string Id, string Url);

/// <summary>El procesador de pagos falló o no está configurado. El mensaje se puede mostrar.</summary>
public class PaymentProviderException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>Solo en desarrollo, sin <c>Polar:AccessToken</c>: el catálogo funciona, pero no se puede cobrar.</summary>
public sealed class NotConfiguredPaymentProvider(ILogger<NotConfiguredPaymentProvider> logger) : IPaymentProvider
{
    public Task<string?> UpsertProductAsync(PaymentProduct product, CancellationToken cancellationToken)
    {
        logger.LogWarning("Polar no está configurado: el {Tipo} {ProductId} no se sincroniza.", product.Tipo, product.ProductId);
        return Task.FromResult(product.Id);
    }

    public Task<CheckoutSession> CreateCheckoutAsync(CheckoutSessionRequest request, CancellationToken cancellationToken) =>
        throw new PaymentProviderException("Los pagos no están configurados. Agrega Polar:AccessToken con dotnet user-secrets.");
}
