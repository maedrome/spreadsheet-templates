namespace Api.Compras;

public class PolarOptions
{
    public const string Section = "Polar";

    public string AccessToken { get; set; } = "";
    public string WebhookSecret { get; set; } = "";

    /// <summary><c>sandbox</c> o <c>production</c>.</summary>
    public string Environment { get; set; } = "sandbox";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AccessToken);
    public bool IsSandbox => Environment.Equals("sandbox", StringComparison.OrdinalIgnoreCase);

    public Uri ApiBaseUrl => new(IsSandbox ? "https://sandbox-api.polar.sh/" : "https://api.polar.sh/");

    /// <summary>Diferencia máxima entre <c>webhook-timestamp</c> y el reloj, para que un webhook capturado no se pueda reenviar después.</summary>
    public TimeSpan WebhookTolerance { get; set; } = TimeSpan.FromMinutes(5);
}

public class ResendOptions
{
    public const string Section = "Resend";

    public string ApiKey { get; set; } = "";
}

public class EmailOptions
{
    public const string Section = "Email";

    /// <summary>Remitente con nombre, por ejemplo <c>Plantillas &lt;compras@example.com&gt;</c>.</summary>
    public string From { get; set; } = "";
}

public class CheckoutOptions
{
    public const string Section = "Checkout";

    /// <summary>Checkouts que se pueden iniciar por IP y por ventana.</summary>
    public int PermitLimit { get; set; } = 10;

    /// <summary>Consultas por IP y por ventana a la confirmación y al resumen de una Compra.</summary>
    public int StatusPermitLimit { get; set; } = 120;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}
