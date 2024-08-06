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



//{
//    "fromName": "punkouter24",
//  "fromEmail": "punkouter24@gmail.com",
//  "toName": "punkouter23",
//  "toEmail": "punkouter23@gmail.com",
//  "subject": "Test Email",
//  "body": "This is a test email body.",
//  "isHtml": false,
//  "priority": 1
//}


//{
//    "fromName": "Test Sender",
//  "fromEmail": "sender@example.com",
//  "toName": "Test Recipient",
//  "toEmail": "your-actual-email@example.com",
//  "subject": "Test Email with Attachment",
//  "body": "This email includes a test attachment.",
//  "isHtml": false,
//  "attachments": [
//    {
//        "fileName": "test.txt",
//      "contentType": "text/plain",
//      "content": "SGVsbG8sIHRoaXMgaXMgYSB0ZXN0IGF0dGFjaG1lbnQu"
//    }
//  ]
//}



//https://microsoft.com/devicelogin

//No     Subscription name     Subscription ID                       Tenant
//-----  --------------------  ------------------------------------  -----------------
//[1] *  Azure subscription 1  479062d5-f1b2-47ff-b5f6-c5ae7db0954d  Default DirectoryNo     Subscription name     Subscription ID                       Tenant
//-----  --------------------  ------------------------------------  -----------------
//[1] *  Azure subscription 1  479062d5-f1b2-47ff-b5f6-c5ae7db0954d  Default Directory

//az login --tenant bbc1ef8a-6d87-4226-8735-685dd2ce9ca3
//az login --use-device-code
//az login --tenant "479062d5-f1b2-47ff-b5f6-c5ae7db0954d"
//az login --subscription "479062d5-f1b2-47ff-b5f6-c5ae7db0954d" 
//az account list --output table
//az account set --subscription 479062d5-f1b2-47ff-b5f6-c5ae7db0954d

//az group create --name PoEmailer --location eastus
//az storage account create --name poemailerstorage --location eastus --resource-group PoEmailer --sku Standard_LRS
//az appservice plan create --name poemailersp --resource-group PoEmailer --sku F1
//az webapp create --name poemailer --resource-group PoEmailer --plan poemailersp
//az webapp config appsettings set --name poemailer --resource-group PoEmailer --settings AzureWebJobsStorage="DefaultEndpointsProtocol=https;AccountName=poemailerstorage;AccountKey=qbUh82ACwpDwYY+M8RU73+J49UZLYKyAE9lWpAl9RXpKB/4maQ7XGe9zv38GgyF4CSEwLM1sifBW+ASt1zCBsA==;EndpointSuffix=core.windows.net" SendGrid:ApiKey="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"
//az webapp deployment source config-zip --name poemailer --resource-group PoEmailer --src path/to/your/publish.zip
//az monitor app-insights component create --app poemailer-insights --location eastus --kind web --resource-group PoEmailer --application-type
//az monitor app-insights component show --app poemailer-insights --resource-group PoEmailer --query instrumentationKey -o tsv
//f422172e-2ef6-4fe4-914d-e23a24a9befd
//az webapp config appsettings set --name poemailer --resource-group PoEmailer --settings APPINSIGHTS_INSTRUMENTATIONKEY=f422172e-2ef6-4fe4-914d-e23a24a9befd
//az webapp config set --name poemailer --resource-group PoEmailer --web-sockets-enabled true
//az webapp config appsettings set --name poemailer --resource-group PoEmailer --settings APPINSIGHTS_PROFILERFEATURE_VERSION=1.0.0 APPINSIGHTS_SNAPSHOTFEATURE_VERSION=1.0.0 ApplicationInsightsAgent_EXTENSION_VERSION=~2 DiagnosticServices_EXTENSION_VERSION=~3 InstrumentationEngine_EXTENSION_VERSION=~1 SnapshotDebugger_EXTENSION_VERSION=~1 XDT_MicrosoftApplicationInsights_BaseExtensions=~1 XDT_MicrosoftApplicationInsights_Mode=recommended




//cd C:\Users\punko\Downloads\Cftc.Ais.Emailer\Cftc.Ais.Emailer.Api
//dotnet publish -c Release -o ./publish
//Compress-Archive -Path .\publish\* -DestinationPath .\publish.zip -Force
//az webapp deploy --resource-group PoEmailer --name poemailer --src-path .\publish.zip