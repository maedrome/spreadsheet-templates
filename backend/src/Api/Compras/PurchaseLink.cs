using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Compras;

/// <summary>Enlace de compra. Solo se guarda el hash SHA-256 del token, nunca el token en claro.</summary>
public class PurchaseLink
{
    private const int TokenBytes = 32;

    private PurchaseLink() { }

    public int Id { get; private set; }
    public int OrderId { get; private set; }
    public Order Order { get; private set; } = null!;
    public byte[] TokenHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    internal static (PurchaseLink Link, string Token) Create(Order order, DateTimeOffset now)
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        var link = new PurchaseLink { Order = order, TokenHash = HashToken(token), CreatedAt = now };
        return (link, token);
    }

    public static byte[] HashToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}

public class PurchaseLinkConfiguration : IEntityTypeConfiguration<PurchaseLink>
{
    public void Configure(EntityTypeBuilder<PurchaseLink> builder)
    {
        builder.Property(pl => pl.TokenHash).HasMaxLength(32);
        builder.HasIndex(pl => pl.TokenHash).IsUnique();
    }
}
