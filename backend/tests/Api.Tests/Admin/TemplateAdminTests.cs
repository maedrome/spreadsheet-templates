using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Admin;
using Api.Catalogo;
using Api.Compras;
using Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Api.Tests.Admin;

public class TemplateAdminTests(AdminApiFactory factory) : IClassFixture<AdminApiFactory>
{
    private const string CopyUrl = "https://docs.google.com/spreadsheets/d/1AbC_d-9/copy";

    [Fact]
    public async Task A_template_can_be_built_from_scratch_and_published()
    {
        var client = await factory.CreateAdminClientAsync();

        var created = await client.PostAsJsonAsync("/api/admin/plantillas", Input("desde-cero"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var template = (await created.Content.ReadFromJsonAsync<TemplateDetail>())!;

        var incomplete = await client.PostAsync($"/api/admin/plantillas/{template.Id}/publicar", null);
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        var problem = (await incomplete.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal("No se puede publicar la Plantilla: falta al menos una Preview, el archivo .xlsx, el link de Google Sheets.", problem.Detail);

        template = await UploadPreviewAsync(client, template.Id, Png(2000, 1250));
        var preview = Assert.Single(template.Previews);
        Assert.Equal(PreviewVariant.StandardWidths, preview.Variants.Select(v => v.Width));
        Assert.Equal([300, 600, 1000], preview.Variants.Select(v => v.Height));
        foreach (var variant in preview.Variants)
        {
            var fileName = variant.Url.Split('/')[^1];
            Assert.Matches("^[a-z0-9]+-[0-9]+\\.webp$", fileName);
            using var codec = SKCodec.Create(Path.Combine(factory.StorageRoot, "previews", fileName));
            Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
            Assert.Equal(variant.Width, codec.Info.Width);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(variant.Url)).StatusCode);
        }

        var xlsx = await client.PutAsync($"/api/admin/plantillas/{template.Id}/xlsx", File("presupuesto.xlsx", Xlsx()));
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        Assert.True((await xlsx.Content.ReadFromJsonAsync<TemplateDetail>())!.HasXlsx);
        var xlsxPath = (await XlsxPathAsync(template.Id))!;
        Assert.DoesNotContain(Path.DirectorySeparatorChar, xlsxPath);
        Assert.True(System.IO.File.Exists(Path.Combine(factory.StorageRoot, "templates", xlsxPath)));

        (await client.PutAsJsonAsync($"/api/admin/plantillas/{template.Id}", Input("desde-cero") with { GoogleSheetsCopyUrl = CopyUrl }))
            .EnsureSuccessStatusCode();

        var published = await client.PostAsync($"/api/admin/plantillas/{template.Id}/publicar", null);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var detail = (await published.Content.ReadFromJsonAsync<TemplateDetail>())!;
        Assert.True(detail.IsPublished);
        Assert.True(detail.SlugLocked);
        Assert.Empty(detail.MissingForPublication);
    }

    [Fact]
    public async Task Preview_upload_rejects_files_that_are_not_images()
    {
        var client = await factory.CreateAdminClientAsync();
        var template = await CreateAsync(client, "no-imagen");

        var response = await client.PostAsync(
            $"/api/admin/plantillas/{template.Id}/previews", File("preview.png", "esto no es una imagen"u8.ToArray(), "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("El archivo no es una imagen válida.", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
    }

    [Fact]
    public async Task Preview_upload_rejects_images_narrower_than_the_largest_variant()
    {
        var client = await factory.CreateAdminClientAsync();
        var template = await CreateAsync(client, "angosta");

        var response = await client.PostAsync($"/api/admin/plantillas/{template.Id}/previews", File("preview.png", Png(800, 600), "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("al menos 1600 px", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
    }

    [Fact]
    public async Task Xlsx_upload_rejects_a_file_that_is_not_a_zip()
    {
        var client = await factory.CreateAdminClientAsync();
        var template = await CreateAsync(client, "no-zip");

        var response = await client.PutAsync($"/api/admin/plantillas/{template.Id}/xlsx", File("presupuesto.xlsx", "a,b,c\n1,2,3"u8.ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("El archivo no es un .xlsx válido.", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
        Assert.Null(await XlsxPathAsync(template.Id));
    }

    [Fact]
    public async Task Xlsx_upload_rejects_other_extensions()
    {
        var client = await factory.CreateAdminClientAsync();
        var template = await CreateAsync(client, "otra-extension");

        var response = await client.PutAsync($"/api/admin/plantillas/{template.Id}/xlsx", File("presupuesto.zip", Xlsx()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(".xlsx", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
    }

    [Theory]
    [InlineData("https://docs.google.com/spreadsheets/d/abc/edit")]
    [InlineData("http://docs.google.com/spreadsheets/d/abc/copy")]
    [InlineData("https://evil.example.com/spreadsheets/d/abc/copy")]
    public async Task Google_sheets_link_must_be_a_copy_link(string url)
    {
        var client = await factory.CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/admin/plantillas", Input("link-malo") with { GoogleSheetsCopyUrl = url });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!;
        Assert.Contains("googleSheetsCopyUrl", problem.Errors.Keys);
    }

    [Fact]
    public async Task Previews_can_be_reordered_and_deleted()
    {
        var client = await factory.CreateAdminClientAsync();
        var template = await CreateAsync(client, "orden");
        await UploadPreviewAsync(client, template.Id, Png(1600, 900, SKColors.Red));
        template = await UploadPreviewAsync(client, template.Id, Png(1600, 900, SKColors.Green));
        var (first, second) = (template.Previews[0].Id, template.Previews[1].Id);

        var reordered = await client.PutAsJsonAsync($"/api/admin/plantillas/{template.Id}/previews/orden", new PreviewOrderInput([second, first]));
        Assert.Equal([second, first], (await reordered.Content.ReadFromJsonAsync<TemplateDetail>())!.Previews.Select(p => p.Id));

        var files = template.Previews.Single(p => p.Id == first).Variants.Select(v => v.Url.Split('/')[^1]).ToList();
        var deleted = await client.DeleteAsync($"/api/admin/plantillas/{template.Id}/previews/{first}");
        Assert.Equal([second], (await deleted.Content.ReadFromJsonAsync<TemplateDetail>())!.Previews.Select(p => p.Id));
        Assert.All(files, f => Assert.False(System.IO.File.Exists(Path.Combine(factory.StorageRoot, "previews", f))));
    }

    [Fact]
    public async Task Unpublishing_a_template_in_a_published_bundle_returns_400_with_the_rule()
    {
        var client = await factory.CreateAdminClientAsync();
        var bundle = TestData.PublishableBundle("admin-unpub");
        bundle.Publish(TestData.Now);
        await SaveAsync(db => db.Bundles.Add(bundle));

        var response = await client.PostAsync($"/api/admin/plantillas/{bundle.Templates[0].TemplateId}/despublicar", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("está en Paquetes publicados: Paquete admin-unpub", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
    }

    [Fact]
    public async Task A_template_with_orders_cannot_be_deleted()
    {
        var client = await factory.CreateAdminClientAsync();
        var template = TestData.PublishedTemplate("con-compras");
        await SaveAsync(db => db.Orders.Add(Order.ForTemplate("cliente@example.com", template, 500, "admin-del", TestData.Now)));
        await client.PostAsync($"/api/admin/plantillas/{template.Id}/despublicar", null);

        var response = await client.DeleteAsync($"/api/admin/plantillas/{template.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("tiene Compras", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
    }

    [Fact]
    public async Task A_template_without_orders_can_be_deleted()
    {
        var client = await factory.CreateAdminClientAsync();
        var template = await CreateAsync(client, "borrable");

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/admin/plantillas/{template.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/admin/plantillas/{template.Id}")).StatusCode);
    }

    [Fact]
    public async Task Orders_are_listed_newest_first()
    {
        var client = await factory.CreateAdminClientAsync();
        var template = TestData.PublishedTemplate("listado");
        await SaveAsync(db =>
        {
            db.Orders.Add(Order.ForTemplate("vieja@example.com", template, 500, "admin-list-1", TestData.Now.AddDays(-1)));
            db.Orders.Add(Order.ForTemplate("nueva@example.com", template, 450, "admin-list-2", TestData.Now));
        });

        var orders = (await client.GetFromJsonAsync<List<OrderListItem>>("/api/admin/compras"))!
            .Where(o => o.ProductName == template.Name).ToList();

        Assert.Equal(["nueva@example.com", "vieja@example.com"], orders.Select(o => o.Email));
        Assert.Equal(new OrderListItem(orders[0].Id, "nueva@example.com", "plantilla", template.Name, 450, TestData.Now, "Paid"), orders[0]);
    }

    private static TemplateInput Input(string slug) =>
        new(slug, $"Plantilla {slug}", "Una plantilla de presupuesto.", 900, null, null);

    private static async Task<TemplateDetail> CreateAsync(HttpClient client, string slug)
    {
        var response = await client.PostAsJsonAsync("/api/admin/plantillas", Input(slug));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TemplateDetail>())!;
    }

    private static async Task<TemplateDetail> UploadPreviewAsync(HttpClient client, int templateId, byte[] png)
    {
        var response = await client.PostAsync($"/api/admin/plantillas/{templateId}/previews", File("preview.png", png, "image/png"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TemplateDetail>())!;
    }

    private async Task<string?> XlsxPathAsync(int templateId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Templates.Where(t => t.Id == templateId).Select(t => t.XlsxPath).SingleAsync();
    }

    private async Task SaveAsync(Action<AppDbContext> change)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        change(db);
        await db.SaveChangesAsync();
    }

    internal static MultipartFormDataContent File(string fileName, byte[] bytes, string contentType = "application/octet-stream")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    internal static byte[] Png(int width, int height, SKColor? color = null)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
            canvas.Clear(color ?? SKColors.SteelBlue);
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    /// <summary>Un ZIP mínimo con la estructura de un .xlsx.</summary>
    private static byte[] Xlsx()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry("[Content_Types].xml").Open());
            writer.Write("<Types/>");
        }
        return stream.ToArray();
    }
}
