namespace Platform.Application.Services;

public record ImageTo3DRequest(Guid ProductId, IReadOnlyList<string> ProcessedImageUrls);

public record ImageTo3DResult(
    bool Success,
    string? GlbUrl,
    string? ThumbnailUrl,
    long? FileSizeBytes,
    int? PolygonCount,
    string? ErrorMessage);

/// <summary>
/// Abstraction over whatever image-to-3D backend generates the mesh.
/// Never call a specific AI model directly from application/domain code —
/// always go through this interface so the underlying model/service can be
/// swapped (local model, external API, future custom model) without touching
/// callers such as JobService.
/// </summary>
public interface IImageTo3DService
{
    Task<ImageTo3DResult> GenerateAsync(ImageTo3DRequest request, CancellationToken ct = default);
}
