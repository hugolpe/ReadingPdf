using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ReadingPdf.Services
{
    public class WarmUpHostedService : IHostedService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WarmUpHostedService> _logger;

        public WarmUpHostedService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<WarmUpHostedService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Try ActivationApi first, then PredictionApi, then a sensible default.
            var url = _configuration["ActivationApi:BaseUrl"]
                      ?? _configuration["PredictionApi:BaseUrl"]
                      ?? "http://localhost:7164/";

            if (!url.EndsWith("/")) url += "/";

            const int maxRetries = 5;
            const int delaySeconds = 3;

            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            for (int i = 1; i <= maxRetries; i++)
            {
                try
                {
                    // Use LogInformation but also log a warning when the endpoint is not responding.
                    _logger.LogInformation("Warming external API: {Url} (attempt {Attempt}/{Max})", url, i, maxRetries);
                    var response = await client.GetAsync(url, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Activation/Prediction API warmed successfully ({StatusCode})", response.StatusCode);
                        return;
                    }

                    _logger.LogWarning("Activation/Prediction API returned {StatusCode} (attempt {Attempt}/{Max})", response.StatusCode, i, maxRetries);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Warm-up cancelled.");
                    return;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogInformation("API not available yet (attempt {Attempt}/{Max}): {Message}", i, maxRetries, ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to call Activation/Prediction API at {Url} (attempt {Attempt}/{Max})", url, i, maxRetries);
                }

                if (i < maxRetries)
                {
                    _logger.LogInformation("Waiting {Seconds}s before next attempt...", delaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
            }

            _logger.LogError("Could not connect to Activation/Prediction API after {Max} attempts", maxRetries);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}