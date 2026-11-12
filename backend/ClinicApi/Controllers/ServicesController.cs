using ClinicApi.Data;
using ClinicApi.Dtos;
using ClinicApi.Models;
using ClinicApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicApi.Controllers;

/// <summary>Catalog of billable add-on services. Doctors pick from it during a consultation.</summary>
[Route("api/[controller]"), Authorize]
public class ServicesController(ClinicDbContext db) : ApiBase
{
    [HttpGet]
    public async Task<List<ClinicService>> List([FromQuery] bool all = false)
    {
        var q = db.Services.AsQueryable();
        if (!(all && CurrentRole == UserRole.Admin)) q = q.Where(s => s.IsActive);
        return await q.OrderBy(s => s.Name).ToListAsync();
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<ActionResult<ClinicService>> Create(ServiceRequest req)
    {
        var s = new ClinicService { Name = req.Name.Trim(), Description = req.Description?.Trim(), Fee = req.Fee, IsActive = req.IsActive };
        db.Services.Add(s);
        await db.SaveChangesAsync();
        return StatusCode(201, s);
    }

    [HttpPut("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<ActionResult<ClinicService>> Update(int id, ServiceRequest req)
    {
        var s = await db.Services.FindAsync(id);
        if (s is null) return NotFound();
        s.Name = req.Name.Trim();
        s.Description = req.Description?.Trim();
        s.Fee = req.Fee;
        s.IsActive = req.IsActive;
        await db.SaveChangesAsync();
        return s;
    }
}
