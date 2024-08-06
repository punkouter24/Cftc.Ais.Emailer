using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Cftc.Ais.Emailer.Application.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Cftc.Ais.Emailer.Application.Infrastructure
{
    public class EmailStorageService
    {
        private readonly TableClient _tableClient;
        private readonly BlobContainerClient _blobContainerClient;
        private readonly SendGridClient _sendGridClient;
        private readonly ILogger<EmailStorageService> _logger;

        public EmailStorageService(IConfiguration configuration, ILogger<EmailStorageService> logger)
        {
            string connectionString = configuration["AzureWebJobsStorage"];
            _tableClient = new TableClient(connectionString, "Emails");
            _tableClient.CreateIfNotExists();

            _blobContainerClient = new BlobContainerClient(connectionString, "email-attachments");
            _blobContainerClient.CreateIfNotExists();

            _sendGridClient = new SendGridClient(configuration["SendGrid:ApiKey"]);
            _logger = logger;
        }

        public async Task SaveAndSendEmailAsync(EmailDto email)
        {
            await SaveEmailAsync(email);

            var msg = new SendGridMessage()
            {
                From = new EmailAddress(email.FromEmail, email.FromName),
                Subject = email.Subject,
                PlainTextContent = email.IsHtml ? "" : email.Body,
                HtmlContent = email.IsHtml ? email.Body : ""
            };

            msg.AddTo(new EmailAddress(email.ToEmail, email.ToName));

            if (email.Attachments != null)
            {
                foreach (var attachment in email.Attachments)
                {
                    msg.AddAttachment(attachment.FileName,
                        Convert.ToBase64String(attachment.Content),
                        attachment.ContentType);
                }
            }

            var response = await _sendGridClient.SendEmailAsync(msg);

            if (response.StatusCode != System.Net.HttpStatusCode.Accepted &&
                response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                _logger.LogError($"Failed to send email: {response.StatusCode}");
                throw new Exception($"Failed to send email: {response.StatusCode}");
            }

            _logger.LogInformation($"Email sent successfully: {email.Subject}");
        }

        public async Task SaveEmailAsync(EmailDto email)
        {
            var entity = new TableEntity(email.FromEmail, Guid.NewGuid().ToString())
            {
                { "ToEmail", email.ToEmail },
                { "Subject", email.Subject },
                { "SentDate", DateTime.UtcNow }
            };

            await _tableClient.AddEntityAsync(entity);

            if (email.Attachments != null)
            {
                foreach (var attachment in email.Attachments)
                {
                    var blobClient = _blobContainerClient.GetBlobClient(attachment.AttachmentId.ToString());
                    await blobClient.UploadAsync(new MemoryStream(attachment.Content), overwrite: true);
                }
            }
        }

        public async Task<EmailDto> GetEmailAsync(string partitionKey, string rowKey)
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(partitionKey, rowKey);
            var email = new EmailDto
            {
                FromEmail = response.Value.PartitionKey,
                ToEmail = response.Value.GetString("ToEmail"),
                Subject = response.Value.GetString("Subject"),
                // ... map other properties
            };

            // Fetch attachments if any
            var query = _blobContainerClient.GetBlobs().Where(b => b.Name.StartsWith(rowKey));
            foreach (var blobItem in query)
            {
                var blobClient = _blobContainerClient.GetBlobClient(blobItem.Name);
                var content = await blobClient.DownloadContentAsync();
                email.Attachments.Add(new AttachmentDto
                {
                    FileName = blobItem.Name,
                    ContentType = blobItem.Properties.ContentType,
                    Size = blobItem.Properties.ContentLength ?? 0,
                    AttachmentId = Guid.Parse(blobItem.Name.Split('_')[0]),
                    Content = content.Value.Content.ToArray()
                });
            }

            return email;
        }
    }
}