using Cftc.Ais.Emailer.Api;
using Cftc.Ais.Emailer.Application.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using Xunit;

namespace Cftc.Ais.Emailer.Tests
{
    public class EmailApiTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public EmailApiTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task SendEmail_ValidEmail_ReturnsOk()
        {
            // Arrange
            var client = _factory.CreateClient();
            var email = new EmailDto
            {
                FromName = "punkouter24",
                FromEmail = "punkouter24@gmail.com",
                ToName = "punkouter23",
                ToEmail = "punkouter23@gmail.com",
                Subject = "Test Email",
                Body = "This is a test email.",
                IsHtml = false,
                Priority = EmailPriority.Normal
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/sendemail", email);

            // Assert
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            responseString.Should().Contain("Email received and saved successfully");
        }

        [Fact]
        public async Task SendEmail_InvalidEmail_ReturnsBadRequest()
        {
            // Arrange
            var client = _factory.CreateClient();
            var invalidEmail = new EmailDto
            {
                // Missing required fields
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/sendemail", invalidEmail);

            // Assert
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        }
    }
}