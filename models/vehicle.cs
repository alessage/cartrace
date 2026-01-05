
// --------------------
// Repository interface
// --------------------
using System.Text.Json.Serialization;

public interface IVehicleSnapshotRepository
{
    Task<VehicleSnapshot?> GetByPlateAsync(string plate, CancellationToken ct = default);
}

// --------------------
// Mock repository
// --------------------
public sealed class MockVehicleSnapshotRepository : IVehicleSnapshotRepository
{
    public Task<VehicleSnapshot?> GetByPlateAsync(string plate, CancellationToken ct = default)
    {
        var p = NormalizePlate(plate);

        // Simula "not found"
        if (p is "ZZ999ZZ" or "NOTFOUND")
            return Task.FromResult<VehicleSnapshot?>(null);

        // Mock con varianti per testare casi diversi
        var now = DateTimeOffset.UtcNow;

        var hash = Math.Abs(p.GetHashCode());

        var makes = new[] { "Fiat", "Volkswagen", "Ford", "Mercedes-Benz", "Renault", "Iveco" };
        var models = new Dictionary<string, string[]>
        {
            ["Fiat"] = new[] { "Ducato", "Doblo", "Fiorino", "Scudo" },
            ["Volkswagen"] = new[] { "Transporter", "Caddy", "Crafter" },
            ["Ford"] = new[] { "Transit", "Transit Custom", "Courier" },
            ["Mercedes-Benz"] = new[] { "Sprinter", "Vito" },
            ["Renault"] = new[] { "Master", "Trafic", "Kangoo" },
            ["Iveco"] = new[] { "Daily" }
        };

        var fuels = new[] { "Diesel", "Hybrid", "Electric", "CNG" };


        var partsCatalog = new[]
{
    new { Code = "OIL-5W30-5L", Desc = "Engine oil 5W-30 (5L)", Min=45m, Max=85m },
    new { Code = "FLT-OIL-001",  Desc = "Oil filter",            Min=8m,  Max=18m },
    new { Code = "FLT-AIR-002",  Desc = "Air filter",            Min=10m, Max=25m },
    new { Code = "BRK-PAD-FR",   Desc = "Front brake pads set",  Min=35m, Max=120m },
    new { Code = "BRK-DISC-FR",  Desc = "Front brake discs pair",Min=90m, Max=220m },
    new { Code = "WPR-BL-650",   Desc = "Wiper blades 650mm",     Min=12m, Max=35m },
};

        string[] OrgUnits = new[]
{
    "Milan Hub",
    "Rome Hub",
    "Turin Logistics",
    "Service Ops",
    "Warehouse North",
    "Delivery South"
};

        string[] Roles = new[]
        {
    "Driver",
    "Technician",
    "Supervisor"
};

        List<CurrentUser> GenerateUsers(string plate)
        {
            var h = Math.Abs(plate.GetHashCode());

            var count = 1 + (h % 3); // 1–3 users
            var users = new List<CurrentUser>();

            for (int i = 0; i < count; i++)
            {
                var userSeed = h + i * 97;

                var role = Roles[userSeed % Roles.Length];
                var org = OrgUnits[(userSeed / 3) % OrgUnits.Length];

                // attivi da 1 a 90 giorni
                var days = 1 + (userSeed % 200);

                users.Add(new CurrentUser
                {
                    UserId = $"{role.Substring(0, 1)}_{100 + (userSeed % 900)}",
                    Role = role,
                    OrgUnit = org,
                    Since = DateTimeOffset.UtcNow.AddDays(-days)
                });
            }

            return users;
        }

        decimal PickCost(int seed, decimal min, decimal max)
        {
            var r = (seed % 1000) / 1000m;        // 0..0.999 deterministico
            return Math.Round(min + (max - min) * r, 2);
        }

        List<PartUsed> PickParts(string plate, int eventIndex, int count)
        {
            var h = Math.Abs((plate + "|" + eventIndex).GetHashCode());
            var list = new List<PartUsed>();

            for (int i = 0; i < count; i++)
            {
                var idx = (h + i * 97) % partsCatalog.Length;
                var item = partsCatalog[idx];
                list.Add(new PartUsed
                {
                    Code = item.Code,
                    Description = item.Desc,
                    Cost = PickCost(h + i * 31, item.Min, item.Max)
                });
            }
            return list;
        }

        int BaseKm(string plate)
        {
            var h = Math.Abs(plate.GetHashCode());
            return 20_000 + (h % 180_000); // 20.000 – 200.000 km
        }


        var make = makes[hash % makes.Length];
        var modelList = models[make];
        var model = modelList[hash % modelList.Length];
        var year = 2016 + (hash % 9); // 2016–2024
        var fuel = fuels[(hash / 7) % fuels.Length];

       


        var snapshot = new VehicleSnapshot
        {
            DataAsOf = now,
            Vehicle = new VehicleInfo
            {
                PlateMasked = MaskPlate(p),
                Make = make,
                Model = model,
                Year = year,
                Fuel = fuel
            },
            ServiceEvents = new List<ServiceEvent>
            {
                new ServiceEvent
                {
                    Type = "Service / Oil + Filters",
                    Km = BaseKm(p),
                    Where = "Workshop X (MI)",
                    When = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-85)),
                    Parts = PickParts(p, eventIndex: 0, count: 2)
                },
                new ServiceEvent
                {
                    Type = "Brake pads replacement",
                    Km = BaseKm(p),
                    Where = "Workshop Y (MI)",
                    When = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-145)),
                    Parts = PickParts(p, eventIndex: 0, count: 2)
                }
            },
            CurrentUsers = GenerateUsers(p),
            Warnings = new List<string>()
        };

        // Caso “storia parziale”
        if (p.StartsWith("AA"))
        {
            snapshot.ServiceEvents.Clear();
            snapshot.Warnings.Add("Service history not available for this plate in the mock dataset.");
        }

       
      
       

        // Caso “km sospetti”
        if (p.StartsWith("CC"))
        {
            snapshot.Warnings.Add("Potential odometer inconsistency detected (mock warning).");
        }

        return Task.FromResult<VehicleSnapshot?>(snapshot);
    }

    private static string NormalizePlate(string plate)
        => (plate ?? "").Trim().ToUpperInvariant();

    private static string MaskPlate(string plate)
    {
        if (plate.Length <= 2) return "**";
        if (plate.Length <= 4) return plate[..1] + "**" + plate[^1];
        return plate[..2] + "***" + plate[^2..];
    }
}



// --------------------
// DTOs
// --------------------
public sealed class VehicleSnapshot
{
    [JsonPropertyName("data_as_of")]
    public DateTimeOffset DataAsOf { get; set; }

    [JsonPropertyName("vehicle")]
    public VehicleInfo Vehicle { get; set; } = new();

    [JsonPropertyName("service_events")]
    public List<ServiceEvent> ServiceEvents { get; set; } = new();

    [JsonPropertyName("current_users")]
    public List<CurrentUser> CurrentUsers { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();
}

public sealed class VehicleInfo
{
    [JsonPropertyName("plate_masked")]
    public string PlateMasked { get; set; } = "";

    [JsonPropertyName("make")]
    public string? Make { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("fuel")]
    public string? Fuel { get; set; }
}

public sealed class ServiceEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("km")]
    public int Km { get; set; }

    [JsonPropertyName("where")]
    public string Where { get; set; } = "";

    [JsonPropertyName("when")]
    public DateOnly When { get; set; }
    [JsonPropertyName("parts")]
    public List<PartUsed> Parts { get; set; } = new();
}

public sealed class PartUsed
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("cost")]
    public decimal Cost { get; set; }
}

public sealed class CurrentUser
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = "";

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("org_unit")]
    public string? OrgUnit { get; set; }

    [JsonPropertyName("since")]
    public DateTimeOffset? Since { get; set; }
}
