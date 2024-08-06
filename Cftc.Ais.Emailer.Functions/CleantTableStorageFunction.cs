using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using Azure;
using System.Linq;
using Polly;
using Polly.Retry;
using Azure.Storage.Blobs;

namespace Cftc.Ais.Emailer.Functions
{
    public class CleanupTableStorageFunction
    {
        private readonly TableClient _tableClient;
        private readonly ILogger<CleanupTableStorageFunction> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly BlobContainerClient _blobContainerClient;

        public CleanupTableStorageFunction(ILogger<CleanupTableStorageFunction> logger)
        {
            _logger = logger;
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            _tableClient = new TableClient(connectionString, "Emails");

            var blobServiceClient = new BlobServiceClient(connectionString);
            _blobContainerClient = blobServiceClient.GetBlobContainerClient("email-attachments");


            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    5,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning($"Attempt {retryCount} failed with exception {exception.Message}. Retrying in {timeSpan.TotalSeconds} seconds.");
                    }
                );
        }

        [Function("CleanupStorageFunction")]
        public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"Cleanup function executed at: {DateTime.Now}");

            await _retryPolicy.ExecuteAsync(async () =>
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-48);

                // Cleanup Table Storage
                var query = _tableClient.QueryAsync<TableEntity>(filter: $"Timestamp le datetime'{cutoffTime:o}'");
                await foreach (var entity in query)
                {
                    await _tableClient.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
                    _logger.LogInformation($"Deleted email from table: PartitionKey={entity.PartitionKey}, RowKey={entity.RowKey}");
                }

                // Cleanup Blob Storage
                var blobItems = _blobContainerClient.GetBlobsAsync();
                await foreach (var blobItem in blobItems)
                {
                    if (blobItem.Properties.CreatedOn <= cutoffTime)
                    {
                        await _blobContainerClient.DeleteBlobAsync(blobItem.Name);
                        _logger.LogInformation($"Deleted blob: {blobItem.Name}");
                    }
                }
            });
        }
    }
}