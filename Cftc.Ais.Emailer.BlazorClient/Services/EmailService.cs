using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Cftc.Ais.Emailer.Application.DTOs;

namespace Cftc.Ais.Emailer.BlazorClient.Services
{
    public class EmailService
    {
        private readonly HttpClient _httpClient;

        public EmailService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> SendEmailAsync(EmailDto email)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/sendemail", email);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<EmailDto> GetEmailAsync(string partitionKey, string rowKey)
        {
            var response = await _httpClient.GetAsync($"/api/email/{partitionKey}/{rowKey}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<EmailDto>();
        }
    }
}