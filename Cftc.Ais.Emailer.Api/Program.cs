using Azure.Storage.Blobs;
using Cftc.Ais.Emailer.Application.DTOs;
using Cftc.Ais.Emailer.Application.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace Cftc.Ais.Emailer.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            ConfigureServices(builder.Services, builder.Configuration);

            var app = builder.Build();
            ConfigureApp(app);

            app.Run();
        }

        public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddApplicationInsightsTelemetry();
            services.AddSingleton<EmailStorageService>();

            services.AddHealthChecks()
                .AddCheck("AzureStorage", () =>
                {
                    try
                    {
                        var client = new BlobServiceClient(configuration["AzureWebJobsStorage"]);
                        var containerNames = client.GetBlobContainers().AsPages(pageSizeHint: 1).GetEnumerator();
                        containerNames.MoveNext();
                        return HealthCheckResult.Healthy();
                    }
                    catch (Exception ex)
                    {
                        return HealthCheckResult.Unhealthy($"Azure Storage check failed: {ex.Message}");
                    }
                })
                .AddCheck("SendGrid", () =>
                {
                    if (string.IsNullOrEmpty(configuration["SendGrid:ApiKey"]))
                    {
                        return HealthCheckResult.Unhealthy("SendGrid API key is not configured");
                    }
                    return HealthCheckResult.Healthy();
                });

            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    builder =>
                    {
                        builder.AllowAnyOrigin()
                               .AllowAnyMethod()
                               .AllowAnyHeader();
                    });
            });
        }

        public static void ConfigureApp(WebApplication app)
        {
            // if (app.Environment.IsDevelopment())
            // {
            app.UseSwagger();
            app.UseSwaggerUI();
            // }

            app.UseHttpsRedirection();

            app.UseCors("AllowAll");

            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = async (context, report) =>
                {
                    context.Response.ContentType = "application/json";
                    var response = new
                    {
                        Status = report.Status.ToString(),
                        Checks = report.Entries.Select(e => new
                        {
                            Component = e.Key,
                            Status = e.Value.Status.ToString(),
                            Description = e.Value.Description
                        }),
                        TotalDuration = report.TotalDuration
                    };
                    await context.Response.WriteAsync(JsonSerializer.Serialize(response));
                }
            });

            app.MapPost("/api/sendemail", async (EmailDto email, EmailStorageService storageService, ILogger<Program> logger) =>
            {
                try
                {
                    logger.LogInformation("Received email: {EmailJson}", JsonSerializer.Serialize(email));

                    // Validate attachments
                    if (email.Attachments != null)
                    {
                        long totalSize = email.Attachments.Sum(a => a.Size);
                        if (totalSize > 10 * 1024 * 1024) // 10 MB limit
                        {
                            return Results.BadRequest("Total attachment size exceeds 10 MB limit");
                        }
                        if (email.Attachments.Count > 5)
                        {
                            return Results.BadRequest("Maximum of 5 attachments allowed");
                        }
                    }

                    await storageService.SaveAndSendEmailAsync(email);
                    return Results.Ok("Email received, saved, and sent successfully");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing email");
                    return Results.BadRequest("Error processing email");
                }
            });

            app.MapGet("/api/email/{partitionKey}/{rowKey}", async (string partitionKey, string rowKey, EmailStorageService storageService) =>
            {
                try
                {
                    var email = await storageService.GetEmailAsync(partitionKey, rowKey);
                    return Results.Ok(email);
                }
                catch (Exception)
                {
                    return Results.NotFound();
                }
            });

            app.MapGet("/", () => "Welcome to Cftc.Ais.Emailer API");

           
        }
    }
}


