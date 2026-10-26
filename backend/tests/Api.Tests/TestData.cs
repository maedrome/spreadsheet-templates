using Api.Catalogo;

namespace Api.Tests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Una Plantilla con todo lo necesario para publicarse, todavía sin publicar.</summary>
    public static Template PublishableTemplate(string slug, int priceCents = 500)
    {
        var template = new Template(slug, $"Plantilla {slug}")
        {
            Description = "Una plantilla de presupuesto.",
            PriceCents = priceCents,
            XlsxPath = $"{slug}.xlsx",
            GoogleSheetsCopyUrl = $"https://docs.google.com/spreadsheets/d/{slug}/copy",
        };
        template.AddPreview().Variants.Add(new PreviewVariant { Width = 640, Height = 480, FileName = $"{slug}-640.webp" });
        return template;
    }

    public static Template PublishedTemplate(string slug, int priceCents = 500)
    {
        var template = PublishableTemplate(slug, priceCents);
        template.Publish(Now);
        return template;
    }

    /// <summary>Un Paquete de dos Plantillas publicadas de 500 centavos, a 700: Ahorro de 300.</summary>
    public static Bundle PublishableBundle(string slug, params Template[] templates)
    {
        var bundle = new Bundle(slug, $"Paquete {slug}") { Description = "Un paquete.", PriceCents = 700 };
        if (templates.Length == 0)
            templates = [PublishedTemplate($"{slug}-a"), PublishedTemplate($"{slug}-b")];
        foreach (var template in templates)
            bundle.AddTemplate(template);
        return bundle;
    }
}
