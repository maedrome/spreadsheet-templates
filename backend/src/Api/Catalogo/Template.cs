using Api.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Catalogo;

public class Template
{
    private Template() { }

    public Template(string slug, string name)
    {
        Slug = Catalogo.Slug.EnsureValid(slug);
        Name = name;
    }

    public int Id { get; private set; }
    public string Slug { get; private set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = "";
    public int PriceCents { get; set; }
    public bool IsPublished { get; private set; }

    /// <summary>Fecha de la primera publicación. Desde entonces el slug queda fijo.</summary>
    public DateTimeOffset? FirstPublishedAt { get; private set; }

    public int Position { get; set; }
    public string? XlsxPath { get; set; }
    public string? GoogleSheetsCopyUrl { get; set; }

    /// <summary>El producto de Polar con el que se cobra. Se crea al publicar y se actualiza al cambiar el precio.</summary>
    public string? PolarProductId { get; set; }

    public List<Preview> Previews { get; } = [];
    public List<BundleTemplate> BundleTemplates { get; } = [];

    public Preview? MainPreview => Previews.OrderBy(p => p.Position).FirstOrDefault();

    public void ChangeSlug(string slug)
    {
        if (FirstPublishedAt is not null && slug != Slug)
            throw new DomainException("El slug de una Plantilla no se puede cambiar después de publicarla.");
        Slug = Catalogo.Slug.EnsureValid(slug);
    }

    public Preview AddPreview()
    {
        var preview = new Preview(Previews.Count == 0 ? 0 : Previews.Max(p => p.Position) + 1);
        Previews.Add(preview);
        return preview;
    }

    /// <summary>Requiere <see cref="Previews"/> cargadas.</summary>
    public void Publish(DateTimeOffset now)
    {
        var missing = MissingForPublication();
        if (missing.Count > 0)
            throw new DomainException($"No se puede publicar la Plantilla: falta {string.Join(", ", missing)}.");

        IsPublished = true;
        FirstPublishedAt ??= now;
    }

    /// <summary>Lo que le falta para poder publicarse; vacío si está completa. Requiere <see cref="Previews"/> cargadas.</summary>
    public List<string> MissingForPublication()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(Name)) missing.Add("el nombre");
        if (string.IsNullOrWhiteSpace(Description)) missing.Add("la descripción");
        if (PriceCents <= 0) missing.Add("un precio mayor que 0");
        if (Previews.Count == 0) missing.Add("al menos una Preview");
        if (string.IsNullOrWhiteSpace(XlsxPath)) missing.Add("el archivo .xlsx");
        if (string.IsNullOrWhiteSpace(GoogleSheetsCopyUrl)) missing.Add("el link de Google Sheets");
        return missing;
    }

    /// <summary>Requiere <see cref="BundleTemplates"/> cargados con su <see cref="BundleTemplate.Bundle"/>.</summary>
    public void Unpublish()
    {
        var publishedBundles = BundleTemplates.Select(bt => bt.Bundle).Where(b => b.IsPublished).ToList();
        if (publishedBundles.Count > 0)
            throw new DomainException(
                "No se puede despublicar la Plantilla porque está en Paquetes publicados: " +
                $"{string.Join(", ", publishedBundles.Select(b => b.Name))}.");

        IsPublished = false;
    }
}

public class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.Property(t => t.Slug).HasMaxLength(Slug.MaxLength);
        builder.HasIndex(t => t.Slug).IsUnique();
        builder.Property(t => t.Name).HasMaxLength(200);
        builder.Property(t => t.XlsxPath).HasMaxLength(500);
        builder.Property(t => t.GoogleSheetsCopyUrl).HasMaxLength(500);
        builder.Property(t => t.PolarProductId).HasMaxLength(100);
        builder.ToTable(t => t.HasCheckConstraint("CK_Templates_PriceCents", "\"PriceCents\" >= 0"));

        builder.HasMany(t => t.Previews).WithOne(p => p.Template).HasForeignKey(p => p.TemplateId);
    }
}
