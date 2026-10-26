using Api.Catalogo;
using Api.Common;

namespace Api.Tests.Catalogo;

public class TemplateTests
{
    [Fact]
    public void Publish_succeeds_when_template_is_complete()
    {
        var template = TestData.PublishableTemplate("mensual");

        template.Publish(TestData.Now);

        Assert.True(template.IsPublished);
        Assert.Equal(TestData.Now, template.FirstPublishedAt);
    }

    public static TheoryData<string, Action<Template>> MissingRequirements => new()
    {
        { "el nombre", t => t.Name = " " },
        { "la descripción", t => t.Description = "" },
        { "un precio mayor que 0", t => t.PriceCents = 0 },
        { "al menos una Preview", t => t.Previews.Clear() },
        { "el archivo .xlsx", t => t.XlsxPath = null },
        { "el link de Google Sheets", t => t.GoogleSheetsCopyUrl = null },
    };

    [Theory]
    [MemberData(nameof(MissingRequirements))]
    public void Publish_fails_when_a_requirement_is_missing(string missing, Action<Template> breakIt)
    {
        var template = TestData.PublishableTemplate("mensual");
        breakIt(template);

        var error = Assert.Throws<DomainException>(() => template.Publish(TestData.Now));

        Assert.Contains(missing, error.Message);
        Assert.False(template.IsPublished);
    }

    [Fact]
    public void Slug_can_change_before_first_publication()
    {
        var template = TestData.PublishableTemplate("mensual");

        template.ChangeSlug("presupuesto-mensual");

        Assert.Equal("presupuesto-mensual", template.Slug);
    }

    [Fact]
    public void Slug_cannot_change_once_published_even_after_unpublishing()
    {
        var template = TestData.PublishedTemplate("mensual");
        template.Unpublish();

        Assert.Throws<DomainException>(() => template.ChangeSlug("otro"));
        Assert.Equal("mensual", template.Slug);
    }

    [Theory]
    [InlineData("Mayúsculas")]
    [InlineData("con espacios")]
    [InlineData("-guion-inicial")]
    [InlineData("")]
    public void Invalid_slug_is_rejected(string slug)
    {
        Assert.Throws<DomainException>(() => new Template(slug, "Plantilla"));
    }

    [Fact]
    public void Cannot_unpublish_while_in_a_published_bundle()
    {
        var template = TestData.PublishedTemplate("mensual");
        var bundle = TestData.PublishableBundle("hogar", template, TestData.PublishedTemplate("anual"));
        bundle.Publish(TestData.Now);

        var error = Assert.Throws<DomainException>(template.Unpublish);

        Assert.Contains("Paquete hogar", error.Message);
        Assert.True(template.IsPublished);
    }

    [Fact]
    public void Can_unpublish_when_its_bundles_are_not_published()
    {
        var template = TestData.PublishedTemplate("mensual");
        TestData.PublishableBundle("hogar", template, TestData.PublishedTemplate("anual"));

        template.Unpublish();

        Assert.False(template.IsPublished);
    }

    [Fact]
    public void Main_preview_is_the_first_by_position()
    {
        var template = TestData.PublishableTemplate("mensual");
        var second = template.AddPreview();
        second.Position = -1;

        Assert.Same(second, template.MainPreview);
    }
}
