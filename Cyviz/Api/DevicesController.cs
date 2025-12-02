using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cyviz.Infrastructure;
using Cyviz.Domain;
using Microsoft.Extensions.Caching.Memory;
using Cyviz.Application;

namespace Cyviz.Api;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly CommandRouter _router;

    public DevicesController(AppDbContext db, IMemoryCache cache, CommandRouter router)
    {
        _db = db; _cache = cache; _router = router;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? top, [FromQuery] string? after, [FromQuery] DeviceStatus? status, [FromQuery] DeviceType? type, [FromQuery] string? search)
    {
        var key = (top, after, status, type, search);
        if (_cache.TryGetValue(key, out object? cached) && cached is object res)
            return Ok(res);
        const int defaultPage = 20;
        var take = Math.Clamp(top ?? defaultPage, 1, 100);
        var query = _db.Devices.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(d => d.Status == status);
        if (type.HasValue) query = query.Where(d => d.Type == type);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(d => d.Name.Contains(search));
        if (!string.IsNullOrEmpty(after)) query = query.Where(d => string.Compare(d.Id, after) > 0);
        query = query.OrderBy(d => d.Id);
        var list = await query.Take(take).ToListAsync();
        var next = list.Count == take ? list[^1].Id : null;
        var payload = new { items = list, next };
        _cache.Set(key, payload, TimeSpan.FromSeconds(60));
        return Ok(payload);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var device = await _db.Devices.FindAsync(id);
        if (device == null) return NotFound();
        var tele = await _db.Telemetry.Where(t => t.DeviceId == id).OrderByDescending(t => t.TimestampUtc).Take(50).ToListAsync();
        var etag = device.RowVersion != null ? Convert.ToBase64String(device.RowVersion) : null;
        return Ok(new { device, telemetry = tele, etag });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] Device update)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == id);
        if (device == null) return NotFound();
        var ifMatch = Request.Headers["If-Match"].FirstOrDefault();
        ifMatch = ifMatch?.Trim('"').Replace("W/", "");
    //    if (device.RowVersion == null || ifMatch == null || Convert.ToBase64String(device.RowVersion) != ifMatch)
    //        return StatusCode(412);
        device.Location = update.Location;
        _db.Update(device);
        await _db.SaveChangesAsync();
        return Ok(device);
    }

    public record CommandRequest(string IdempotencyKey, string Command);

    [HttpPost("{id}/commands")]
    public async Task<IActionResult> PostCommand(string id, [FromBody] CommandRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.IdempotencyKey) || string.IsNullOrWhiteSpace(req.Command))
            return BadRequest();
        var result = await _router.EnqueueAsync(id, req.IdempotencyKey, req.Command, HttpContext.RequestAborted);
        if (result.Status == CommandEnqueueResultStatus.QueueFull)
            return StatusCode(429, new { error = "Queue full" });
        return Accepted(new { commandId = result.CommandId });
    }

    [HttpGet("{id}/commands/{commandId}")]
    public async Task<IActionResult> GetCommand(string id, Guid commandId)
    {
        var cmd = await _db.Commands.AsNoTracking().FirstOrDefaultAsync(c => c.Id == commandId && c.DeviceId == id);
        if (cmd == null) return NotFound();
        return Ok(cmd);
    }

    [HttpPost("{id}/heartbeat")]
    public async Task<IActionResult> Heartbeat(string id)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == id);
        if (device == null) return NotFound();
        
        device.LastSeenUtc = DateTime.UtcNow;
        device.Status = DeviceStatus.Online;
        
        await _db.SaveChangesAsync();
        
        return Ok(new { deviceId = id, status = device.Status, lastSeenUtc = device.LastSeenUtc });
    }
}
