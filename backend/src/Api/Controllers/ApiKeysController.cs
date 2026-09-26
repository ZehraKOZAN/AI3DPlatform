using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Application.DTOs;
using Platform.Domain.Entities;
using Platform.Infrastructure.Database;

namespace Platform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/api-keys")]
public class ApiKeysController : ControllerBase
{
    private readonly AppDbContext _db;

    public ApiKeysController(AppDbContext db) => _db = db;

    [HttpPost]
    public async Task<ActionResult<ApiKeyDto>> Create(CreateApiKeyRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var plainKey = $"sk_live_{Convert.ToHexString(RandomNumberGenerator.GetBytes(24))}";

        var apiKey = new ApiKey
        {
            UserId = userId,
            Name = request.Name,
            KeyHash = BCrypt.Net.BCrypt.HashPassword(plainKey)
        };

        _db.ApiKeys.Add(apiKey);
        await _db.SaveChangesAsync(ct);

        // Plain-text key is only ever returned once, at creation time.
        return Ok(new ApiKeyDto(apiKey.Id, apiKey.Name, plainKey, apiKey.CreatedAt, null, true));
    }

    [HttpGet]
    public async Task<ActionResult<List<ApiKeyDto>>> List(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var keys = await _db.ApiKeys.Where(k => k.UserId == userId).ToListAsync(ct);
        return Ok(keys.Select(k => new ApiKeyDto(k.Id, k.Name, null, k.CreatedAt, k.LastUsedAt, k.IsActive)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var key = await _db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.UserId == userId, ct);
        if (key is null) return NotFound();

        key.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
