using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Catalogo;

public class Preview
{
    private Preview() { }

    internal Preview(int position) => Position = position;

    public int Id { get; private set; }
    public int TemplateId { get; private set; }
    public Template Template { get; private set; } = null!;

    /// <summary>Orden dentro de la Plantilla. La de menor posición es la Preview principal.</summary>
    public int Position { get; set; }

    /// <summary>Archivos WebP generados para cada ancho. Los produce la conversión de la fase 3.</summary>
    public List<PreviewVariant> Variants { get; } = [];
}

public class PreviewVariant
{
    public static readonly int[] StandardWidths = [480, 960, 1600];

    public int Width { get; set; }
    public int Height { get; set; }
    public string FileName { get; set; } = null!;
}

public class PreviewConfiguration : IEntityTypeConfiguration<Preview>
{
    public void Configure(EntityTypeBuilder<Preview> builder)
    {
        builder.HasIndex(p => new { p.TemplateId, p.Position });
        builder.OwnsMany(p => p.Variants, v => v.ToJson());
    }
}
