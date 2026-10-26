using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Compras;

/// <summary>
/// La API de Polar. Un checkout exige productos creados en Polar, así que cada Producto publicado tiene el suyo,
/// con un único precio fijo en USD (Polar archiva los precios que se dejan fuera al actualizar).
/// </summary>
public sealed class PolarPaymentProvider(HttpClient http, ILogger<PolarPaymentProvider> logger) : IPaymentProvider
{
    /// <summary>Límite de Polar para el nombre de un producto.</summary>
    private const int MaxNameLength = 64;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<string?> UpsertProductAsync(PaymentProduct product, CancellationToken cancellationToken)
    {
        var name = product.Name.Length <= MaxNameLength ? product.Name : product.Name[..(MaxNameLength - 1)].TrimEnd() + "…";
        var description = string.IsNullOrWhiteSpace(product.Description) ? null : product.Description;
        var metadata = new Dictionary<string, object> { ["tipo"] = product.Tipo, ["producto_id"] = product.ProductId };
        var newPrice = new PriceInput("fixed", product.PriceCents, "usd");

        if (product.Id is null)
        {
            var created = await SendAsync<PolarProduct>(
                HttpMethod.Post, "v1/products/", new ProductCreate(name, description, [newPrice], metadata), cancellationToken);
            return created.Id;
        }

        // Si el precio no cambió se conserva por id: así no se crea un precio nuevo con cada cambio de nombre.
        var current = await SendAsync<PolarProduct>(HttpMethod.Get, $"v1/products/{product.Id}", null, cancellationToken);
        var samePrice = current.Prices.FirstOrDefault(p =>
            !p.IsArchived && p.AmountType == "fixed" && p.PriceAmount == product.PriceCents && p.PriceCurrency == "usd");
        object price = samePrice is null ? newPrice : new ExistingPrice(samePrice.Id);

        var updated = await SendAsync<PolarProduct>(
            HttpMethod.Patch, $"v1/products/{product.Id}", new ProductUpdate(name, description, false, [price], metadata), cancellationToken);
        return updated.Id;
    }

    public async Task<CheckoutSession> CreateCheckoutAsync(CheckoutSessionRequest request, CancellationToken cancellationToken)
    {
        var checkout = await SendAsync<PolarCheckout>(
            HttpMethod.Post, "v1/checkouts/",
            new CheckoutCreate([request.ProductId], request.SuccessUrl, request.ReturnUrl, request.Metadata),
            cancellationToken);
        return new CheckoutSession(checkout.Id, checkout.Url);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, path);
        if (body is not null)
            message.Content = JsonContent.Create(body, body.GetType(), options: Json);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "No se pudo conectar con Polar ({Method} {Path}).", method, path);
            throw new PaymentProviderException("No se pudo conectar con Polar. Inténtalo de nuevo en unos minutos.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Polar respondió {Status} a {Method} {Path}: {Error}", (int)response.StatusCode, method, path, error);
                throw new PaymentProviderException($"Polar rechazó la operación ({(int)response.StatusCode}). Revisa el log para ver el detalle.");
            }

            return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
                ?? throw new PaymentProviderException("Polar devolvió una respuesta vacía.");
        }
    }

    private record PriceInput(string AmountType, int PriceAmount, string PriceCurrency);
    private record ExistingPrice(string Id);
    private record ProductCreate(string Name, string? Description, List<PriceInput> Prices, Dictionary<string, object> Metadata);

    private record ProductUpdate(
        string Name, string? Description, bool IsArchived, List<object> Prices, Dictionary<string, object> Metadata);

    private record CheckoutCreate(
        List<string> Products, string SuccessUrl, string ReturnUrl, IReadOnlyDictionary<string, string> Metadata);

    private record PolarProduct(string Id, List<PolarPrice> Prices);
    private record PolarPrice(string Id, string AmountType, int? PriceAmount, string? PriceCurrency, bool IsArchived);
    private record PolarCheckout(string Id, string Url);
}
