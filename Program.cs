using Amazon.Lambda.AspNetCoreServer.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowChatGPT", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// --------------------
// MVC + JSON
// --------------------
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// --------------------
// Repository (mock per ora)
// --------------------
builder.Services.AddSingleton<IVehicleSnapshotRepository, MockVehicleSnapshotRepository>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var app = builder.Build();

// --------------------
// Middleware
// --------------------
app.UseHttpsRedirection();


app.UseSwagger(c =>
{
    c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
    {
        swaggerDoc.Servers = new List<OpenApiServer>
        {
            new OpenApiServer
            {
                Url = $"{httpReq.Scheme}://{httpReq.Host.Value}"
            }
        };
    });
});
app.UseSwaggerUI();


app.MapControllers();


app.MapGet("/", () => Results.Ok("CarTrace MCP is running"));

app.Run();