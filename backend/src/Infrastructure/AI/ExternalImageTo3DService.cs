using System.Net.Http.Json;
using Platform.Application.Services;

namespace Platform.Infrastructure.AI;

/// <summary>
/// Calls the standalone Python/FastAPI AI service over HTTP.
/// This is the implementation used in production; swap in
/// LocalImageTo3DService or a FutureCustomModelService for other setups
/// without changing anything that depends on IImageTo3DService.
/// </summary>
public class ExternalImageTo3DService : IImageTo3DService
{
    private readonly HttpClient _http;
    private readonly string? _publicBase;
    private readonly string? _internalBase;

    public ExternalImageTo3DService(HttpClient http, IConfiguration config)
    {
        _http = http; // BaseAddress configured from AiService:BaseUrl in appsettings

        // Storage:PublicBaseUrl (e.g. http://localhost:5000/storage) is what the
        // browser uses. The AI service runs in its own container/host and can't
        // necessarily reach "localhost" for the backend, so when
        // Storage:InternalBaseUrl (e.g. http://backend:8080/storage) is set,
        // rewrite URLs to that before handing them to the AI service.
        _publicBase = config["Storage:PublicBaseUrl"]?.TrimEnd('/');
        _internalBase = config["Storage:InternalBaseUrl"]?.TrimEnd('/');
    }

    private string ToInternalUrl(string url)
    {
        if (!string.IsNullOrEmpty(_publicBase) && !string.IsNullOrEmpty(_internalBase) && url.StartsWith(_publicBase))
        {
            return _internalBase + url[_publicBase.Length..];
        }
        return url;
    }

    public async Task<ImageTo3DResult> GenerateAsync(ImageTo3DRequest request, CancellationToken ct = default)
    {
        var payload = new
        {
            product_id = request.ProductId,
            image_urls = request.ProcessedImageUrls.Select(ToInternalUrl).ToList()
        };

        using var response = await _http.PostAsJsonAsync("/generate", payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return new ImageTo3DResult(false, null, null, null, null,
                $"AI service returned {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<AiServiceResponse>(cancellationToken: ct);
        if (result is null)
        {
            return new ImageTo3DResult(false, null, null, null, null, "Empty response from AI service");
        }

        return new ImageTo3DResult(
            Success: true,
            GlbUrl: result.glb_url,
            ThumbnailUrl: result.thumbnail_url,
            FileSizeBytes: result.file_size_bytes,
            PolygonCount: result.polygon_count,
            ErrorMessage: null);
    }

    private record AiServiceResponse(
        string glb_url,
        string thumbnail_url,
        long file_size_bytes,
        int polygon_count);
}
