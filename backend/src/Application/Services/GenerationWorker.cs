using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Domain.Enums;
using Platform.Infrastructure.Database;
using Platform.Infrastructure.Queue;

namespace Platform.Application.Services;

/// <summary>
/// Hosted background service implementing the async pipeline described in the
/// spec: QUEUED -> PROCESSING_IMAGES -> GENERATING_3D -> OPTIMIZING_MODEL ->
/// UPLOADING_MODEL -> COMPLETED (or FAILED with a message). Runs out-of-process
/// from the request/response cycle so uploads never block on 3D generation.
/// </summary>
public class GenerationWorker : BackgroundService
{
    private readonly IJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GenerationWorker> _logger;

    public GenerationWorker(IJobQueue queue, IServiceScopeFactory scopeFactory, ILogger<GenerationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(message, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generation job {JobId} failed", message.JobId);
                await MarkFailedAsync(message.JobId, ex.Message, stoppingToken);
            }
        }
    }

    private async Task ProcessJobAsync(GenerationJobMessage message, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var aiService = scope.ServiceProvider.GetRequiredService<IImageTo3DService>();

        var model = await db.ProductModels.FirstAsync(m => m.Id == message.JobId, ct);
        var product = await db.Products
            .Include(p => p.Images)
            .FirstAsync(p => p.Id == message.ProductId, ct);

        await SetStatusAsync(db, model, JobStatus.ProcessingImages, ct);
        var imageUrls = product.Images
            .Select(i => i.ProcessedUrl ?? i.OriginalUrl)
            .ToList();

        await SetStatusAsync(db, model, JobStatus.Generating3D, ct);
        var result = await aiService.GenerateAsync(new ImageTo3DRequest(product.Id, imageUrls), ct);

        if (!result.Success)
        {
            await MarkFailedAsync(model.Id, result.ErrorMessage ?? "Unknown AI service error", ct);
            return;
        }

        // Mesh/texture optimization (polygon reduction, LOD, GLB packaging) is
        // performed inside the AI service pipeline; here we just record the result.
        await SetStatusAsync(db, model, JobStatus.OptimizingModel, ct);
        await SetStatusAsync(db, model, JobStatus.UploadingModel, ct);

        model.ModelUrl = result.GlbUrl;
        model.ThumbnailUrl = result.ThumbnailUrl;
        model.FileSizeBytes = result.FileSizeBytes;
        model.PolygonCount = result.PolygonCount;
        model.GenerationStatus = JobStatus.Completed;

        product.Status = ProductStatus.Ready;
        product.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    private static async Task SetStatusAsync(AppDbContext db, Domain.Entities.ProductModel model, JobStatus status, CancellationToken ct)
    {
        model.GenerationStatus = status;
        await db.SaveChangesAsync(ct);
    }

    private async Task MarkFailedAsync(Guid modelId, string errorMessage, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var model = await db.ProductModels.FindAsync(new object?[] { modelId }, ct);
        if (model is null) return;

        model.GenerationStatus = JobStatus.Failed;
        model.ErrorMessage = errorMessage;

        var product = await db.Products.FindAsync(new object?[] { model.ProductId }, ct);
        if (product is not null) product.Status = ProductStatus.Failed;

        await db.SaveChangesAsync(ct);
    }
}
