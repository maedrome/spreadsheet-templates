using System.Net;
using System.Net.Http.Json;
using Api.Admin;
using Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Admin;

public class BundleAdminTests(AdminApiFactory factory) : IClassFixture<AdminApiFactory>
{
    [Fact]
    public async Task Publishing_a_bundle_that_breaks_the_rules_returns_400_with_the_message()
    {
        var client = await factory.CreateAdminClientAsync();
        var bundle = await CreateAsync(client, "vacio", priceCents: 700);

        var response = await client.PostAsync($"/api/admin/paquetes/{bundle.Id}/publicar", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal("No se puede publicar el Paquete: necesita al menos 2 Plantillas; el Ahorro debe ser mayor que 0.", problem.Detail);
    }

    [Fact]
    public async Task A_bundle_with_published_templates_and_savings_can_be_published()
    {
        var client = await factory.CreateAdminClientAsync();
        var (a, b) = (TestData.PublishedTemplate("paquete-a"), TestData.PublishedTemplate("paquete-b"));
        await SaveAsync(db => db.Templates.AddRange(a, b));
        var bundle = await CreateAsync(client, "completo", priceCents: 1200);

        await client.PostAsJsonAsync($"/api/admin/paquetes/{bundle.Id}/plantillas", new BundleTemplateInput(a.Id));
        var noSavings = await client.PostAsJsonAsync($"/api/admin/paquetes/{bundle.Id}/plantillas", new BundleTemplateInput(b.Id));
        var detail = (await noSavings.Content.ReadFromJsonAsync<BundleDetail>())!;
        Assert.Equal(1000, detail.TemplatesTotalCents);
        Assert.Equal(-200, detail.SavingsCents);
        Assert.Equal(["el Ahorro debe ser mayor que 0"], detail.PublicationProblems);

        var update = await client.PutAsJsonAsync($"/api/admin/paquetes/{bundle.Id}", new BundleInput("completo", "Paquete completo", "Dos plantillas.", 800, null));
        Assert.Equal(200, (await update.Content.ReadFromJsonAsync<BundleDetail>())!.SavingsCents);

        var published = await client.PostAsync($"/api/admin/paquetes/{bundle.Id}/publicar", null);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.True((await published.Content.ReadFromJsonAsync<BundleDetail>())!.IsPublished);

        // Publicado, no puede quedarse sin Ahorro ni con menos de 2 Plantillas.
        var tooExpensive = await client.PutAsJsonAsync($"/api/admin/paquetes/{bundle.Id}", new BundleInput("completo", "Paquete completo", "Dos plantillas.", 1000, null));
        Assert.Equal(HttpStatusCode.BadRequest, tooExpensive.StatusCode);
        var remove = await client.DeleteAsync($"/api/admin/paquetes/{bundle.Id}/plantillas/{a.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, remove.StatusCode);
        Assert.Contains("dejaría de cumplir las reglas", (await remove.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);

        var list = (await client.GetFromJsonAsync<List<BundleListItem>>("/api/admin/paquetes"))!;
        Assert.Contains(list, item => item.Id == bundle.Id && item.IsPublished && item.TemplateCount == 2 && item.SavingsCents == 200);
    }

    [Fact]
    public async Task A_published_bundle_cannot_be_deleted()
    {
        var client = await factory.CreateAdminClientAsync();
        var bundle = TestData.PublishableBundle("borrar-publicado");
        bundle.Publish(TestData.Now);
        await SaveAsync(db => db.Bundles.Add(bundle));

        var response = await client.DeleteAsync($"/api/admin/paquetes/{bundle.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Despublica el Paquete antes de borrarlo.", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
    }

    private static async Task<BundleDetail> CreateAsync(HttpClient client, string slug, int priceCents)
    {
        var response = await client.PostAsJsonAsync("/api/admin/paquetes", new BundleInput(slug, $"Paquete {slug}", "Un paquete.", priceCents, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BundleDetail>())!;
    }

    private async Task SaveAsync(Action<AppDbContext> change)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        change(db);
        await db.SaveChangesAsync();
    }
}
