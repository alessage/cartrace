using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

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

        var sw = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("N");

        if (req == null || string.IsNullOrWhiteSpace(req.Plate))
            return BadRequest(new { error = "Missing plate" });

        var plate = req.Plate.Trim().ToUpperInvariant();

        var snapshot = await _repo.GetByPlateAsync(plate, ct);


        System.Threading.Thread.Sleep(500 * snapshot.ServiceEvents.Count);

        sw.Stop();


        if (snapshot == null)
            return NotFound(new { error = "Vehicle not found" });

        snapshot.RequestId = requestId;
        snapshot.ServerTime = DateTimeOffset.UtcNow;
        snapshot.LatencyMs = (int)sw.ElapsedMilliseconds;

        return Ok(snapshot);
    }
}

public sealed class PlateRequest
{
    public string Plate { get; set; } = "";
}
