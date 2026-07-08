using System.Text.Json;
using InventoryHub.Api.Data;
using InventoryHub.Api.Models;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<InventoryStore>();
builder.Services.AddMemoryCache();
builder.Services.AddResponseCaching();

// Response compression: cuts payload size for the JSON list endpoint
// substantially, which matters once the front end is fetching pages
// repeatedly while the user types in the search box.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // camelCase is the front end's expectation; being explicit here
        // avoids the "PascalCase vs camelCase" mismatch that was one of
        // the integration bugs found during debugging (see INTEGRATION_SUMMARY.md).
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// CORS: the front end is served from a different origin (static file
// server / GitHub Pages) than the API, so without an explicit policy the
// browser blocks every fetch() with a CORS error — this was the first
// integration issue hit and fixed in Activity 2.
const string frontEndPolicy = "FrontEndPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(frontEndPolicy, policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5500",
                "http://127.0.0.1:5500",
                "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Global exception handler: guarantees every error — including unhandled
// ones — comes back as the same structured JSON ApiError shape instead of
// an HTML developer-exception page, which was silently breaking the front
// end's error-handling code (it expected JSON and choked on HTML).
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var error = new ApiError("INTERNAL_ERROR", "An unexpected error occurred.");
        await context.Response.WriteAsJsonAsync(error);
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseResponseCompression();
app.UseResponseCaching();
app.UseCors(frontEndPolicy);
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
