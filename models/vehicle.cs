
// --------------------
// Repository interface
// --------------------
using System;
using System.Net.Sockets;
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

        string GenerateVin(string plate)
        {
            const string chars = "ABCDEFGHJKLMNPRSTUVWXYZ0123456789"; // no I,O,Q
            var h = Math.Abs(plate.GetHashCode());

            var vin = new char[17];

            for (int i = 0; i < 17; i++)
            {
                vin[i] = chars[(h + i * 37) % chars.Length];
            }

            // Costruiamo un prefisso realistico (WMI)
            var wmi = new[] { "ZFA", "WVW", "WF0", "WDB", "VF1", "ZCF" };
            var prefix = wmi[h % wmi.Length];

            vin[0] = prefix[0];
            vin[1] = prefix[1];
            vin[2] = prefix[2];

            return new string(vin);
        }

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

        string GeneratePracticeNumber(string plate, int eventIndex)
        {
            var h = Math.Abs((plate + "|" + eventIndex).GetHashCode());
            // formato: PR-2026-XXXXXX
            var seq = 100000 + (h % 900000);
            return $"PR-2026-{seq}";
        }


        var make = makes[hash % makes.Length];
        var modelList = models[make];
        var model = modelList[hash % modelList.Length];
        var year = 2005 + (hash % 9); // 2016–2024
        var fuel = fuels[(hash / 7) % fuels.Length];


        double ComputeConfidence(VehicleSnapshot s)
        {
            var score = 0.95;

            score -= s.ServiceEvents.Count * 0.025;
            score -= s.CurrentUsers.Count * 0.010;

           
            // penalizza in base alla severità warning_details
            foreach (var w in s.WarningDetails)
            {
                score -= w.Severity switch
                {
                    "high" => 0.25,
                    "medium" => 0.12,
                    _ => 0.05
                };
            }

            // clamp 0..1
            if (score < 0.05) score = 0.05;
            if (score > 0.99) score = 0.99;

            return Math.Round(score, 2);
        }

        var snapshot = new VehicleSnapshot
        {
            DataAsOf = now,
            Vehicle = new VehicleInfo
            {
                PlateMasked = MaskPlate(p),
                Vin = GenerateVin(p),
                Make = make,
                Model = model,
                Year = year,
                Fuel = fuel
            },
            CurrentUsers = GenerateUsers(p),
            Warnings = new List<string>()
        };

        Random rnd = new Random();
        int r = rnd.Next(1, 3);


        var serviceType = new[] { "mechanics", "body", "glass", "tires", "revision" };

        for (int i = 0; i <= r; i++)
        {
            snapshot.ServiceEvents.Add(
                new ServiceEvent
                {
                    PracticeNumber = GeneratePracticeNumber(p, 1),
                    Type = serviceType[i],
                    Km = BaseKm(p),
                    Where = "Workshop " + Math.Abs(plate.GetHashCode()),
                    When = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-145)),
                    Backoffice = "BCK_" + Math.Abs(plate.GetHashCode()),
                    Technician = "TECH_" + Math.Abs(plate.GetHashCode()),
                    Parts = PickParts(p, eventIndex: 0, count: 2)
                }
                );
        }

        foreach (var ev in snapshot.ServiceEvents)
        {
            ev.PartsTotalCost = ev.Parts.Sum(x => x.Cost);
            Random rand = new Random();
            ev.Labor = rand.Next(751) / 100.0 + 25.0;
        }

        snapshot.Confidence = ComputeConfidence(snapshot);

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
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }  // 0..1

    [JsonPropertyName("vehicle")]
    public VehicleInfo Vehicle { get; set; } = new();

    [JsonPropertyName("service_events")]
    public List<ServiceEvent> ServiceEvents { get; set; } = new();

    [JsonPropertyName("current_users")]
    public List<CurrentUser> CurrentUsers { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    [JsonPropertyName("warning_details")]
    public List<WarningDetail> WarningDetails { get; set; } = new();
}

public sealed class WarningDetail
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";   // es. ODOMETER_INCONSISTENCY

    [JsonPropertyName("message")]
    public string Message { get; set; } = ""; // descrizione leggibile

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "info"; // info|medium|high
}

public sealed class VehicleInfo
{
    [JsonPropertyName("plate_masked")]
    public string PlateMasked { get; set; } = "";

    [JsonPropertyName("vin")]
    public string? Vin { get; set; }

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
    [JsonPropertyName("practice_number")]
    public string PracticeNumber { get; set; } = "";
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("km")]
    public int Km { get; set; }

    [JsonPropertyName("where")]
    public string Where { get; set; } = "";

    [JsonPropertyName("when")]
    public DateOnly When { get; set; }

    [JsonPropertyName("backoffice")]
    public string Backoffice { get; set; } = "";

    [JsonPropertyName("technician")]
    public string Technician { get; set; } = "";

    [JsonPropertyName("parts_total_cost")]
    public decimal PartsTotalCost { get; set; }
    [JsonPropertyName("labor")]
    public double Labor { get; set; }

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
