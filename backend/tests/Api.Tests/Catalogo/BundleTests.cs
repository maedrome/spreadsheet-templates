using Api.Catalogo;
using Api.Common;

namespace Api.Tests.Catalogo;

public class BundleTests
{
    [Fact]
    public void Savings_is_computed_from_current_template_prices()
    {
        var a = TestData.PublishedTemplate("a", 500);
        var b = TestData.PublishedTemplate("b", 500);
        var bundle = TestData.PublishableBundle("hogar", a, b);

        Assert.Equal(300, bundle.SavingsCents);

        a.PriceCents = 900;

        Assert.Equal(700, bundle.SavingsCents);
    }

    [Fact]
    public void Publish_succeeds_with_two_published_templates_and_positive_savings()
    {
        var bundle = TestData.PublishableBundle("hogar");

        bundle.Publish(TestData.Now);

        Assert.True(bundle.IsPublished);
        Assert.Equal(TestData.Now, bundle.FirstPublishedAt);
    }

    [Fact]
    public void Publish_fails_with_fewer_than_two_templates()
    {
        var bundle = TestData.PublishableBundle("hogar", TestData.PublishedTemplate("a", 2000));

        var error = Assert.Throws<DomainException>(() => bundle.Publish(TestData.Now));

        Assert.Contains("al menos 2 Plantillas", error.Message);
    }

    [Fact]
    public void Publish_fails_when_a_template_is_not_published()
    {
        var bundle = TestData.PublishableBundle("hogar", TestData.PublishedTemplate("a"), TestData.PublishableTemplate("b"));

        var error = Assert.Throws<DomainException>(() => bundle.Publish(TestData.Now));

        Assert.Contains("publicadas", error.Message);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(1200)]
    public void Publish_fails_when_savings_is_not_positive(int bundlePriceCents)
    {
        var bundle = TestData.PublishableBundle("hogar");
        bundle.PriceCents = bundlePriceCents;

        var error = Assert.Throws<DomainException>(() => bundle.Publish(TestData.Now));

        Assert.Contains("Ahorro", error.Message);
        Assert.False(bundle.IsPublished);
    }

    [Fact]
    public void Slug_cannot_change_once_published()
    {
        var bundle = TestData.PublishableBundle("hogar");
        bundle.Publish(TestData.Now);
        bundle.Unpublish();

        Assert.Throws<DomainException>(() => bundle.ChangeSlug("casa"));
    }

    [Fact]
    public void Cannot_add_an_unpublished_template_to_a_published_bundle()
    {
        var bundle = TestData.PublishableBundle("hogar");
        bundle.Publish(TestData.Now);

        Assert.Throws<DomainException>(() => bundle.AddTemplate(TestData.PublishableTemplate("c")));
    }

    [Fact]
    public void Cannot_remove_a_template_if_the_published_bundle_would_break_the_rules()
    {
        var a = TestData.PublishedTemplate("a");
        var bundle = TestData.PublishableBundle("hogar", a, TestData.PublishedTemplate("b"));
        bundle.Publish(TestData.Now);

        Assert.Throws<DomainException>(() => bundle.RemoveTemplate(a));
        Assert.Equal(2, bundle.Templates.Count);
    }

    [Fact]
    public void Templates_keep_the_order_they_were_added_in()
    {
        var bundle = TestData.PublishableBundle("hogar");

        Assert.Equal([0, 1], bundle.Templates.Select(bt => bt.Position));
    }
}
