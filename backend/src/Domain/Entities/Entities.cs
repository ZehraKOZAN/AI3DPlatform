using Platform.Domain.Enums;

namespace Platform.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string CompanyName { get; set; } = default!;
    public Plan Plan { get; set; } = Plan.Free;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Product> Products { get; set; } = new();
    public List<ApiKey> ApiKeys { get; set; } = new();
}

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public ProductStatus Status { get; set; } = ProductStatus.Draft;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ProductImage> Images { get; set; } = new();
    public List<ProductModel> Models { get; set; } = new();
}

public class ProductImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public string OriginalUrl { get; set; } = default!;
    public string? ProcessedUrl { get; set; }
    public ViewAngle ViewAngle { get; set; } = ViewAngle.Unknown;
    public double? QualityScore { get; set; }
    public ImageProcessingStatus ProcessingStatus { get; set; } = ImageProcessingStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ProductModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public string? ModelUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public long? FileSizeBytes { get; set; }
    public int? PolygonCount { get; set; }
    public int Version { get; set; } = 1;
    public JobStatus GenerationStatus { get; set; } = JobStatus.Queued;
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string KeyHash { get; set; } = default!;
    public string Name { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
