using Microsoft.EntityFrameworkCore;
using MediaButler.Data;
using MediaButler.Data.Repositories;
using MediaButler.Data.UnitOfWork;
using MediaButler.Services;
using MediaButler.Services.Interfaces;
using MediaButler.Core.Services;
using MediaButler.API.Middleware;
using MediaButler.API.Filters;
using MediaButler.ML.Extensions;
using MediaButler.Services.Background;
using MediaButler.Services.FileOperations;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog from appsettings.json following "Simple Made Easy" principles
builder.Host.UseSerilog((context, configuration) => 
    configuration.ReadFrom.Configuration(context.Configuration));

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
builder.Services.AddScoped<IRollbackService, RollbackService>();
builder.Services.AddScoped<IErrorClassificationService, ErrorClassificationService>();
builder.Services.AddScoped<IFileOrganizationService, FileOrganizationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Add file operation services
builder.Services.AddScoped<IFileOperationService, FileOperationService>();
builder.Services.AddScoped<IPathGenerationService, PathGenerationService>();

// Add ML services with configuration
builder.Services.AddMediaButlerML(builder.Configuration);

// Add background processing services with configuration
builder.Services.AddBackgroundServices(builder.Configuration);

// Add API services with validation
builder.Services.AddControllers(options =>
{
    // Add global model validation filter
    options.Filters.Add<ModelValidationFilter>();
    
    // Configure JSON options for consistent formatting
    options.RespectBrowserAcceptHeader = true;
    options.ReturnHttpNotAcceptable = true;
});

builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = false;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "MediaButler API", 
        Version = "v1",
        Description = "Intelligent media file organization system with ML-powered classification"
    });
    
    // Include XML comments for API documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Add JSON serialization configuration
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = false;
});

var app = builder.Build();

// Configure the HTTP request pipeline with middleware order
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MediaButler API v1");
        c.RoutePrefix = "swagger";
        c.DisplayRequestDuration();
        c.EnableValidator();
    });
}

// Add request/response logging first for complete request tracking
app.UseRequestResponseLogging();

// Add performance monitoring with ARM32 optimization
app.UsePerformanceMonitoring();

// Add global exception handling with structured logging
app.UseGlobalExceptionHandler();

app.UseHttpsRedirection();
app.UseRouting();

// Add CORS configuration for future UI
app.UseCors(policy =>
{
    policy.AllowAnyOrigin()
          .AllowAnyMethod()
          .AllowAnyHeader();
});

// Map controllers
app.MapControllers();

// Enhanced root endpoint with API information
app.MapGet("/", () => new
{
    Service = "MediaButler API",
    Version = "1.0.0",
    Status = "Ready",
    Documentation = "/swagger",
    HealthCheck = "/api/health",
    Timestamp = DateTime.UtcNow
});

app.Run();

// Make Program class accessible for testing
public partial class Program { }
