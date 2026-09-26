using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.DTOs;
using Platform.Application.Services;
using Platform.Domain.Enums;

namespace Platform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _products;
    private readonly IConfiguration _config;

    public ProductsController(ProductService products, IConfiguration config)
    {
        _products = products;
        _config = config;
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest request, CancellationToken ct)
    {
        try
        {
            var product = await _products.CreateProductAsync(User.GetUserId(), request, ct);
            return Ok(ToDto(product));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> List(CancellationToken ct)
    {
        var products = await _products.ListProductsAsync(User.GetUserId(), ct);
        return Ok(products.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Get(Guid id, CancellationToken ct)
    {
        var product = await _products.GetProductAsync(User.GetUserId(), id, ct);
        return product is null ? NotFound() : Ok(ToDto(product));
    }

    [HttpPost("{id:guid}/images")]
    [RequestSizeLimit(25_000_000)] // 25 MB per image, enforced per section 16 (upload size limits)
    public async Task<ActionResult<ProductImageDto>> UploadImage(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0) return BadRequest(new { message = "Empty file." });

        var allowed = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
        if (!allowed.Contains(file.ContentType))
            return BadRequest(new { message = "Unsupported file type. Use JPG, PNG or WEBP." });

        await using var stream = file.OpenReadStream();
        var image = await _products.AddImageAsync(User.GetUserId(), id, stream, file.FileName, file.ContentType, ct);

        return Ok(new ProductImageDto(image.Id, image.OriginalUrl, image.ProcessedUrl, image.ProcessingStatus));
    }

    [HttpPost("{id:guid}/generate")]
    public async Task<ActionResult<GenerateJobResponse>> Generate(Guid id, CancellationToken ct)
    {
        try
        {
            var model = await _products.QueueGenerationAsync(User.GetUserId(), id, ct);
            return Ok(new GenerateJobResponse(model.Id, model.GenerationStatus));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/regenerate")]
    public Task<ActionResult<GenerateJobResponse>> Regenerate(Guid id, CancellationToken ct) => Generate(id, ct);

    [HttpGet("{id:guid}/model")]
    public async Task<ActionResult<ProductModelDto>> GetModel(Guid id, CancellationToken ct)
    {
        var model = await _products.GetModelAsync(User.GetUserId(), id, ct);
        if (model is null) return NotFound();

        return Ok(new ProductModelDto(
            model.Id, model.ModelUrl, model.ThumbnailUrl, model.Version, model.GenerationStatus, model.ErrorMessage));
    }

    [HttpGet("{id:guid}/embed-code")]
    public ActionResult<EmbedCodeResponse> GetEmbedCode(Guid id)
    {
        var viewerBase = _config["Viewer:BaseUrl"] ?? "https://viewer.example.com";
        var viewerUrl = $"{viewerBase}/embed/{id}";

        var iframe = $"<iframe src=\"{viewerUrl}\" width=\"100%\" height=\"500\" frameborder=\"0\"></iframe>";
        var script = $"<script src=\"{viewerBase}/sdk/viewer.js\"></script>\n" +
                     $"<div class=\"product-viewer\" data-product-id=\"{id}\"></div>";

        return Ok(new EmbedCodeResponse(id.ToString(), iframe, script, viewerUrl));
    }

    private static ProductDto ToDto(Domain.Entities.Product p)
    {
        var latest = p.Models.OrderByDescending(m => m.Version).FirstOrDefault();
        return new ProductDto(
            p.Id, p.Name, p.Description, p.Status, p.CreatedAt,
            p.Images.Select(i => new ProductImageDto(i.Id, i.OriginalUrl, i.ProcessedUrl, i.ProcessingStatus)).ToList(),
            latest is null ? null : new ProductModelDto(latest.Id, latest.ModelUrl, latest.ThumbnailUrl, latest.Version, latest.GenerationStatus, latest.ErrorMessage));
    }
}
