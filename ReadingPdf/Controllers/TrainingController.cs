using Microsoft.AspNetCore.Mvc;
using Microsoft.ML;
using Newtonsoft.Json;
using ReadingPdf.Services;
using System.Text;
using System.Linq;

namespace ReadingPdf.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrainingController : ControllerBase
    {
        private readonly AccountPredictionService _predictor;
        private readonly ILogger<TrainingController> _logger;

        public TrainingController(AccountPredictionService predictor, ILogger<TrainingController> logger)
        {
            _predictor = predictor;
            _logger = logger;
        }

        // -----------------------------------------------------------
        //     ENDPOINT PRINCIPAL DE ENTRENAMIENTO
        // -----------------------------------------------------------
        [HttpPost("TrainFromPayload")]
        public IActionResult TrainFromPayload([FromBody] TrainingRequest req)
        {
            try
            {
                if (req == null || req.Rows == null || req.Rows.Count < 2)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Debe enviar al menos 2 filas para entrenar."
                    });
                }

                string baseDir = Path.Combine(Directory.GetCurrentDirectory(), "models");
                if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);

                string modelPath = Path.Combine(baseDir, "account-predictor.zip");
                string labelsPath = Path.Combine(baseDir, "labels.txt");
                string tokensPath = Path.Combine(baseDir, "tokens_by_company.json");

                // ----------------------------------------------
                // 1. Preparación de datos para entrenamiento
                // ----------------------------------------------
                var rows = req.Rows
                    .Select(r => (
                        Label: (r.Label ?? "").Trim(),
                        Memo: (r.Memo ?? "").Trim(),
                        Company: (r.Company ?? "").Trim()
                    ))
                    .ToList();

                int uniqueLabels = rows.Select(r => r.Label).Distinct().Count();
                if (uniqueLabels < 2)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "El modelo requiere al menos 2 clases distintas para entrenar.",
                        clasesEncontradas = uniqueLabels
                    });
                }

                // ----------------------------------------------
                // 2. ENTRENAR Y GUARDAR MODELO
                // ----------------------------------------------
                _predictor.TrainAndSave(rows, modelPath, tokensPath, labelsPath);

                _logger.LogInformation($"Modelo guardado en: {modelPath}");

                // ----------------------------------------------
                // 3. Respuesta de éxito
                // ----------------------------------------------
                return Ok(new
                {
                    success = true,
                    message = "Modelo entrenado y guardado correctamente.",
                    clases = uniqueLabels,
                    registros = rows.Count,
                    modelo = modelPath,
                    labels = labelsPath,
                    tokens = tokensPath
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en entrenamiento.");
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message,
                    detail = ex.ToString()
                });
            }
        }
    }

    // ============================================================
    //           DTO PARA ENTRENAMIENTO
    // ============================================================
    public class TrainingRequest
    {
        public List<RowTrainDto> Rows { get; set; } = new();
    }

    public class RowTrainDto
    {
        public string Label { get; set; } = "";
        public string Memo { get; set; } = "";
        public string Company { get; set; } = "";
    }
}
