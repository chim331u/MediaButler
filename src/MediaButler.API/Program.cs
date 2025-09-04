using Microsoft.EntityFrameworkCore;
using MediaButler.Data;
using MediaButler.Data.Repositories;
using MediaButler.Data.UnitOfWork;
using MediaButler.Services;
using MediaButler.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddDbContext<MediaButlerDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("MediaButler.Data")));

// Add repository and unit of work
builder.Services.AddScoped<ITrackedFileRepository, TrackedFileRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Add application services
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IConfigurationService, ConfigurationService>();
builder.Services.AddScoped<IStatsService, StatsService>();

// Add API services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/", () => "MediaButler API - Ready!");

// Test endpoints for service validation
app.MapGet("/api/health", (IStatsService statsService) => 
{
    return Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
});

app.MapGet("/api/stats", async (IStatsService statsService) => 
{
    var result = await statsService.GetProcessingStatsAsync();
    return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error);
});

app.Run();
