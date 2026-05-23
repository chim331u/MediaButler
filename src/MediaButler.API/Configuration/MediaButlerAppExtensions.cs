using MediaButler.API.Middleware;
using MediaButler.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Hangfire;

namespace Microsoft.AspNetCore.Builder;

public static class MediaButlerAppExtensions
{
    public static async Task UseMediaButlerDatabase(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MediaButlerDbContext>();
        try
        {
            if (context.Database.IsRelational())
            {
                await context.Database.MigrateAsync();
                Log.Information("Database migration completed successfully");
            }
            else
            {
                await context.Database.EnsureCreatedAsync();
                Log.Information("Database created successfully (non-relational)");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to migrate database");
            throw;
        }
    }

    public static IApplicationBuilder UseMediaButlerDocumentation(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "MediaButler API v1");
                c.RoutePrefix = "swagger";
                c.DisplayRequestDuration();
                c.EnableValidator();
            });

            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                DashboardTitle = "MediaButler Background Jobs",
                StatsPollingInterval = 10000 // 10 seconds
            });
        }
        return app;
    }

    public static IApplicationBuilder UseMediaButlerMiddleware(this IApplicationBuilder app)
    {
        // Add request/response logging first for complete request tracking
        app.UseRequestResponseLogging();

        // Add performance monitoring with ARM32 optimization
        app.UsePerformanceMonitoring();

        // Add global exception handling with structured logging
        app.UseGlobalExceptionHandler();

        app.UseHttpsRedirection();
        app.UseRouting();

        // Use configurable CORS policy for all environments
        app.UseCors("ConfigurablePolicy");

        // Add API Key Authentication
        app.UseApiKeyAuthentication();

        return app;
    }
}
