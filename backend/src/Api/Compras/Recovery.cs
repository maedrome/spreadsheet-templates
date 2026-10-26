using System.Collections.Concurrent;
using System.Net.Mail;
using System.Threading.Channels;
using Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Compras;

public record RecoveryInput(string? Email);

/// <summary>La misma respuesta exista o no el email.</summary>
public record RecoveryAccepted(string Message);

/// <summary>Las solicitudes de recuperación pendientes. La búsqueda y el envío ocurren fuera del request, para que su duración no revele nada.</summary>
public sealed class RecoveryQueue
{
    private readonly Channel<string> _channel = Channel.CreateBounded<string>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public bool TryEnqueue(string normalizedEmail) => _channel.Writer.TryWrite(normalizedEmail);

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}

/// <summary>Límite por email normalizado. Al superarlo la solicitud se descarta en silencio, así que no revela si el email existe.</summary>
public sealed class RecoveryEmailLimiter(IOptions<RecoveryOptions> options, TimeProvider time)
{
    private readonly ConcurrentDictionary<string, (DateTimeOffset WindowStart, int Count)> _windows = new();

    public bool TryAcquire(string normalizedEmail)
    {
        var now = time.GetUtcNow();
        var window = options.Value.Window;
        var limit = options.Value.EmailPermitLimit;
        var allowed = false;
        _windows.AddOrUpdate(
            normalizedEmail,
            _ =>
            {
                allowed = limit > 0;
                return (now, 1);
            },
            (_, current) =>
            {
                if (now - current.WindowStart >= window)
                {
                    allowed = limit > 0;
                    return (now, 1);
                }
                allowed = current.Count < limit;
                return allowed ? (current.WindowStart, current.Count + 1) : current;
            });

        if (_windows.Count > 10_000)
            foreach (var (key, value) in _windows)
                if (now - value.WindowStart >= window)
                    _windows.TryRemove(key, out _);
        return allowed;
    }
}

public static class RecoveryEndpoints
{
    public const string AcceptedMessage = "Si hay compras con ese email, te enviamos los enlaces.";

    public static void MapRecovery(this RouteGroupBuilder group) =>
        group.MapPost("", Recover).RequireRateLimiting(ComprasSetup.RecoveryRateLimit);

    private static Results<Accepted<RecoveryAccepted>, ValidationProblem> Recover(
        RecoveryInput input, RecoveryQueue queue, RecoveryEmailLimiter limiter)
    {
        if (!IsValidEmail(input.Email))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Escribe un email válido."] });

        var email = Order.NormalizeEmail(input.Email!);
        if (limiter.TryAcquire(email))
            queue.TryEnqueue(email);
        return TypedResults.Accepted((string?)null, new RecoveryAccepted(AcceptedMessage));
    }

    private static bool IsValidEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 320)
            return false;
        var email = value.Trim();
        return MailAddress.TryCreate(email, out var address)
            && address.Address == email
            && address.Host.Contains('.');
    }
}

/// <summary>Emite un Enlace de compra nuevo por cada Compra no reembolsada del email y envía un solo email con todos.</summary>
public sealed class RecoveryWorker(
    RecoveryQueue queue, IServiceScopeFactory scopes, TimeProvider time, ILogger<RecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var email in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await RecoverAsync(email, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "No se pudo procesar una recuperación de compras.");
            }
        }
    }

    private async Task RecoverAsync(string email, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orders = await db.Orders
            .Where(o => o.Email == email && o.Status == OrderStatus.Paid)
            .Include(o => o.Template).Include(o => o.Bundle)
            .OrderByDescending(o => o.PaidAt)
            .ToListAsync(ct);
        if (orders.Count == 0)
            return;

        var now = time.GetUtcNow();
        var links = orders
            .Select(o => (ProductName: o.Template?.Name ?? o.Bundle?.Name ?? "", Token: o.IssuePurchaseLink(now)))
            .ToList();
        await db.SaveChangesAsync(ct);

        await scope.ServiceProvider.GetRequiredService<PurchaseEmails>().SendRecoveryAsync(email, links, ct);
    }
}
