namespace Api.Catalogo;

public record PreviewVariantDto(string Url, int Width, int Height);

public record PreviewDto(IReadOnlyList<PreviewVariantDto> Variants);

public record TemplateSummaryDto(string Slug, string Name, int PriceCents, PreviewDto? MainPreview);

public record TemplateCardDto(string Slug, string Name, string Description, int PriceCents, PreviewDto? MainPreview);

public record BundleCardDto(
    string Slug,
    string Name,
    string Description,
    int PriceCents,
    int SavingsCents,
    IReadOnlyList<TemplateSummaryDto> Templates);

public record CatalogoDto(IReadOnlyList<BundleCardDto> Bundles, IReadOnlyList<TemplateCardDto> Templates);

public record TemplateDetailDto(
    string Slug,
    string Name,
    string Description,
    int PriceCents,
    IReadOnlyList<PreviewDto> Previews);

public record BundleDetailDto(
    string Slug,
    string Name,
    string Description,
    int PriceCents,
    int SavingsCents,
    IReadOnlyList<TemplateSummaryDto> Templates);

internal static class CatalogoMapping
{
    public const string PreviewsRoute = "/api/previews";

    public static PreviewDto ToDto(this Preview preview) =>
        new(preview.Variants
            .OrderBy(v => v.Width)
            .Select(v => new PreviewVariantDto($"{PreviewsRoute}/{v.FileName}", v.Width, v.Height))
            .ToList());

    public static TemplateSummaryDto ToSummary(this Template template) =>
        new(template.Slug, template.Name, template.PriceCents, template.MainPreview?.ToDto());

    public static TemplateCardDto ToCard(this Template template) =>
        new(template.Slug, template.Name, template.Description, template.PriceCents, template.MainPreview?.ToDto());

    public static IReadOnlyList<TemplateSummaryDto> SummariesOf(this Bundle bundle) =>
        bundle.Templates.OrderBy(bt => bt.Position).Select(bt => bt.Template.ToSummary()).ToList();

    public static BundleCardDto ToCard(this Bundle bundle) =>
        new(bundle.Slug, bundle.Name, bundle.Description, bundle.PriceCents, bundle.SavingsCents, bundle.SummariesOf());

    public static BundleDetailDto ToDetail(this Bundle bundle) =>
        new(bundle.Slug, bundle.Name, bundle.Description, bundle.PriceCents, bundle.SavingsCents, bundle.SummariesOf());

    public static TemplateDetailDto ToDetail(this Template template) =>
        new(template.Slug, template.Name, template.Description, template.PriceCents,
            template.Previews.OrderBy(p => p.Position).Select(p => p.ToDto()).ToList());
}
