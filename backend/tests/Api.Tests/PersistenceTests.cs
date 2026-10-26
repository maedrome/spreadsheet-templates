using Api.Catalogo;
using Api.Common;
using Api.Compras;
using Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public class PersistenceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private AppDbContext NewContext()
    {
        // Crear el cliente arranca la API, que aplica las migraciones.
        factory.CreateClient();
        return factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
    }

    [Fact]
    public async Task Migrations_apply_on_startup_and_match_the_model()
    {
        await using var db = NewContext();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Catalog_and_order_round_trip()
    {
        string token;
        await using (var db = NewContext())
        {
            var bundle = TestData.PublishableBundle("rt-hogar");
            bundle.Publish(TestData.Now);
            db.Bundles.Add(bundle);
            var order = Order.ForBundle("cliente@example.com", bundle, 700, "rt-polar", TestData.Now);
            token = order.IssuePurchaseLink(TestData.Now);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var hash = PurchaseLink.HashToken(token);
            var order = await db.Orders
                .Where(o => o.PurchaseLinks.Any(pl => pl.TokenHash == hash))
                .Include(o => o.Bundle)
                .Include(o => o.Templates).ThenInclude(ot => ot.Template).ThenInclude(t => t.Previews)
                .SingleAsync();

            Assert.Equal("rt-hogar", order.Bundle!.Slug);
            Assert.Equal(["rt-hogar-a", "rt-hogar-b"], order.Templates.Select(ot => ot.Template.Slug).Order());
            var variant = Assert.Single(order.Templates[0].Template.MainPreview!.Variants);
            Assert.Equal(640, variant.Width);
        }
    }

    [Fact]
    public async Task Template_with_orders_cannot_be_deleted()
    {
        int templateId;
        await using (var db = NewContext())
        {
            var bundle = TestData.PublishableBundle("del-hogar");
            bundle.Publish(TestData.Now);
            db.Orders.Add(Order.ForBundle("cliente@example.com", bundle, 700, "del-polar", TestData.Now));
            await db.SaveChangesAsync();
            templateId = bundle.Templates[0].TemplateId;
        }

        await using (var db = NewContext())
        {
            db.Templates.Remove(await db.Templates.SingleAsync(t => t.Id == templateId));

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Template_without_orders_can_be_deleted_with_its_previews()
    {
        int templateId;
        await using (var db = NewContext())
        {
            var template = TestData.PublishableTemplate("sin-compras");
            db.Templates.Add(template);
            await db.SaveChangesAsync();
            templateId = template.Id;
        }

        await using (var db = NewContext())
        {
            db.Templates.Remove(await db.Templates.SingleAsync(t => t.Id == templateId));
            await db.SaveChangesAsync();

            Assert.False(await db.Previews.AnyAsync(p => p.TemplateId == templateId));
        }
    }

    [Fact]
    public async Task Polar_order_id_is_unique()
    {
        await using var db = NewContext();
        var template = TestData.PublishedTemplate("dup-polar");
        db.Orders.Add(Order.ForTemplate("a@example.com", template, 500, "dup-polar", TestData.Now));
        await db.SaveChangesAsync();

        db.Orders.Add(Order.ForTemplate("a@example.com", template, 500, "dup-polar", TestData.Now));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Template_slug_is_unique()
    {
        await using var db = NewContext();
        db.Templates.Add(TestData.PublishableTemplate("dup-slug"));
        await db.SaveChangesAsync();

        db.Templates.Add(TestData.PublishableTemplate("dup-slug"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Unpublish_rule_holds_for_data_loaded_from_the_database()
    {
        int templateId;
        await using (var db = NewContext())
        {
            var bundle = TestData.PublishableBundle("unpub-hogar");
            bundle.Publish(TestData.Now);
            db.Bundles.Add(bundle);
            await db.SaveChangesAsync();
            templateId = bundle.Templates[0].TemplateId;
        }

        await using (var db = NewContext())
        {
            var template = await db.Templates
                .Include(t => t.BundleTemplates).ThenInclude(bt => bt.Bundle)
                .SingleAsync(t => t.Id == templateId);

            Assert.Throws<DomainException>(template.Unpublish);
        }
    }
}
