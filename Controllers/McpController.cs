using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace CarTrace.Mcp.Controllers;

[ApiController]
[Route("api/mcp")]
public sealed class McpController : ControllerBase
{
    private readonly IVehicleSnapshotRepository _repo;

    public McpController(IVehicleSnapshotRepository repo) => _repo = repo;

    
    [HttpGet]
    public IActionResult GetTools()
    {
        return Ok(new
        {
            tools = new object[]
            {
                new
                {
                    name = "get_vehicle_snapshot_by_plate",
                    description = "Given a license plate, returns generic vehicle info, service events (type/km/where/when) and current corporate users.",
                    input_schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            plate = new { type = "string", description = "Vehicle license plate (e.g., AB123CD)" }
                        },
                        required = new[] { "plate" }
                    }
                }
            }
        });
    }

    // MCP: call_tool
    [HttpPost]
    public async Task<IActionResult> CallTool([FromBody] McpCallRequest req, CancellationToken ct)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Tool))
            return BadRequest(new { error = "Invalid MCP request" });

        if (!string.Equals(req.Tool, "get_vehicle_snapshot_by_plate", StringComparison.Ordinal))
            return BadRequest(new { error = "Unknown tool" });

        if (req.Arguments == null || !req.Arguments.TryGetValue("plate", out var plateObj))
            return BadRequest(new { error = "Missing argument: plate" });

        var plate = (plateObj ?? "").ToString()!.Trim();
        if (plate.Length < 5)
            return BadRequest(new { error = "Invalid plate" });

        var snapshot = await _repo.GetByPlateAsync(plate, ct);

        if (snapshot is null)
        {
            // MCP standard-like response: no data but structured output
            return Ok(new
            {
                content = new object[]
                {
                    new
                    {
                        type = "json",
                        json = new
                        {
                            data_as_of = DateTimeOffset.UtcNow,
                            vehicle = new { plate_masked = MaskPlate(plate) },
                            service_events = Array.Empty<object>(),
                            current_users = Array.Empty<object>(),
                            warnings = new[] { "Plate not found." }
                        }
                    }
                }
            });
        }

        return Ok(new
        {
            content = new object[]
            {
                new { type = "json", json = snapshot }
            }
        });
    }

    private static string MaskPlate(string plate)
    {
        var p = (plate ?? "").Trim().ToUpperInvariant();
        if (p.Length <= 2) return "**";
        if (p.Length <= 4) return p[..1] + "**" + p[^1];
        return p[..2] + "***" + p[^2..];
    }
}

public sealed class McpCallRequest
{
    [JsonPropertyName("tool")]
    public string Tool { get; set; } = "";

    [JsonPropertyName("arguments")]
    public Dictionary<string, object?> Arguments { get; set; } = new();
}
