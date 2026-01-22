using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.ML.Data;
using Newtonsoft.Json;
using OfficeOpenXml;
using ReadingPdf.Data;
using ReadingPdf.Models;
using ReadingPdf.Parsers;
using ReadingPdf.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;
using UglyToad.PdfPig;

namespace ReadingPdf.Controllers
{
    public class PdfNewController : Controller
    {
        // In-memory "tabla temporal" storage: BatchId -> TempBatch (items + optional BankId)
        // This avoids touching DbContext/migrations and lets the user edit before exporting.
        //private class TempBatch
        //{
        //    public List<Movimiento> Items { get; set; } = new();
        //    public int? BankId { get; set; }
        //}
        // Add these fields to your PdfNewController class (near other private fields)
        private static readonly Regex regexCreditoContexto = new Regex(@"(CREDITO|CRÉDITO|DEPÓSITO|DEPOSITO|DEPOSIT|CREDIT)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex regexDebitoContexto = new Regex(@"(DÉBITO|DEBITO|DEBIT|RETIRO|WITHDRAWAL|CHARGE)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private class TempBatch
        {
            public List<Movimiento> Items { get; set; } = new();
            public int? BankId { get; set; }

            // ✅ NUEVO: cuenta QuickBooks seleccionada en Index
            public string? SelectedCuentaQB { get; set; }

            // ✅ NUEVO: Interest Charged extraído del/los PDFs (nullable)
            public decimal? InterestCharged { get; set; }
        }


        private static readonly ConcurrentDictionary<Guid, TempBatch> _tempTables = new();

        private readonly string _apiPredictUrl = "https://localhost:44377/api/Movimientos/predict-mov";
        private readonly string _clasificacionApiBase = "http://localhost:7164"; // <--- Ajusta la URL base según dónde corra tu ClasificacionApi
        private readonly ApplicationDbContext _context;
        private readonly ApplicationDbContext _db;
        private readonly IClearbitService _clearbit;
        private readonly string _patternsPath;
        private static readonly object _patternsLock = new();
        private readonly ILogger<PdfNewController> _logger;
        private readonly AccountPredictionService _accountPredictor;
        private readonly IPredictionApiClient _predictionClient;
   

        public PdfNewController(ApplicationDbContext context, IClearbitService clearbit, ILogger<PdfNewController> logger, AccountPredictionService accountPredictor, IPredictionApiClient predictionClient)
        {
            _context = context;
            _db = context; // ensure single DbContext field is initialized
            _clearbit = clearbit;
            _logger = logger;
            _patternsPath = Path.Combine(Directory.GetCurrentDirectory(), "extraction_patterns.json");
            _accountPredictor = accountPredictor;
            _predictionClient = predictionClient;
        }

        // Note: removed the second constructor that accepted (ApplicationDbContext db, IClearbitService clearbit)
        // to avoid DI ambiguity. All required services are injected in the single constructor above.

        // =====================================================
        // 🔄 Reabrir proceso guardado
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> ReabrirProceso(Guid id)
        {
            var proceso = await _db.Procesos
                .Include(p => p.Movimientos)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proceso == null)
                return NotFound("Proceso no encontrado");

            // ==========================
            // 1️⃣ Reconstruir el MODEL
            // ==========================
            var movimientos = proceso.Movimientos
                .OrderBy(m => m.Fecha)
                .Select(m => new Movimiento
                {
                    Fecha = m.Fecha,
                    Empresa = m.Empresa,
                    EmpresaExtraida = m.EmpresaOriginal,
                    Descripcion = m.Descripcion,
                    Monto = m.Monto,
                    CuentaPredicha = m.CuentaPredicha,
                    CuentaContableAplicada = m.CuentaAplicada,
                    TipoDocumento = m.TipoDocumento,
                    ScorePrediccion = (int)m.ScorePrediccion
                })
                .ToList();

            // ==========================
            // 2️⃣ ViewBag EXACTO
            // ==========================
            ViewBag.BatchId = proceso.Id; // 🔑 MISMO BatchId
            ViewBag.BankName = proceso.Banco;
            ViewBag.ItemsCount = movimientos.Count;
            ViewBag.LastInterestCharged = proceso.SaldoInicial;
            //ViewBag.SelectedCuentaQB = proceso.CuentaQBSeleccionada;
            ViewBag.SelectedCuentaQB = LastSelectedAccountQB ?? "";

            // Empresas reconocidas (para verde)
            ViewBag.RecognizedCompanies = movimientos
                .Where(m => !string.IsNullOrWhiteSpace(m.Empresa))
                .Select(m => m.Empresa)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // ==========================
            // 3️⃣ Renderizar MISMA vista
            // ==========================
            return View("Resultados", movimientos);
        }


        // 🔹 Normaliza texto (quita tildes, pasa a minúsculas, recorta)
        private string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }

            return sb.ToString()
                     .Normalize(NormalizationForm.FormC)
                     .ToLower()
                     .Trim();
        }

        // 🔹 Palabras que indican que NO es un nombre de empresa/persona
        private static readonly string[] NoiseWords = new[] 
        {
    "payment", "pago", "transfer", "transferencia", "zelle",
    "deposit", "withdrawal", "visa", "mastercard", "spei",
    "conf#", "ref#", "autoriz", "authorization", "ach",
    "purchase", "pos ", "mobile", "online"
};

        // 🔹 Valida que el candidato parezca nombre de empresa/persona
        private bool IsValidCompanyName(string name)
        {
            var norm = Normalize(name);

            if (string.IsNullOrWhiteSpace(norm))
                return false;

            if (norm.Length < 3 || norm.Length > 30)
                return false;

            // Debe tener al menos una letra
            if (!norm.Any(char.IsLetter))
                return false;

            // No debe contener palabras de ruido
            if (NoiseWords.Any(n => norm.Contains(n)))
                return false;

            return true;
        }



        // Simple model para persistir patrones
        private class PatternEntry
        {
            public int Id { get; set; }
            public int? BankId { get; set; } // null => global
            public string Pattern { get; set; } = "";
            public string Description { get; set; } = "";
            public DateTime CreatedAt { get; set; }
        }

        private List<PatternEntry> LoadPatterns()
        {
            lock (_patternsLock)
            {
                try
                {
                    if (!System.IO.File.Exists(_patternsPath))
                        return new List<PatternEntry>();

                    var json = System.IO.File.ReadAllText(_patternsPath, Encoding.UTF8);
                    var list = JsonConvert.DeserializeObject<List<PatternEntry>>(json);
                    return list ?? new List<PatternEntry>();
                }
                catch
                {
                    // Si hay error, retornar lista vacía para no bloquear procesamiento
                    return new List<PatternEntry>();
                }
            }
        }

        private void SavePatterns(List<PatternEntry> patterns)
        {
            lock (_patternsLock)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(patterns.OrderBy(p => p.Id).ToList(), Formatting.Indented);
                    System.IO.File.WriteAllText(_patternsPath, json, Encoding.UTF8);
                }
                catch
                {
                    // No throw: no queremos romper el flujo de procesamiento por fallo al guardar aprendizaje.
                }
            }
        }

        private string? ExtractNameFromMemo(string? memo, int? bankId)
        {
            if (string.IsNullOrWhiteSpace(memo))
                return null;

            memo = memo.Trim();

            // 1) Probar patrones persistidos
            var patterns = LoadPatterns();

            // Priorizar patrones del banco
            var ordered = patterns
                .Where(p => p.BankId == bankId || p.BankId == null)
                .OrderByDescending(p => p.BankId.HasValue) // first specific bank
                .ThenBy(p => p.CreatedAt)
                .ToList();

            foreach (var p in ordered)
            {
                try
                {
                    var rx = new Regex(p.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    var m = rx.Match(memo);
                    if (m.Success)
                    {
                        if (m.Groups.Count > 1 && m.Groups[1].Success)
                            return m.Groups[1].Value.Trim();
                        return m.Value.Trim();
                    }
                }
                catch
                {
                    // patrones corruptos -> ignorar
                    continue;
                }
            }

            // 2) Heurísticas: keywords seguidas de nombre
            var keywords = new[] 
            {
                "PAY TO","PAGO A","PAGADO A","MERCHANT","REMIT TO","VENDEDOR","PAYEE","TO:","FOR:",
                "POR:","DE:","COMPRADO EN","COMPRA EN"
            };

            foreach (var kw in keywords)
            {
                // construir regex como: (?:KW)\s+([A-Za-z0-9\.\-&ÑÁÉÍÓÚñáéíóú ]{3,80})
                var safeKw = Regex.Escape(kw);
                var pattern = $@"(?:{safeKw})\s+([A-Za-z0-9\.\-&ÑÁÉÍÓÚñáéíóú ]{{3,80}})";
                var rx = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                var m = rx.Match(memo);
                if (m.Success && m.Groups.Count > 1)
                {
                    var candidate = m.Groups[1].Value.Trim();
                    if (candidate.Length >= 3)
                    {
                        // aprender patrón para el banco
                        AutoAddPattern(bankId, $@"(?:{safeKw})\s+({Regex.Escape(candidate)})", $"Auto-generated from keyword '{kw}'");
                        return candidate;
                    }
                }
            }

            // 3) Heurística: secuencia de palabras con mayúsculas (merchant names a menudo en Title Case or ALL CAPS)
            var capRx = new Regex(@"([A-ZÁÉÍÓÚÑ][A-Za-zÁÉÍÓÚñáéíóú0-9\.\-&]{1,30}(?:\s+[A-ZÁÉÍÓÚÑ][A-Za-zÁÉÍÓÚñáéíóú0-9\.\-&]{1,30}){0,4})", RegexOptions.Compiled);
            var capMatch = capRx.Match(memo);
            if (capMatch.Success)
            {
                var candidate = capMatch.Groups[1].Value.Trim();
                if (candidate.Length >= 3 && candidate.Length <= 80)
                {
                    // Guardar patrón sencillo que capture esa secuencia
                    AutoAddPattern(bankId, $"({Regex.Escape(candidate)})", "Auto-generated from capitalized sequence");
                    return candidate;
                }
            }

            // 4) Heurística: cadenas en mayúsculas continuas (ALL CAPS)
            var allCapsRx = new Regex(@"([A-Z0-9&\.\- ]{4,80})", RegexOptions.Compiled);
            var m2 = allCapsRx.Match(memo);
            if (m2.Success)
            {
                var candidate = m2.Groups[1].Value.Trim();
                // filtrar si contiene muchas palabras genéricas o números
                if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length >= 3)
                {
                    AutoAddPattern(bankId, $"({Regex.Escape(candidate)})", "Auto-generated from ALL CAPS segment");
                    return candidate;
                }
            }

            // No se encontró
            return null;
        }

        private void AutoAddPattern(int? bankId, string pattern, string description)
        {
            try
            {
                lock (_patternsLock)
                {
                    var list = LoadPatterns();
                    // Evitar duplicados exactos
                    if (list.Any(p => p.BankId == bankId && string.Equals(p.Pattern, pattern, StringComparison.OrdinalIgnoreCase)))
                        return;

                    var id = (list.Count == 0) ? 1 : list.Max(p => p.Id) + 1;
                    var entry = new PatternEntry
                    {
                        Id = id,
                        BankId = bankId,
                        Pattern = pattern,
                        Description = description,
                        CreatedAt = DateTime.UtcNow
                    };
                    list.Add(entry);
                    SavePatterns(list);
                }
            }
            catch
            {
                // no detener flujo
            }
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Leer bancos desde la tabla
            var bancos = _context.Bank.ToList();

            // Pasar al ViewBag o a un ViewModel
            ViewBag.Bancos = bancos;

            return View();
        }

        //[HttpPost]
        //public async Task<IActionResult> Procesar(List<IFormFile> archivosPdf, int BancoId)
            [HttpPost]
        public async Task<IActionResult> Procesar(List<IFormFile> archivosPdf, int BancoId, string? LastSelectedAccountQB)
        {
            if (archivosPdf == null || archivosPdf.Count == 0)
                return View("Index");



            var bancoSeleccionado = await _context.Bank.FindAsync(BancoId);

            var movimientosTotales = new List<Movimiento>();

            // Accumulators for Interest Charged across processed PDFs
            decimal totalInterest = 0m;
            bool anyInterestFound = false;

            foreach (var archivoPdf in archivosPdf)
            {
                // Guardar temporalmente
                var tempPath = Path.GetTempFileName();
                using (var stream = new FileStream(tempPath, FileMode.Create))
                    await archivoPdf.CopyToAsync(stream);

                // Diagnostic placeholder (raw text extraction helper not available)
                string? rawText = null;

                // Procesar PDF actual (protegido)
                try
                {
                    var movimientos = AmexParser.ParsePdf(tempPath, bancoSeleccionado) ?? new List<Movimiento>();

                    // capture InterestCharged exposed by AmexParser (if any)
                    // IMPORTANT: only take the first PDF's InterestCharged and ignore the rest
                    if (!anyInterestFound)
                    {
                        if (AmexParser.LastInterestCharged.HasValue)
                        {
                            totalInterest = AmexParser.LastInterestCharged.Value;
                            anyInterestFound = true;
                        }
                        else if (AmexParser.PaymentsCredits.HasValue) // <-- ADDED: consider PaymentsCredits
                        {
                            totalInterest = AmexParser.PaymentsCredits.Value;
                            anyInterestFound = true;
                        }
                        else if (AmexParser.BalanceAnterior.HasValue)
                        {
                            totalInterest = AmexParser.BalanceAnterior.Value;
                            anyInterestFound = true;
                        }
                        else
                        {
                            totalInterest = AmexParser.SaldoAnteriorFees.Value;
                            anyInterestFound = true;
                        }
                    }


                    // Agregar a la lista general
                    movimientosTotales.AddRange(movimientos);

                    // Borrar temporal solo si parse tuvo éxito (evita perder el PDF en caso de fallo)
                    try
                    {
                        System.IO.File.Delete(tempPath);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed deleting temp file {TempPath}", tempPath);
                    }

                    if (movimientos == null || movimientos.Count == 0)
                    {
                        _logger?.LogInformation("Parsed 0 movimientos for file {TempPath}. Extracted text length: {Len}", tempPath, rawText?.Length ?? 0);
                    }
                    else
                    {
                        _logger?.LogInformation("Parsed {Count} movimientos for file {TempPath}", movimientos.Count, tempPath);
                    }
                }
                catch (Exception ex)
                {
                    // Log and persist rawText next to the temp PDF for inspection
                    _logger?.LogError(ex, "Error parsing PDF {TempPath}", tempPath);
                    try
                    {
                        var dbgPath = Path.ChangeExtension(tempPath, ".txt");
                        var content = $"ParsePdf failed: {ex.GetType().FullName}: {ex.Message}\n\n--- ExtractedText ---\n\n{rawText ?? "(no text)"}";
                        System.IO.File.WriteAllText(dbgPath, content, Encoding.UTF8);
                        _logger?.LogInformation("Wrote debug text to {DbgPath}", dbgPath);
                    }
                    catch (Exception wex)
                    {
                        _logger?.LogWarning(wex, "Failed writing debug text for {TempPath}", tempPath);
                    }

                    // Do not delete the PDF so you can inspect it manually if needed.
                    continue;
                }
            }

            // Cargar lista de empresas reconocidas una sola vez (para no llamar a BD por cada movimiento)
            var empresasReconocidas = _context.EmpresasReconocidas
                .Where(e => !string.IsNullOrWhiteSpace(e.TextoOriginal))
                .Select(e => e.TextoOriginal!)
                .ToList();

            // Pass recognized company names to the view so the view can validate origin
            ViewBag.RecognizedCompanies = empresasReconocidas;

            // Cache local para no pedir Clearbit repetidamente por la misma empresa
            var clearbitCache = new ConcurrentDictionary<string, ReadingPdf.Services.ClearbitResult?>();

            // model file paths for local predictor
            var modelPath = Path.Combine(Directory.GetCurrentDirectory(), "models", "account-predictor.zip");
            var tokensPath = Path.Combine(Directory.GetCurrentDirectory(), "models", "tokens_by_company.json");
            var labelsPath = Path.Combine(Directory.GetCurrentDirectory(), "models", "labels.json");

            foreach (var mov in movimientosTotales)
            {
                try
                {
                    // 0) Intento de detección usando la tabla de EmpresasReconocidas (case-insensitive)
                    string? detected = null;
                    if (!string.IsNullOrWhiteSpace(mov.Descripcion))
                    {
                        detected = empresasReconocidas
                            .FirstOrDefault(e => mov.Descripcion.IndexOf(e, StringComparison.OrdinalIgnoreCase) >= 0);
                    }



                    if (!string.IsNullOrWhiteSpace(detected))
                    {
                        // If we found a known company, store it in UPPERCASE
                        mov.Empresa = detected.ToUpperInvariant();
                    }
                    else
                    {
                        // 1) EXTRAER NOMBRE desde el memo si Empresa está vacía
                        if (string.IsNullOrWhiteSpace(mov.Empresa))
                        {
                            var extracted = ExtractNameFromMemo(mov.Descripcion, BancoId);
                            if (!string.IsNullOrWhiteSpace(extracted))
                                mov.Empresa = extracted.ToUpperInvariant();
                        }
                    }

                    // 2) Enriching Clearbit (si hay empresa identificable)
                    if (!string.IsNullOrWhiteSpace(mov.Empresa))
                    {
                        var key = mov.Empresa.Trim().ToUpperInvariant();
                        if (!clearbitCache.TryGetValue(key, out var cbResult))
                        {
                            // Primero: name -> domain
                            var domain = await _clearbit.FindDomainByNameAsync(mov.Empresa);
                            if (!string.IsNullOrWhiteSpace(domain))
                            {
                                cbResult = await _clearbit.EnrichByDomainAsync(domain);
                            }
                            else
                            {
                                // intento directo por nombre como dominio
                                cbResult = await _clearbit.EnrichByDomainAsync(mov.Empresa);
                            }
                            clearbitCache[key] = cbResult;
                        }

                        if (cbResult != null)
                        {
                            mov.EnrichedCompanyDomain = cbResult.Domain;
                            mov.EnrichedIndustry = cbResult.Industry;
                            mov.EnrichedSubIndustry = cbResult.SubIndustry;
                            mov.EnrichedSector = cbResult.Sector;
                            mov.EnrichedNaics = cbResult.Naics;
                            mov.EnrichedTags = cbResult.Tags != null ? string.Join(",", cbResult.Tags) : null;

                            // Aplicar reglas para mapa contable
                            mov.CuentaContableAplicada = MapClearbitToAccount(cbResult, mov.Descripcion);
                            // Si aún no hay cuenta predicha por tu predictor, puedes usar la aplicada
                            if (string.IsNullOrWhiteSpace(mov.CuentaPredicha))
                                mov.CuentaPredicha = mov.CuentaContableAplicada;
                        }
                    }

                    // 3) Predicción remota usando PredictionController API, con fallback a predictor local
                    try
                    {
                        bool applied = false;

                        // 3a) Intentar predicción remota primero
                        try
                        {
                            var remote = await _predictionClient.PredictAccountAsync(mov.Descripcion ?? "", mov.Empresa ?? "", CancellationToken.None);
                            if (!string.IsNullOrWhiteSpace(remote))
                            {
                                mov.CuentaPredicha = remote;
                                // remote API doesn't return score; use a sentinel high value to indicate confidence
                                mov.ScorePrediccion = 100;
                                applied = true;
                            }
                        }
                        catch (Exception exRemote)
                        {
                            _logger?.LogWarning(exRemote, "Remote prediction API failed for movement {desc}", mov.Descripcion);
                        }

                        // 3b) Si la remota no arrojó resultado, usar predictor local como fallback
                        if (!applied)
                        {
                            var planDeCuentas = GetCompanyPlanAccounts(mov.Empresa ?? "");
                            var (account, score) = _accountPredictor.PredictForCompany(mov.Descripcion ?? "", mov.Empresa ?? "", planDeCuentas, modelPath, tokensPath, labelsPath);
                            if (!string.IsNullOrWhiteSpace(account))
                            {
                                mov.CuentaPredicha = account;
                                // Convert float score in [0..1] to 0..100; keep 0 if unknown
                                mov.ScorePrediccion = (int)Math.Round(score * 100);
                            }
                            else
                            {
                                if (string.IsNullOrWhiteSpace(mov.CuentaPredicha))
                                    mov.CuentaPredicha = "SIN PREDICCION";
                                mov.ScorePrediccion = 0;
                            }
                        }
                    }
                    catch (Exception exPred)
                    {
                        _logger?.LogWarning(exPred, "Prediction flow failed for movement {desc}", mov.Descripcion);
                        if (string.IsNullOrWhiteSpace(mov.CuentaPredicha))
                            mov.CuentaPredicha = "SIN PREDICCION";
                        mov.ScorePrediccion = 0;
                    }

                    //// compute Debit / Credit from Monto (debit when negative, credit when positive)
                    //mov.Debito = mov.Monto < 0 ? Math.Abs(mov.Monto) : 0m;
                    //mov.Credito = mov.Monto > 0 ? mov.Monto : 0m;

                    CalcularDebitoCredito(mov);
                }
                catch (Exception ex)
                {
                    mov.CuentaPredicha = mov.CuentaPredicha ?? "ERROR";
                    mov.ScorePrediccion = 0;
                    _logger?.LogError(ex, "Error while enriching/predicting movement");
                }
            }

            // Create a temporary batch ID and store in-memory for later editing/export
            var batchId = Guid.NewGuid();

            _tempTables[batchId] = new TempBatch
            {
                Items = movimientosTotales.Select(m => CloneMovimientoForTemp(m)).ToList(),
                BankId = BancoId,
                SelectedCuentaQB = LastSelectedAccountQB,
                InterestCharged = anyInterestFound ? totalInterest : (decimal?)null
            };

            ViewBag.BatchId = batchId;
            ViewBag.BankName = bancoSeleccionado?.BankName ?? "";
            ViewBag.ItemsCount = movimientosTotales.Count;
            ViewBag.SelectedCuentaQB = LastSelectedAccountQB;
            ViewBag.LastInterestCharged = anyInterestFound ? totalInterest : (decimal?)null;

            return View("Resultados", movimientosTotales);
        }

        // Helper to clone Movimiento (avoid accidental shared refs)
        private Movimiento CloneMovimientoForTemp(Movimiento m) =>
                new Movimiento
                {
                    Id = m.Id,
                    Fecha = m.Fecha,
                    Descripcion = m.Descripcion,
                    Referencia = m.Referencia,
                    Ciudad = m.Ciudad,
                    Estado = m.Estado,
                    Telefono = m.Telefono,
                    Categoria = m.Categoria,
                    Detalle = m.Detalle,
                    ZipCode = m.ZipCode,
                    Monto = m.Monto,
                    Moneda = m.Moneda,
                    FechaInicio = m.FechaInicio,
                    FechaFin = m.FechaFin,
                    Documento = m.Documento,
                    Pasajero = m.Pasajero,
                    TipoDocumento = m.TipoDocumento,
                    Notas = m.Notas,
                    Empresa = m.Empresa,
                    CuentaPredicha = m.CuentaPredicha,
                    ScorePrediccion = m.ScorePrediccion,
                    EnrichedIndustry = m.EnrichedIndustry,
                    EnrichedSubIndustry = m.EnrichedSubIndustry,
                    EnrichedSector = m.EnrichedSector,
                    EnrichedNaics = m.EnrichedNaics,
                    EnrichedTags = m.EnrichedTags,
                    EnrichedCompanyDomain = m.EnrichedCompanyDomain,
                    CuentaContableAplicada = m.CuentaContableAplicada,
                    EmpresaExtraida = m.EmpresaExtraida,
                    // copy the computed fields
                    Debito = m.Debito,
                    Credito = m.Credito
                };

        private void CalcularDebitoCredito(Movimiento mov)
        {
            var desc = mov.Descripcion ?? "";
            var montoAbs = Math.Abs(mov.Monto);

            bool contextoCredito = regexCreditoContexto.IsMatch(desc);
            bool contextoDebito = regexDebitoContexto.IsMatch(desc);

            if (contextoCredito)
            {
                mov.Debito = montoAbs;
                mov.Credito = 0m;
                return;
            }

            if (contextoDebito)
            {
                if (mov.Monto < 0)
                {
                    mov.Debito = montoAbs;
                    mov.Credito = 0m;
                }
                else
                {
                    mov.Credito = montoAbs;
                    mov.Debito = 0m;
                }
                return;
            }

            if (mov.Monto < 0)
            {
                mov.Debito = montoAbs;
                mov.Credito = 0m;
            }
            else
            {
                mov.Credito = montoAbs;
                mov.Debito = 0m;
            }
        }


        [HttpGet]
        public IActionResult EditTemporal(Guid batchId, string? sortBy = null, string? sortDir = "asc")
        {
            if (!_tempTables.TryGetValue(batchId, out var batch))
            {
                // Log useful diagnostic info
                _logger?.LogWarning("EditTemporal: batch {BatchId} not found. Current in-memory batches: {Keys}",
                    batchId,
                    _tempTables.Keys.Any() ? string.Join(", ", _tempTables.Keys) : "<none>");

                // Provide friendly UX: store message and redirect to Index so user can retry
                TempData["ErrorMessage"] = $"Batch '{batchId}' not found. It may have expired or the app was restarted.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.BatchId = batchId;
            if (batch.BankId.HasValue)
            {
                var bank = _context.Bank.Find(batch.BankId.Value);
                ViewBag.BankName = bank?.BankName ?? "";
            }
            else
            {
                ViewBag.BankName = "";
            }

            ViewBag.ItemsCount = batch.Items?.Count ?? 0;
            ViewBag.SortBy = sortBy;
            ViewBag.SortDir = sortDir;
            ViewBag.SelectedCuentaQB = batch.SelectedCuentaQB ?? "";


            IEnumerable<Movimiento> itemsToShow = batch.Items ?? new List<Movimiento>();

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                if (sortBy.Equals("descripcion", StringComparison.OrdinalIgnoreCase))
                {
                    itemsToShow = sortDir?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true
                        ? itemsToShow.OrderByDescending(m => m.Descripcion)
                        : itemsToShow.OrderBy(m => m.Descripcion);
                }
                else if (sortBy.Equals("fecha", StringComparison.OrdinalIgnoreCase))
                {
                    itemsToShow = sortDir?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true
                        ? itemsToShow.OrderByDescending(m => m.Fecha)
                        : itemsToShow.OrderBy(m => m.Fecha);
                }
                // Add other fields if you want to support more sorting keys.
            }

            return View("Resultados", itemsToShow.ToList());
        }

        [HttpPost]
        public IActionResult UpdateTemporal(Guid batchId, List<Movimiento> movimientos)
        {
            if (movimientos == null)
                return BadRequest();

            //_tempTables[batchId] = new TempBatch
            //{
            //    Items = movimientos.Select(m => CloneMovimientoForTemp(m)).ToList(),
            //    BankId = _tempTables.TryGetValue(batchId, out var existing) ? existing.BankId : null
            //};
            _tempTables[batchId] = new TempBatch
            {
                Items = movimientos.Select(m => CloneMovimientoForTemp(m)).ToList(),
                BankId = _tempTables.TryGetValue(batchId, out var existing) ? existing.BankId : null,
                SelectedCuentaQB = _tempTables.TryGetValue(batchId, out var ex2) ? ex2.SelectedCuentaQB : null
            };

            return RedirectToAction(nameof(EditTemporal), new { batchId });
        }

        // Changed to GET so browser can navigate and show Save dialog. Accepts optional filename.
        [HttpGet]
        public IActionResult ExportarTemporal(Guid batchId, string? filename = null)
        {
            if (!_tempTables.TryGetValue(batchId, out var batch) || batch.Items.Count == 0)
                return NotFound("Batch not found or empty");

            var bytes = BuildExcelBytes(batch.Items, out var fileName, filename);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // New endpoint: receive selected text and learn/update Empresa for the item in the temp table
        // Note: we keep this simple and allow anonymous POSTs for frontend JS by ignoring antiforgery.
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SelectEmpresa([FromBody] SelectEmpresaDto dto)
        {
            if (dto == null || dto.BatchId == Guid.Empty || string.IsNullOrWhiteSpace(dto.Selected))
                return BadRequest(new { success = false, message = "Invalid input" });

            if (!_tempTables.TryGetValue(dto.BatchId, out var batch))
                return NotFound(new { success = false, message = "Batch not found" });

            if (dto.Index < 0 || dto.Index >= batch.Items.Count)
                return BadRequest(new { success = false, message = "Index out of range" });

            // Normalize to UPPERCASE before storing/learning
            var selected = dto.Selected.Trim().ToUpperInvariant();
            batch.Items[dto.Index].Empresa = selected;
            batch.Items[dto.Index].EmpresaExtraida = selected;

            // Learn a simple pattern that captures the exact selected text for this bank
            try
            {
                var bankId = batch.BankId;
                AutoAddPattern(bankId, $"({Regex.Escape(selected)})", "User-selected: learned exact match");
            }
            catch
            {
                // ignore learning failures
            }

            // Persist as recognized company so future Procesar runs pick it up immediately
            try
            {
                var selUpper = selected.ToUpperInvariant();
                var exists = _context.EmpresasReconocidas
                    .Any(e => e.TextoOriginal != null && e.TextoOriginal.ToUpper() == selUpper);

                if (!exists)
                {
                    _context.EmpresasReconocidas.Add(new EmpresaReconocida
                    {
                        TextoOriginal = selected,
                        FechaRegistro = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to persist EmpresaReconocida for '{empresa}'", selected);
                return StatusCode(500, new { success = false, message = "Failed saving empresa" });
            }

            return Ok(new { success = true, empresa = selected });
        }

        public class SelectEmpresaDto
        {
            public Guid BatchId { get; set; }
            public int Index { get; set; }
            public string Selected { get; set; } = "";
        }

        // Updated to accept optional desiredFileName and sanitize it
        private byte[] BuildExcelBytes(List<Movimiento> movimientos, out string fileName, string? desiredFileName = null)
        {
            using var paquete = new ExcelPackage();
            var hoja = paquete.Workbook.Worksheets.Add("Movimientos");

            hoja.Cells[1, 1].Value = "Fecha";
            hoja.Cells[1, 2].Value = "Empresa Extraida";
            hoja.Cells[1, 3].Value = "Empresa";
            hoja.Cells[1, 4].Value = "Descripción";
            hoja.Cells[1, 5].Value = "Monto";
            hoja.Cells[1, 6].Value = "Cuenta";

            for (int i = 0; i < movimientos.Count; i++)
            {
                var mov = movimientos[i];
                hoja.Cells[i + 2, 1].Value = mov.Fecha.ToString("MM/dd/yyyy");
                hoja.Cells[i + 2, 2].Value = mov.EmpresaExtraida;
                hoja.Cells[i + 2, 3].Value = mov.Empresa;
                hoja.Cells[i + 2, 4].Value = mov.Descripcion;
                hoja.Cells[i + 2, 5].Value = mov.Monto;
                hoja.Cells[i + 2, 6].Value = mov.CuentaPredicha;
            }

            // If caller provided a filename, sanitize and use it; otherwise generate one.
            if (!string.IsNullOrWhiteSpace(desiredFileName))
            {
                try
                {
                    var sanitized = desiredFileName;
                    // replace invalid filename chars
                    foreach (var c in Path.GetInvalidFileNameChars())
                        sanitized = sanitized.Replace(c, '_');
                    if (!sanitized.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                        sanitized += ".xlsx";
                    fileName = sanitized;
                }
                catch
                {
                    fileName = "MOV" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".xlsx";
                }
            }
            else
            {
                fileName = "MOV" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".xlsx";
            }

            return paquete.GetAsByteArray();
        }

        private static string MapClearbitToAccount(ReadingPdf.Services.ClearbitResult cb, string description)
        {
            // Reglas heurísticas: prioridad por NAICS -> industry/subIndustry -> tags -> descripción
            string text = (description ?? "").ToUpperInvariant();

            // 1) NAICS-based rules (comunes)
            if (!string.IsNullOrWhiteSpace(cb.Naics))
            {
                var naics = cb.Naics;
                if (naics.StartsWith("722")) // Food services and drinking places
                    return "Expenses:Restaurants";
                if (naics.StartsWith("447") || naics.StartsWith("324") || naics.Contains("FUEL")) // Gas stations / petroleum
                    return "Expenses:Gasoline";
                if (naics.StartsWith("481") || naics.StartsWith("482") || naics.StartsWith("488")) // Transportation/air/ship
                    return "Expenses:Transportation";
                if (naics.StartsWith("5112") || naics.StartsWith("518") || naics.StartsWith("519")) // software/online
                    return "Expenses:Software";
            }

            // 2) Industry / SubIndustry
            var industry = (cb.Industry ?? "").ToUpperInvariant();
            var sub = (cb.SubIndustry ?? "").ToUpperInvariant();
            if (industry.Contains("RESTAURANT") || sub.Contains("RESTAURANT") || industry.Contains("FOOD"))
                return "Expenses:Restaurants";
            if (industry.Contains("GAS") || industry.Contains("FUEL") || sub.Contains("PETROLEUM"))
                return "Expenses:Gasoline";
            if (industry.Contains("TRAVEL") || industry.Contains("AIRLINES") || industry.Contains("TRANSPORT"))
                return "Expenses:Travel";
            if (industry.Contains("SOFTWARE") || industry.Contains("TECH") || industry.Contains("INFORMATION TECHNOLOGY"))
                return "Expenses:Software";
            if (industry.Contains("WHOLESALE") || industry.Contains("RETAIL"))
                return "Expenses:Supplies";

            // 3) Tags
            if (cb.Tags != null)
            {
                foreach (var t in cb.Tags)
                {
                    var tt = t.ToUpperInvariant();
                    if (tt.Contains("RESTAURANT") || tt.Contains("FOOD")) return "Expenses:Restaurants";
                    if (tt.Contains("GAS") || tt.Contains("FUEL")) return "Expenses:Gasoline";
                    if (tt.Contains("SAAS") || tt.Contains("SOFTWARE")) return "Expenses:Software";
                    if (tt.Contains("HOTEL") || tt.Contains("LODGE")) return "Expenses:Travel";
                }
            }

            // 4) Fallback: buscar palabras clave en la descripción del movimiento (heurística)
            if (text.Contains("RESTAURANT") || text.Contains("DINER") || text.Contains("FOOD") || text.Contains("CAFÉ") || text.Contains("CAFE"))
                return "Expenses:Restaurants";
            if (text.Contains("GAS") || text.Contains("PETROLEUM") || text.Contains("PUMP") || text.Contains("ESTACION"))
                return "Expenses:Gasoline";
            if (text.Contains("UBER") || text.Contains("LYFT") || text.Contains("TAXI") || text.Contains("TRANSPORT"))
                return "Expenses:Transportation";
            if (text.Contains("HOTEL") || text.Contains("MOTEL") || text.Contains("AIRLINES") || text.Contains("AIRLINE") || text.Contains("TICKET"))
                return "Expenses:Travel";
            if (text.Contains("SOFT") || text.Contains("SAAS") || text.Contains("SUBSCRIPTION") || text.Contains("ANNUAL") && text.Contains("FEE"))
                return "Expenses:Software";

            // Default
            return "Expenses:Uncategorized";
        }

        // Existing GuardarExcel kept for backwards compatibility (still used nowhere by default)
        private void GuardarExcel(List<Movimiento> movimientos)
        {
            using var paquete = new ExcelPackage();
            var hoja = paquete.Workbook.Worksheets.Add("Movimientos");

            hoja.Cells[1, 1].Value = "Fecha";
            hoja.Cells[1, 2].Value = "Empresa Extraida";
            hoja.Cells[1, 3].Value = "Empresa";
            hoja.Cells[1, 4].Value = "Descripción";
            hoja.Cells[1, 5].Value = "Monto";
            hoja.Cells[1, 6].Value = "Cuenta";

            for (int i = 0; i < movimientos.Count; i++)
            {
                var mov = movimientos[i];
                hoja.Cells[i + 2, 1].Value = mov.Fecha.ToString("MM/dd/yyyy");
                hoja.Cells[i + 2, 2].Value = mov.EmpresaExtraida;
                hoja.Cells[i + 2, 3].Value = mov.Empresa;
                hoja.Cells[i + 2, 4].Value = mov.Descripcion;
                hoja.Cells[i + 2, 5].Value = mov.Monto;
                hoja.Cells[i + 2, 6].Value = mov.CuentaPredicha;
            }

            string nombreArchivo = "MOV" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".xlsx";
            var ruta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", nombreArchivo);
            paquete.SaveAs(new FileInfo(ruta));
        }

        private class ProgressInfo
        {
            public int Percent { get; set; }
            public string Status { get; set; } = "";
            public bool Done { get; set; } = false;
            public int TotalFiles { get; set; } = 0;
            public int ProcessedFiles { get; set; } = 0;
            public int TotalMovements { get; set; } = 0;
            public int ProcessedMovements { get; set; } = 0;
        }

        private static readonly ConcurrentDictionary<Guid, ProgressInfo> _progress = new();

        public string CuentaQB { get; private set; }
        public string LastSelectedAccountQB { get; private set; }

        [HttpPost]
        [IgnoreAntiforgeryToken] // called via fetch from client; keep same-origin in production or use antiforgery token.
        public IActionResult StartProcesar(List<IFormFile> archivosPdf, int BancoId, string? CuentaQB)
        {
            if (archivosPdf == null || archivosPdf.Count == 0)
                return BadRequest(new { error = "No files" });

            // capture the selected QuickBooks account from the incoming request
            var capturedCuentaQB = CuentaQB ?? "";

            // create batch id and progress entry
            var batchId = Guid.NewGuid();
            var prog = new ProgressInfo { Percent = 0, Status = "Queued", TotalFiles = archivosPdf.Count };
            _progress[batchId] = prog;

            // Create an initial empty TempBatch immediately so EditTemporal won't return 404
            // (will be overwritten with real results when background processing completes)
            _tempTables[batchId] = new TempBatch
            {
                Items = new List<Movimiento>(),
                BankId = BancoId,
                SelectedCuentaQB = capturedCuentaQB
            };

            // save files to temp paths
            var tempPaths = new List<string>();
            try
            {
                foreach (var f in archivosPdf)
                {
                    var temp = Path.GetTempFileName();
                    using (var fs = new FileStream(temp, FileMode.Create))
                    {
                        f.CopyTo(fs);
                    }
                    tempPaths.Add(temp);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed saving uploaded files to temp");
                _progress.TryRemove(batchId, out _);
                // remove the early temp entry to avoid stale empty batch if we failed
                _tempTables.TryRemove(batchId, out _);
                return StatusCode(500, new { error = "Failed saving files" });
            }

            // capture bank snapshot (safe to pass simple entity)
            var bancoSeleccionado = _context.Bank.Find(BancoId);

            // Fire-and-forget background processing
            Task.Run(async () =>
            {
                try
                {
                    prog.Status = "Processing files";
                    prog.TotalFiles = tempPaths.Count;
                    prog.ProcessedFiles = 0;
                    var movimientosTotales = new List<Movimiento>();

                    // Track only first InterestCharged value
                    decimal totalInterest = 0m;
                    bool anyInterestFound = false;

                    // 1) parse each PDF and update progress per file
                    for (int i = 0; i < tempPaths.Count; i++)
                    {
                        var path = tempPaths[i];
                        prog.Status = $"Parsing file {i + 1} / {tempPaths.Count}";
                        try
                        {
                            var movs = AmexParser.ParsePdf(path, bancoSeleccionado);
                            if (movs != null && movs.Count > 0)
                                movimientosTotales.AddRange(movs);

                            // capture InterestCharged if present, but only the first PDF value
                            if (!anyInterestFound)
                            {
                                if (AmexParser.LastInterestCharged.HasValue)
                                {
                                    totalInterest = AmexParser.LastInterestCharged.Value;
                                    anyInterestFound = true;
                                }
                                else if (AmexParser.PaymentsCredits.HasValue)
                                {
                                    totalInterest = AmexParser.PaymentsCredits.Value;
                                    anyInterestFound = true;
                                }
                                else if (AmexParser.BalanceAnterior.HasValue)
                                {
                                    totalInterest = AmexParser.BalanceAnterior.Value;
                                    anyInterestFound = true;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error parsing file {file}", path);
                        }
                        prog.ProcessedFiles = i + 1;
                        prog.Percent = (int)((prog.ProcessedFiles / (double)Math.Max(1, prog.TotalFiles)) * 30); // first 30% for parsing
                    }

                    prog.Status = "Enriching and predicting";
                    prog.TotalMovements = movimientosTotales.Count;
                    prog.ProcessedMovements = 0;

                    // load recognized companies once
                    var empresasReconocidas = _context.EmpresasReconocidas
                        .Where(e => !string.IsNullOrWhiteSpace(e.TextoOriginal))
                        .Select(e => e.TextoOriginal!)
                        .ToList();

                    var clearbitCache = new ConcurrentDictionary<string, ReadingPdf.Services.ClearbitResult?>();

                    // model file paths for local predictor
                    var modelPath = Path.Combine(Directory.GetCurrentDirectory(), "models", "account-predictor.zip");
                    var tokensPath = Path.Combine(Directory.GetCurrentDirectory(), "models", "tokens_by_company.json");
                    var labelsPath = Path.Combine(Directory.GetCurrentDirectory(), "models", "labels.json");

                    for (int mi = 0; mi < movimientosTotales.Count; mi++)
                    {
                        var mov = movimientosTotales[mi];
                        try
                        {
                            // detection by recognized companies
                            string? detected = null;
                            if (!string.IsNullOrWhiteSpace(mov.Descripcion))
                            {
                                detected = empresasReconocidas
                                    .FirstOrDefault(e => mov.Descripcion.IndexOf(e, StringComparison.OrdinalIgnoreCase) >= 0);
                            }

                            if (!string.IsNullOrWhiteSpace(detected))
                            {
                                mov.Empresa = detected.ToUpperInvariant();
                            }
                            else
                            {
                                if (string.IsNullOrWhiteSpace(mov.Empresa))
                                {
                                    var extracted = ExtractNameFromMemo(mov.Descripcion, BancoId);
                                    if (!string.IsNullOrWhiteSpace(extracted))
                                        mov.Empresa = extracted.ToUpperInvariant();
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(mov.Empresa))
                            {
                                var key = mov.Empresa.Trim().ToUpperInvariant();
                                if (!clearbitCache.TryGetValue(key, out var cbResult))
                                {
                                    var domain = await _clearbit.FindDomainByNameAsync(mov.Empresa);
                                    if (!string.IsNullOrWhiteSpace(domain))
                                        cbResult = await _clearbit.EnrichByDomainAsync(domain);
                                    else
                                        cbResult = await _clearbit.EnrichByDomainAsync(mov.Empresa);

                                    clearbitCache[key] = cbResult;
                                }

                                if (cbResult != null)
                                {
                                    mov.EnrichedCompanyDomain = cbResult.Domain;
                                    mov.EnrichedIndustry = cbResult.Industry;
                                    mov.EnrichedSubIndustry = cbResult.SubIndustry;
                                    mov.EnrichedSector = cbResult.Sector;
                                    mov.EnrichedNaics = cbResult.Naics;
                                    mov.EnrichedTags = cbResult.Tags != null ? string.Join(",", cbResult.Tags) : null;

                                    mov.CuentaContableAplicada = MapClearbitToAccount(cbResult, mov.Descripcion);
                                    if (string.IsNullOrWhiteSpace(mov.CuentaPredicha))
                                        mov.CuentaPredicha = mov.CuentaContableAplicada;
                                }
                            }

                            // 3) Predicción remota usando PredictionController API, con fallback a predictor local
                            try
                            {
                                bool applied = false;

                                // 3a) Intentar predicción remota primero
                                try
                                {
                                    var remote = await _predictionClient.PredictAccountAsync(mov.Descripcion ?? "", mov.Empresa ?? "", CancellationToken.None);
                                    if (!string.IsNullOrWhiteSpace(remote))
                                    {
                                        mov.CuentaPredicha = remote;
                                        // remote API doesn't return score; use a sentinel high value to indicate confidence
                                        mov.ScorePrediccion = 100;
                                        applied = true;
                                    }
                                }
                                catch (Exception exRemote)
                                {
                                    _logger?.LogWarning(exRemote, "Remote prediction API failed for movement {desc}", mov.Descripcion);
                                }

                                // 3b) Si la remota no arrojó resultado, usar predictor local como fallback
                                if (!applied)
                                {
                                    var planDeCuentas = GetCompanyPlanAccounts(mov.Empresa ?? "");
                                    var (account, score) = _accountPredictor.PredictForCompany(mov.Descripcion ?? "", mov.Empresa ?? "", planDeCuentas, modelPath, tokensPath, labelsPath);
                                    if (!string.IsNullOrWhiteSpace(account))
                                    {
                                        mov.CuentaPredicha = account;
                                        // Convert float score in [0..1] to 0..100; keep 0 if unknown
                                        mov.ScorePrediccion = (int)Math.Round(score * 100);
                                    }
                                    else
                                    {
                                        if (string.IsNullOrWhiteSpace(mov.CuentaPredicha))
                                            mov.CuentaPredicha = "SIN PREDICCION";
                                        mov.ScorePrediccion = 0;
                                    }
                                }
                            }
                            catch (Exception exPred)
                            {
                                _logger?.LogWarning(exPred, "Prediction flow failed for movement {desc}", mov.Descripcion);
                                if (string.IsNullOrWhiteSpace(mov.CuentaPredicha))
                                    mov.CuentaPredicha = "SIN PREDICCION";
                                mov.ScorePrediccion = 0;
                            }

                            // compute Debito/Credito
                            mov.Debito = mov.Monto < 0 ? Math.Abs(mov.Monto) : 0m;
                            mov.Credito = mov.Monto > 0 ? mov.Monto : 0m;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error processing movement {idx}", mi);
                            mov.CuentaPredicha = mov.CuentaPredicha ?? "ERROR";
                            mov.ScorePrediccion = 0;
                        }

                        prog.ProcessedMovements = mi + 1;
                        var part = 30 + (int)((prog.ProcessedMovements / (double)Math.Max(1, prog.TotalMovements)) * 70);
                        prog.Percent = Math.Min(100, part);
                        prog.Status = $"Processed {prog.ProcessedMovements}/{prog.TotalMovements} movimientos";
                    }

                    // store results in in-memory temp table (overwrites the early empty entry)
                    _tempTables[batchId] = new TempBatch
                    {
                        Items = movimientosTotales.Select(m => CloneMovimientoForTemp(m)).ToList(),
                        BankId = BancoId,
                        SelectedCuentaQB = capturedCuentaQB,
                        InterestCharged = anyInterestFound ? totalInterest : (decimal?)null
                    };

                    prog.Status = "Done";
                    prog.Percent = 100;
                    prog.Done = true;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Background processing failed for batch {batch}", batchId);
                    _progress.TryGetValue(batchId, out var pFail);
                    if (pFail != null)
                    {
                        pFail.Status = "Error";
                        pFail.Done = true;
                    }
                }
                finally
                {
                    // cleanup temp files
                    try
                    {
                        foreach (var t in tempPaths)
                        {
                            try { System.IO.File.Delete(t); } catch { }
                        }
                    }
                    catch { }
                }
            });

            // return batch id to client so it can poll
            return Json(new { batchId = batchId });
        }

        [HttpGet]
        public IActionResult GetProgress(Guid batchId)
        {
            if (!_progress.TryGetValue(batchId, out var progInfo))
                return NotFound(new { error = "Batch not found" });

            return Json(new
            {
                percent = progInfo.Percent,
                status = progInfo.Status,
                done = progInfo.Done,
                processedFiles = progInfo.ProcessedFiles,
                totalFiles = progInfo.TotalFiles,
                processedMovements = progInfo.ProcessedMovements,
                totalMovements = progInfo.TotalMovements
            });
        }

        public static List<Movimiento> GetItems(Guid batchId)
        {
            if (_tempTables.TryGetValue(batchId, out var tempBatch))
            {
                return tempBatch.Items;
            }

            return new List<Movimiento>();
        }

        public class SyncPlanDto
        {
            public string NombreEmpresa { get; set; } = "";
            public List<string> Cuentas { get; set; } = new List<string>();
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SyncPlanCuentas([FromBody] SyncPlanDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.NombreEmpresa))
                return BadRequest(new { success = false, message = "Invalid input" });

            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.ExpectContinue = false;
                http.DefaultRequestVersion = new Version(1,1);
                http.Timeout = TimeSpan.FromSeconds(30);



                // 1) Registrar u obtener empresa en la ClasificacionApi
                Console.WriteLine("URL => " + $"{_clasificacionApiBase}/api/Clasificacion/registrar-empresa");

                var req1 = JsonConvert.SerializeObject(new { nombre = dto.NombreEmpresa });
                var res1 = await http.PostAsync($"{_clasificacionApiBase}/api/Clasificacion/registrar-empresa",
                    new StringContent(req1, Encoding.UTF8, "application/json"));

//                Console.WriteLine("URL => " + $"{_clasificacionApiBase}/api/Clasificacion/registrar-empresa");
                Console.WriteLine("JSON => " + req1);



                if (!res1.IsSuccessStatusCode)
                    return StatusCode((int)res1.StatusCode, new { success = false, message = "Failed obtaining empresa from ClasificacionApi" });

                var c1 = await res1.Content.ReadAsStringAsync();
                int empresaId = 0;
                try
                {
                    var dyn = JsonConvert.DeserializeObject<dynamic>(c1);
                    empresaId = (int)(dyn?.empresaId ?? dyn?.EmpresaId ?? 0);
                }
                catch
                {
                    int.TryParse(c1, out empresaId);
                }

                if (empresaId == 0)
                    return StatusCode(500, new { success = false, message = "Invalid empresaId returned by ClasificacionApi" });

                // 2) Sincronizar plan de cuentas
                var req2 = JsonConvert.SerializeObject(new { empresaId = empresaId, cuentas = dto.Cuentas ?? new List<string>() });
                var res2 = await http.PostAsync($"{_clasificacionApiBase}/api/plan/sincronizar",
                    new StringContent(req2, Encoding.UTF8, "application/json"));

                if (!res2.IsSuccessStatusCode)
                    return StatusCode((int)res2.StatusCode, new { success = false, message = "Failed syncing plan in ClasificacionApi" });

                var c2 = await res2.Content.ReadAsStringAsync();
                int nuevos = 0;
                try
                {
                    var dyn2 = JsonConvert.DeserializeObject<dynamic>(c2);
                    nuevos = (int)(dyn2?.nuevos ?? dyn2?.Nuevos ?? 0);
                }
                catch
                {
                    int.TryParse(c2, out nuevos);
                }

                return Ok(new { success = true, empresaId = empresaId, nuevos = nuevos });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "SyncPlanCuentas failed");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult PredictCuentaPorEmpresa([FromBody] PredictRequest req, [FromServices] AccountPredictionService predictor)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Memo) || string.IsNullOrWhiteSpace(req.Company))
                return BadRequest(new { success = false, message = "Invalid request" });

            // Obtén el plan de cuentas de la empresa desde la BD.
            // Ajusta este bloque a la estructura real de tu tabla Empresa / plan de cuentas.
            List<string> planDeCuentas = GetCompanyPlanAccounts(req.Company);

            // Rutas de archivos del modelo (colócalas donde quieras en tu proyecto)
            var modelPath = Path.Combine(Directory.GetCurrentDirectory(), "models", "account-predictor.zip");
            var tokensPath = Path.Combine(Directory.GetCurrentDirectory(), "models", "tokens_by_company.json");
            var labelsPath = Path.Combine(Directory.GetCurrentDirectory(), "models", "labels.json");

            try
            {
                var (account, score) = predictor.PredictForCompany(req.Memo, req.Company, planDeCuentas, modelPath, tokensPath, labelsPath);
                return Ok(new { success = true, cuenta = account, score = score });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "PredictCuentaPorEmpresa failed");
                return StatusCode(500, new { success = false, message = "Error predicting" });
            }
        }

        public class PredictRequest
        {
            public string Memo { get; set; } = "";
            public string Company { get; set; } = "";
        }

        // Example helper — adapt to your schema:
        private List<string> GetCompanyPlanAccounts(string company)
        {
            // TODO: reemplaza esto por la consulta real de tu tabla que contiene el plan de cuentas por empresa.
            // Ejemplo simple: buscar en _context.Empresas o en una tabla de cuentas asociadas y devolver nombres o códigos.
            // Por ahora devolvemos un conjunto de cuentas de ejemplo.
            return new List<string>
            {
                "Expenses:Restaurants",
                "Expenses:Gasoline",
                "Expenses:Software",
                "Expenses:Travel",
                "Expenses:Uncategorized"
            };
        }
        // C#
        public class PredItem
        {
            [ColumnName("Label")]
            public string Label { get; set; } = ""; // dummy value for prediction

            public string Memo { get; set; } = "";
            public string Company { get; set; } = "";
            public float SimilarWordsCount { get; set; }
        }

        // GET: /PdfNew/ExportCsv?batchId={guid}
        [HttpGet]
        public IActionResult ExportCsv(Guid batchId)
        {
            if (!_tempTables.TryGetValue(batchId, out var batch) || batch?.Items == null)
                return NotFound();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Fecha,Empresa,Descripcion,Debito,Credito,Cuenta,TipoDocumento");

            foreach (var m in batch.Items)
            {
                var fecha = m != null ? m.Fecha.ToString("yyyy-MM-dd") : "";
                string Escape(string? s)
                {
                    if (string.IsNullOrEmpty(s)) return "";
                    s = s.Replace("\"", "\"\"");
                    if (s.Contains(',') || s.Contains('\n') || s.Contains('\r') || s.Contains('"'))
                        return $"\"{s}\"";
                    return s;
                }

                var empresa = Escape(m?.Empresa);
                var desc = Escape(m?.Descripcion);
                var debito = (m?.Debito ?? 0m).ToString(System.Globalization.CultureInfo.InvariantCulture);
                var credito = (m?.Credito ?? 0m).ToString(System.Globalization.CultureInfo.InvariantCulture);
                var cuenta = Escape(m?.CuentaPredicha ?? m?.CuentaContableAplicada);
                var tipo = Escape(m?.TipoDocumento);

                sb.AppendLine($"{fecha},{empresa},{desc},{debito},{credito},{cuenta},{tipo}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"results_{batchId}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }
    }
}

