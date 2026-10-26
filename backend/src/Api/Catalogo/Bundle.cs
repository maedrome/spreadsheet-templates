using Api.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Catalogo;

public class Bundle
{
    public const int MinTemplates = 2;

    private Bundle() { }

    public Bundle(string slug, string name)
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

    /// <summary>El producto de Polar con el que se cobra. Se crea al publicar y se actualiza al cambiar el precio.</summary>
    public string? PolarProductId { get; set; }

    public List<BundleTemplate> Templates { get; } = [];

    /// <summary>Suma de los precios actuales de las Plantillas menos el precio del Paquete. Requiere <see cref="Templates"/> cargados con su Plantilla.</summary>
    public int SavingsCents => Templates.Sum(bt => bt.Template.PriceCents) - PriceCents;

    public void ChangeSlug(string slug)
    {
        if (FirstPublishedAt is not null && slug != Slug)
            throw new DomainException("El slug de un Paquete no se puede cambiar después de publicarlo.");
        Slug = Catalogo.Slug.EnsureValid(slug);
    }

    public void AddTemplate(Template template)
    {
        if (Templates.Any(bt => bt.Template == template || (template.Id != 0 && bt.TemplateId == template.Id)))
            throw new DomainException($"La Plantilla \"{template.Name}\" ya está en el Paquete.");
        if (IsPublished && !template.IsPublished)
            throw new DomainException("No se puede agregar una Plantilla sin publicar a un Paquete publicado.");

        var position = Templates.Count == 0 ? 0 : Templates.Max(bt => bt.Position) + 1;
        var bundleTemplate = new BundleTemplate(this, template, position);
        Templates.Add(bundleTemplate);
        template.BundleTemplates.Add(bundleTemplate);
    }

    public void RemoveTemplate(Template template)
    {
        var bundleTemplate = Templates.SingleOrDefault(bt => bt.Template == template)
            ?? throw new DomainException($"La Plantilla \"{template.Name}\" no está en el Paquete.");

        Templates.Remove(bundleTemplate);
        if (IsPublished && PublicationProblems().Count > 0)
        {
            Templates.Add(bundleTemplate);
            throw new DomainException("No se puede quitar la Plantilla: el Paquete publicado dejaría de cumplir las reglas de publicación.");
        }
        template.BundleTemplates.Remove(bundleTemplate);
    }

    /// <summary>Requiere <see cref="Templates"/> cargados con su Plantilla.</summary>
    public void Publish(DateTimeOffset now)
    {
        var problems = PublicationProblems();
        if (problems.Count > 0)
            throw new DomainException($"No se puede publicar el Paquete: {string.Join("; ", problems)}.");

        IsPublished = true;
        FirstPublishedAt ??= now;
    }

    public void Unpublish() => IsPublished = false;

    /// <summary>Por qué no cumple las reglas de publicación; vacío si las cumple. Requiere <see cref="Templates"/> cargados con su Plantilla.</summary>
    public List<string> PublicationProblems()
    {
        var problems = new List<string>();
        if (Templates.Count < MinTemplates)
            problems.Add($"necesita al menos {MinTemplates} Plantillas");
        if (Templates.Any(bt => !bt.Template.IsPublished))
            problems.Add("todas sus Plantillas deben estar publicadas");
        if (SavingsCents <= 0)
            problems.Add("el Ahorro debe ser mayor que 0");
        return problems;
    }
}

public class BundleTemplate
{
    private BundleTemplate() { }

    internal BundleTemplate(Bundle bundle, Template template, int position)
    {
        Bundle = bundle;
        Template = template;
        Position = position;
    }

    public int BundleId { get; private set; }
    public Bundle Bundle { get; private set; } = null!;
    public int TemplateId { get; private set; }
    public Template Template { get; private set; } = null!;
    public int Position { get; set; }
}

public class BundleConfiguration : IEntityTypeConfiguration<Bundle>
{
    public void Configure(EntityTypeBuilder<Bundle> builder)
    {
        builder.Property(b => b.Slug).HasMaxLength(Slug.MaxLength);
        builder.HasIndex(b => b.Slug).IsUnique();
        builder.Property(b => b.Name).HasMaxLength(200);
        builder.Property(b => b.PolarProductId).HasMaxLength(100);
        builder.ToTable(b => b.HasCheckConstraint("CK_Bundles_PriceCents", "\"PriceCents\" >= 0"));

        builder.HasMany(b => b.Templates).WithOne(bt => bt.Bundle).HasForeignKey(bt => bt.BundleId);
    }
}

public class BundleTemplateConfiguration : IEntityTypeConfiguration<BundleTemplate>
{
    public void Configure(EntityTypeBuilder<BundleTemplate> builder)
    {
        builder.HasKey(bt => new { bt.BundleId, bt.TemplateId });
        builder.HasOne(bt => bt.Template).WithMany(t => t.BundleTemplates).HasForeignKey(bt => bt.TemplateId);
    }
}
