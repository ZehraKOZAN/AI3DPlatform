using Microsoft.EntityFrameworkCore;
using Platform.Application.DTOs;
using Platform.Domain.Entities;
using Platform.Domain.Enums;
using Platform.Infrastructure.Database;
using Platform.Infrastructure.Queue;
using Platform.Infrastructure.Storage;

namespace Platform.Application.Services;

public class ProductService
{
    private readonly AppDbContext _db;
    private readonly IObjectStorageService _storage;
    private readonly IJobQueue _queue;

    public ProductService(AppDbContext db, IObjectStorageService storage, IJobQueue queue)
    {
        _db = db;
        _storage = storage;
        _queue = queue;
    }

    public async Task<Product> CreateProductAsync(Guid userId, CreateProductRequest request, CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId, ct))
            throw new UnauthorizedAccessException("Bu oturum artık geçerli değil. Lütfen tekrar giriş yapın.");

        var product = new Product
        {
            UserId = userId,
            Name = request.Name,
            Description = request.Description
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        return product;
    }

    /// <summary>Every read is scoped to userId — this is the tenant-isolation guarantee.</summary>
    public Task<List<Product>> ListProductsAsync(Guid userId, CancellationToken ct) =>
        _db.Products
           .Where(p => p.UserId == userId)
           .Include(p => p.Images)
           .Include(p => p.Models)
           .OrderByDescending(p => p.CreatedAt)
           .ToListAsync(ct);

    public Task<Product?> GetProductAsync(Guid userId, Guid productId, CancellationToken ct) =>
        _db.Products
           .Where(p => p.UserId == userId && p.Id == productId)
           .Include(p => p.Images)
           .Include(p => p.Models)
           .FirstOrDefaultAsync(ct);

    public async Task<ProductImage> AddImageAsync(
        Guid userId, Guid productId, Stream fileContent, string fileName, string contentType, CancellationToken ct)
    {
        var product = await GetProductAsync(userId, productId, ct)
            ?? throw new InvalidOperationException("Product not found or not owned by this user.");

        var key = $"{userId}/{productId}/original/{Guid.NewGuid()}_{fileName}";
        var url = await _storage.UploadAsync(key, fileContent, contentType, ct);

        var image = new ProductImage
        {
            ProductId = productId,
            OriginalUrl = url,
            ProcessingStatus = ImageProcessingStatus.Pending
        };

        _db.ProductImages.Add(image);
        product.Status = ProductStatus.ImagesUploaded;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return image;
    }

    public async Task<ProductModel> QueueGenerationAsync(Guid userId, Guid productId, CancellationToken ct)
    {
        var product = await GetProductAsync(userId, productId, ct)
            ?? throw new InvalidOperationException("Product not found or not owned by this user.");

        if (product.Images.Count == 0)
            throw new InvalidOperationException("Upload at least one image before generating a 3D model.");

        var nextVersion = (product.Models.Count == 0 ? 0 : product.Models.Max(m => m.Version)) + 1;

        var model = new ProductModel
        {
            ProductId = productId,
            Version = nextVersion,
            GenerationStatus = JobStatus.Queued
        };
        _db.ProductModels.Add(model);

        product.Status = ProductStatus.Processing;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _queue.EnqueueAsync(new GenerationJobMessage(model.Id, productId, userId), ct);

        return model;
    }

    public Task<ProductModel?> GetModelAsync(Guid userId, Guid productId, CancellationToken ct) =>
        _db.ProductModels
           .Where(m => m.ProductId == productId && m.Product!.UserId == userId)
           .OrderByDescending(m => m.Version)
           .FirstOrDefaultAsync(ct);
}
