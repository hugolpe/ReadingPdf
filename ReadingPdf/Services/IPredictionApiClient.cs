using System.Threading;
using System.Threading.Tasks;

namespace ReadingPdf.Services
{
    public interface IPredictionApiClient
    {
        Task<string?> PredictAccountAsync(string memo, string company, CancellationToken ct = default);
    }
}