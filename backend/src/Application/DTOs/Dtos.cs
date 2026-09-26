using Platform.Domain.Enums;

namespace Platform.Application.DTOs;

public record RegisterRequest(string Email, string Password, string CompanyName);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, Guid UserId);

public record CreateProductRequest(string Name, string? Description);

public record ProductDto(
    Guid Id,
    string Name,
    string? Description,
    ProductStatus Status,
    DateTime CreatedAt,
    List<ProductImageDto> Images,
    ProductModelDto? LatestModel);

public record ProductImageDto(Guid Id, string OriginalUrl, string? ProcessedUrl, ImageProcessingStatus Status);

public record ProductModelDto(
    Guid Id,
    string? ModelUrl,
    string? ThumbnailUrl,
    int Version,
    JobStatus Status,
    string? ErrorMessage);

public record GenerateJobResponse(Guid JobId, JobStatus Status);

public record EmbedCodeResponse(string ProductId, string IframeSnippet, string ScriptSnippet, string ViewerUrl);

public record CreateApiKeyRequest(string Name);
public record ApiKeyDto(Guid Id, string Name, string? PlainTextKey, DateTime CreatedAt, DateTime? LastUsedAt, bool IsActive);
