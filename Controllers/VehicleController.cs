using Microsoft.AspNetCore.Mvc;

namespace CarTrace.Mcp.Controllers;

[ApiController]
[Route("api/vehicle")]
public sealed class VehicleController : ControllerBase
{
    private readonly IVehicleSnapshotRepository _repo;

    public VehicleController(IVehicleSnapshotRepository repo)
    {
        _repo = repo;
    }

    // POST /api/vehicle/snapshot
    [HttpPost("snapshot")]
    public async Task<IActionResult> GetSnapshot([FromBody] PlateRequest req, CancellationToken ct)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Plate))
            return BadRequest(new { error = "Missing plate" });

        var plate = req.Plate.Trim().ToUpperInvariant();

        var snapshot = await _repo.GetByPlateAsync(plate, ct);

        if (snapshot == null)
            return NotFound(new { error = "Vehicle not found" });

        return Ok(snapshot);
    }
}

public sealed class PlateRequest
{
    public string Plate { get; set; } = "";
}
