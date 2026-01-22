using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ReadingPdf.Services
{
    public class PredictionApiClient : IPredictionApiClient
    {
        private readonly HttpClient _http;

        public PredictionApiClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<string?> PredictAccountAsync(string memo, string company, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(memo) && string.IsNullOrWhiteSpace(company))
                return null;

            // Payload shape: adjust property names if your PredictionController expects different names
            var payload = new { Memo = memo ?? "", Company = company ?? "" };

            try
            {
                var resp = await _http.PostAsJsonAsync("api/prediction", payload, ct);
                if (!resp.IsSuccessStatusCode) return null;

                var result = await resp.Content.ReadAsStringAsync(ct);
                // The controller returns the account string as plain content (OK(predictedAccount))
                // Trim quotes if JSON string was returned
                return result?.Trim('"');
            }
            catch
            {
                return null;
            }
        }
    }
}