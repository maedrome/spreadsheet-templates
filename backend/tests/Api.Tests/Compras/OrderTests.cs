using Api.Compras;

namespace Api.Tests.Compras;

public class OrderTests
{
    [Fact]
    public void Bundle_order_snapshots_the_templates_included_when_paying()
    {
        var a = TestData.PublishedTemplate("a");
        var b = TestData.PublishedTemplate("b");
        var bundle = TestData.PublishableBundle("hogar", a, b);
        bundle.Publish(TestData.Now);

        var order = Order.ForBundle("cliente@example.com", bundle, 700, "polar_1", TestData.Now);
        bundle.AddTemplate(TestData.PublishedTemplate("c"));

        Assert.Equal([a, b], order.Templates.Select(ot => ot.Template));
    }

    [Fact]
    public void Template_order_includes_only_that_template()
    {
        var template = TestData.PublishedTemplate("a");

        var order = Order.ForTemplate("cliente@example.com", template, 500, "polar_1", TestData.Now);

        Assert.Same(template, Assert.Single(order.Templates).Template);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void Email_is_normalized()
    {
        var order = Order.ForTemplate("  Cliente@Example.COM ", TestData.PublishedTemplate("a"), 500, "polar_1", TestData.Now);

        Assert.Equal("cliente@example.com", order.Email);
    }

    [Fact]
    public void Purchase_link_stores_only_the_hash_of_its_token()
    {
        var order = Order.ForTemplate("cliente@example.com", TestData.PublishedTemplate("a"), 500, "polar_1", TestData.Now);

        var first = order.IssuePurchaseLink(TestData.Now);
        var second = order.IssuePurchaseLink(TestData.Now);

        Assert.NotEqual(first, second);
        Assert.Equal(2, order.PurchaseLinks.Count);
        Assert.Equal(PurchaseLink.HashToken(first), order.PurchaseLinks[0].TokenHash);
        Assert.Equal(32, order.PurchaseLinks[0].TokenHash.Length);
        Assert.True(first.Length >= 43);
    }

    [Fact]
    public void Refund_marks_the_order_as_refunded()
    {
        var order = Order.ForTemplate("cliente@example.com", TestData.PublishedTemplate("a"), 500, "polar_1", TestData.Now);

        order.Refund(TestData.Now.AddDays(1));

        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Equal(TestData.Now.AddDays(1), order.RefundedAt);
    }
}
