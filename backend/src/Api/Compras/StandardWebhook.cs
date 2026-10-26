using System.Security.Cryptography;
using System.Text;

namespace Api.Compras;

/// <summary>
/// Firma de <see href="https://www.standardwebhooks.com/">Standard Webhooks</see>: HMAC-SHA256 de
/// <c>{webhook-id}.{webhook-timestamp}.{cuerpo}</c>, en base64, con el prefijo <c>v1,</c> en <c>webhook-signature</c>.
/// </summary>
public sealed class StandardWebhook
{
    public const string IdHeader = "webhook-id";
    public const string TimestampHeader = "webhook-timestamp";
    public const string SignatureHeader = "webhook-signature";

    private const string SecretPrefix = "whsec_";

    private readonly List<byte[]> _keys = [];
    private readonly TimeProvider _time;
    private readonly TimeSpan _tolerance;

    /// <summary>
    /// Polar cambió el esquema de sus secretos: los generados desde el 8 de septiembre de 2026 son Standard Webhooks
    /// (la clave es el base64 que sigue a <c>whsec_</c>) y los anteriores usan como clave los bytes UTF-8 del secreto
    /// completo. Como sus SDKs, se prueban las dos.
    /// </summary>
    public StandardWebhook(string secret, TimeProvider time, TimeSpan tolerance)
    {
        _time = time;
        _tolerance = tolerance;
        if (string.IsNullOrWhiteSpace(secret))
            return;

        var encoded = secret.StartsWith(SecretPrefix, StringComparison.Ordinal) ? secret[SecretPrefix.Length..] : secret;
        var buffer = new byte[encoded.Length];
        if (Convert.TryFromBase64String(encoded, buffer, out var written))
            _keys.Add(buffer[..written]);
        _keys.Add(Encoding.UTF8.GetBytes(secret));
    }

    public bool IsConfigured => _keys.Count > 0;

    public bool IsValid(string? id, string? timestamp, string? signatures, ReadOnlySpan<byte> body)
    {
        if (!IsConfigured || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(signatures) || !long.TryParse(timestamp, out var seconds))
            return false;

        var sentAt = DateTimeOffset.FromUnixTimeSeconds(Math.Clamp(seconds, 0, 253402300799));
        if ((_time.GetUtcNow() - sentAt).Duration() > _tolerance)
            return false;

        var content = SignedContent(id, timestamp, body);
        var candidates = signatures.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => s.StartsWith("v1,", StringComparison.Ordinal))
            .Select(s => TryDecode(s[3..]))
            .OfType<byte[]>()
            .ToList();

        foreach (var key in _keys)
        {
            var expected = HMACSHA256.HashData(key, content);
            if (candidates.Any(candidate => CryptographicOperations.FixedTimeEquals(candidate, expected)))
                return true;
        }
        return false;
    }

    private static byte[] SignedContent(string id, string timestamp, ReadOnlySpan<byte> body)
    {
        var prefix = Encoding.UTF8.GetBytes($"{id}.{timestamp}.");
        var content = new byte[prefix.Length + body.Length];
        prefix.CopyTo(content, 0);
        body.CopyTo(content.AsSpan(prefix.Length));
        return content;
    }

    private static byte[]? TryDecode(string base64)
    {
        var buffer = new byte[base64.Length];
        return Convert.TryFromBase64String(base64, buffer, out var written) ? buffer[..written] : null;
    }
}
