using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;

namespace ReadingPdf.Services
{
    public interface IClearbitService
    {
        Task<string?> FindDomainByNameAsync(string name);
        Task<ClearbitResult?> EnrichByDomainAsync(string domain);
    }

    public class ClearbitService : IClearbitService
    {
        private readonly HttpClient _http;
        private readonly string? _apiKey;

        public ClearbitService(HttpClient http, IConfiguration config)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _apiKey = config["Clearbit:ApiKey"];
            if (!string.IsNullOrWhiteSpace(_apiKey))
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _http.BaseAddress = new Uri("https://company.clearbit.com");
        }

        public async Task<string?> FindDomainByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var url = $"/v1/domains/find?name={Uri.EscapeDataString(name)}";
            try
            {
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync();
                var j = JObject.Parse(json);
                return j["domain"]?.ToString();
            }
            catch
            {
                return null;
            }
        }

        public async Task<ClearbitResult?> EnrichByDomainAsync(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain)) return null;
            var url = $"/v2/companies/find?domain={Uri.EscapeDataString(domain)}";
            try
            {
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync();
                var j = JObject.Parse(json);

                var result = new ClearbitResult
                {
                    Domain = j["domain"]?.ToString(),
                    Name = j["name"]?.ToString(),
                    Industry = j["category"]?["industry"]?.ToString(),
                    SubIndustry = j["category"]?["subIndustry"]?.ToString(),
                    Sector = j["category"]?["sector"]?.ToString(),
                    Naics = j["naics"]?.ToString() ?? j["category"]?["naics"]?.ToString(),
                    Raw = j
                };

                // tags often come as array
                var tags = new List<string>();
                if (j["tags"] is JArray tj)
                {
                    foreach (var t in tj) tags.Add(t.ToString());
                }
                // also try keywords / tag-like fields
                result.Tags = tags.ToArray();

                return result;
            }
            catch
            {
                return null;
            }
        }
    }

    public class ClearbitResult
    {
        public string? Domain { get; set; }
        public string? Name { get; set; }
        public string? Industry { get; set; }
        public string? SubIndustry { get; set; }
        public string? Sector { get; set; }
        public string? Naics { get; set; }
        public string[]? Tags { get; set; }
        public JObject? Raw { get; set; }
    }
}