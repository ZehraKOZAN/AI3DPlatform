using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Platform.Api.Controllers;
using Platform.Application.Services;
using Platform.Infrastructure.AI;
using Platform.Infrastructure.Database;
using Platform.Infrastructure.Queue;
using Platform.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// --- Database -------------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// --- Storage / Queue (swap implementations here for production) ----------
var storageRootPath = builder.Configuration["Storage:LocalPath"] ?? "./storage";

builder.Services.AddSingleton<IObjectStorageService>(_ =>
    new LocalObjectStorageService(
        rootPath: storageRootPath,
        publicBaseUrl: builder.Configuration["Storage:PublicBaseUrl"] ?? "http://localhost:5000/storage"));

builder.Services.AddSingleton<IJobQueue, InMemoryJobQueue>();

// --- AI service client ------------------------------------------------
builder.Services.AddHttpClient<IImageTo3DService, ExternalImageTo3DService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AiService:BaseUrl"] ?? "http://localhost:8000");
    client.Timeout = TimeSpan.FromMinutes(5); // 3D generation can be slow
});

// --- Application services --------------------------------------------
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddHostedService<GenerationWorker>();

// --- Auth ---------------------------------------------------------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
        };
    });
builder.Services.AddAuthorization();

// --- CORS: e-commerce sites need to call this API/embed the viewer -------
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        // In production, restrict AllowAnyOrigin to registered merchant domains.
    });
});

// --- Rate limiting (basic fixed window; tune per plan in production) -----
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 120;
        opt.QueueLimit = 0;
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Dev/MVP convenience: create the schema directly from the model instead of
// requiring a separate `dotnet ef database update` step. Replace with proper
// EF Core migrations (dotnet ef migrations add / database update) before
// production, so schema changes are versioned and reviewable.
//
// Retries with backoff: Postgres may still be finishing startup even after
// its container is "running" (docker-compose healthchecks mitigate this too,
// but this keeps the app resilient in any environment).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    const int maxAttempts = 10;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            db.Database.EnsureCreated();
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(ex, "Database not ready yet (attempt {Attempt}/{Max}). Retrying in 3s...", attempt, maxAttempts);
            Thread.Sleep(3000);
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Default");

Directory.CreateDirectory(storageRootPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.GetFullPath(storageRootPath)),
    RequestPath = "/storage"
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireRateLimiting("api");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
