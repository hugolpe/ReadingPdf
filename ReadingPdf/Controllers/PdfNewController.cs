using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.ML.Data;
using Newtonsoft.Json;
using NPOI.Util;
using OfficeOpenXml;
using ReadingPdf.Data;
using ReadingPdf.Models;
using ReadingPdf.Parsers;
using ReadingPdf.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
using static ReadingPdf.Controllers.PdfNewController;

namespace ReadingPdf.Controllers
{
    public class PdfNewController : Controller
    {
        private static readonly Regex regexCreditoContexto = new Regex(@"(CREDITO|CRÉDITO|DEPÓSITO|DEPOSITO|DEPOSIT|CREDIT)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex regexDebitoContexto = new Regex(@"(DÉBITO|DEBITO|DEBIT|RETIRO|WITHDRAWAL|CHARGE)", RegexOptions.IgnoreCase | RegexOptions.Compiled);



        private class TempBatch
        {
            public List<Movimiento> Items { get; set; } = new();
            public int? BankId { get; set; }
            public string? SelectedCuentaQB { get; set; }
            public decimal? InterestCharged { get; set; }
        }
        // ✅ 6. AGREGAR PROPIEDAD Percent A BatchProgress


        public class BatchProgress
        {
            public string BatchId { get; set; } = "";
            public int TotalFiles { get; set; }
            public int ProcessedFiles { get; set; }
            public int TotalMovements { get; set; }
            public int ProcessedMovements { get; set; }
            public int Percent { get; set; }
            public string Status { get; set; } = "";
            public bool Done { get; set; }
            public DateTime CompletedAt { get; set; }
            public string SelectedAccountQB { get; set; } = "";
            public List<Movimiento> Movimientos { get; set; } = new();
            public decimal? InterestCharged { get; set; }  // ✅ NUEVA PROPIEDAD
                                                           // ✅ AGREGAR ESTA LÍNEA
            public string BankName { get; set; } = "";  // ← NUEVO CAMPO
            public string EmpresaQBName { get; set; } = "";  // ✅ AGREGAR ESTA LÍNEA
        }

        private static readonly ConcurrentDictionary<Guid, TempBatch> _tempTables = new();
        private static readonly ConcurrentDictionary<string, BatchProgress> _batchProgress = new();

        private readonly string _clasificacionApiBase = "http://localhost:7164";
        private readonly ApplicationDbContext _context;
        private readonly ApplicationDbContext _db;
        private readonly IClearbitService _clearbit;
        private readonly string _patternsPath;
        private static readonly object _patternsLock = new();
        private readonly ILogger<PdfNewController> _logger;
        private readonly AccountPredictionService _accountPredictor;
        private readonly IPredictionApiClient _predictionClient;
        private readonly IServiceScopeFactory _scopeFactory;

        public PdfNewController(ApplicationDbContext context, IClearbitService clearbit, ILogger<PdfNewController> logger, AccountPredictionService accountPredictor, IPredictionApiClient predictionClient, IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _db = context;
            _clearbit = clearbit;
            _logger = logger;
            _patternsPath = Path.Combine(Directory.GetCurrentDirectory(), "extraction_patterns.json");
            _accountPredictor = accountPredictor;
            _predictionClient = predictionClient;
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        }

        // ✅ NUEVO MÉTODO: Llamar a la API de predicción
        private async Task<(string? account, int confidence)> PredictAccountViaApi(string memo, string company)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                var requestBody = new
                {
                    Memo = memo ?? "",
                    Company = company ?? ""
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger?.LogInformation("Calling prediction API for memo: {Memo}, company: {Company}", memo, company);

                Console.WriteLine($"{_clasificacionApiBase}/api/Prediction");

                var response = await httpClient.PostAsync($"{_clasificacionApiBase}/api/Prediction", content);



                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();

                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        var predictedAccount = result.Trim('"');
                        _logger?.LogInformation("Prediction API returned: {Account}", predictedAccount);
                        return (predictedAccount, 100);
                    }
                }
                else
                {
                    _logger?.LogWarning("Prediction API returned status code: {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error calling prediction API for memo: {Memo}", memo);
            }

            return (null, 0);
        }

        [HttpGet]
        public async Task<IActionResult> ReabrirProceso(Guid id)
        {
            var proceso = await _db.Procesos
                .Include(p => p.Movimientos)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proceso == null)
                return NotFound("Proceso no encontrado");

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
                    QuickBooksTxnId = m.QuickBooksTxnId,
                    ScorePrediccion = (int)m.ScorePrediccion
                })
                .ToList();

            ViewBag.BatchId = proceso.Id;
            ViewBag.BankName = proceso.Banco;
            ViewBag.ItemsCount = movimientos.Count;
            ViewBag.LastInterestCharged = proceso.SaldoInicial;
            ViewBag.SelectedCuentaQB = proceso.CuentaQBSeleccionada ?? "";        // keep existing slot used by other code
            //ViewBag.LastSelectedAccountQB = proceso.CuentaQBSeleccionada ?? "";   // ← ADDED: show the stored account in the view
            ViewBag.LastSelectedAccountQB = movimientos.LastOrDefault().QuickBooksTxnId ?? "";   // ← ADDED: show the stored account in the view
            ViewBag.RecognizedCompanies = movimientos
                .Where(m => !string.IsNullOrWhiteSpace(m.Empresa))
                .Select(m => m.Empresa)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return View("Resultados", movimientos);
        }

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

        private static readonly string[] NoiseWords = new[]
        {
            "payment", "pago", "transfer", "transferencia", "zelle",
            "deposit", "withdrawal", "visa", "mastercard", "spei",
            "conf#", "ref#", "autoriz", "authorization", "ach",
            "purchase", "pos ", "mobile", "online"
        };

        private bool IsValidCompanyName(string name)
        {
            var norm = Normalize(name);

            if (string.IsNullOrWhiteSpace(norm))
                return false;

            if (norm.Length < 3 || norm.Length > 30)
                return false;

            if (!norm.Any(char.IsLetter))
                return false;

            if (NoiseWords.Any(n => norm.Contains(n)))
                return false;

            return true;
        }

        private class PatternEntry
        {
            public int Id { get; set; }
            public int? BankId { get; set; }
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
                }
            }
        }

        private string? ExtractNameFromMemo(string? memo, int? bankId)
        {
            if (string.IsNullOrWhiteSpace(memo))
                return null;

            memo = memo.Trim();

            var patterns = LoadPatterns();

            var ordered = patterns
                .Where(p => p.BankId == bankId || p.BankId == null)
                .OrderByDescending(p => p.BankId.HasValue)
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
                    continue;
                }
            }

            var keywords = new[]
            {
                "PAY TO","PAGO A","PAGADO A","MERCHANT","REMIT TO","VENDEDOR","PAYEE","TO:","FOR:",
                "POR:","DE:","COMPRADO EN","COMPRA EN"
            };

            foreach (var kw in keywords)
            {
                var safeKw = Regex.Escape(kw);
                var pattern = $@"(?:{safeKw})\s+([A-Za-z0-9\.\-&ÑÁÉÍÓÚñáéíóú ]{{3,80}})";
                var rx = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                var m = rx.Match(memo);
                if (m.Success && m.Groups.Count > 1)
                {
                    var candidate = m.Groups[1].Value.Trim();
                    if (candidate.Length >= 3)
                    {
                        AutoAddPattern(bankId, $@"(?:{safeKw})\s+({Regex.Escape(candidate)})", $"Auto-generated from keyword '{kw}'");
                        return candidate;
                    }
                }
            }

            var capRx = new Regex(@"([A-ZÁÉÍÓÚÑ][A-Za-zÁÉÍÓÚñáéíóú0-9\.\-&]{1,30}(?:\s+[A-ZÁÉÍÓÚÑ][A-Za-zÁÉÍÓÚñáéíóú0-9\.\-&]{1,30}){0,4})", RegexOptions.Compiled);
            var capMatch = capRx.Match(memo);
            if (capMatch.Success)
            {
                var candidate = capMatch.Groups[1].Value.Trim();
                if (candidate.Length >= 3 && candidate.Length <= 80)
                {
                    AutoAddPattern(bankId, $"({Regex.Escape(candidate)})", "Auto-generated from capitalized sequence");
                    return candidate;
                }
            }

            var allCapsRx = new Regex(@"([A-Z0-9&\.\- ]{4,80})", RegexOptions.Compiled);
            var m2 = allCapsRx.Match(memo);
            if (m2.Success)
            {
                var candidate = m2.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length >= 3)
                {
                    AutoAddPattern(bankId, $"({Regex.Escape(candidate)})", "Auto-generated from ALL CAPS segment");
                    return candidate;
                }
            }

            return null;
        }

        private void AutoAddPattern(int? bankId, string pattern, string description)
        {
            try
            {
                lock (_patternsLock)
                {
                    var list = LoadPatterns();
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
            }
        }

        [HttpGet]
        public IActionResult Index()
        {
            var bancos = _context.Bank.ToList();
            ViewBag.Bancos = bancos;
            return View();
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Procesar(List<IFormFile> archivosPdf, int BancoId, string? LastSelectedAccountQB)
        {
            if (archivosPdf == null || archivosPdf.Count == 0)
                return View("Index");

            var bancoSeleccionado = await _context.Bank.FindAsync(BancoId);
            var movimientosTotales = new List<Movimiento>();

            decimal totalInterest = 0m;
            bool anyInterestFound = false;

            foreach (var archivoPdf in archivosPdf)
            {
                var tempPath = Path.GetTempFileName();
                using (var stream = new FileStream(tempPath, FileMode.Create))
                    await archivoPdf.CopyToAsync(stream);

                string? rawText = null;

                try
                {
                    var movimientos = AmexParser.ParsePdf(tempPath, bancoSeleccionado) ?? new List<Movimiento>();

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
                        else if (AmexParser.SaldoAnteriorFees.HasValue)
                        {
                            totalInterest = AmexParser.SaldoAnteriorFees.Value;
                            anyInterestFound = true;
                        }
                    }

                    movimientosTotales.AddRange(movimientos);

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

                    continue;
                }
            }

            var empresasReconocidas = _context.EmpresasReconocidas
                .Where(e => !string.IsNullOrWhiteSpace(e.TextoOriginal))
                .Select(e => e.TextoOriginal!)
                .ToList();

            ViewBag.RecognizedCompanies = empresasReconocidas;

            var clearbitCache = new ConcurrentDictionary<string, ReadingPdf.Services.ClearbitResult?>();

            foreach (var mov in movimientosTotales)
            {
                try
                {
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
                            {
                                cbResult = await _clearbit.EnrichByDomainAsync(domain);
                            }
                            else
                            {
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

                            mov.CuentaContableAplicada = MapClearbitToAccount(cbResult, mov.Descripcion);
                            if (string.IsNullOrWhiteSpace(mov.CuentaPredicha))
                                mov.CuentaPredicha = mov.CuentaContableAplicada;
                        }
                    }

                    // ✅ CAMBIO PRINCIPAL: Usar la nueva API de predicción
                    try
                    {
                        var (predictedAccount, confidence) = await PredictAccountViaApi(
                            mov.Descripcion ?? "",
                            mov.Empresa ?? ""
                        );

                        if (!string.IsNullOrWhiteSpace(predictedAccount))
                        {
                            mov.CuentaPredicha = predictedAccount;
                            mov.ScorePrediccion = confidence;
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(mov.CuentaPredicha))
                                mov.CuentaPredicha = "SIN PREDICCION";
                            mov.ScorePrediccion = 0;
                        }
                    }
                    catch (Exception exPred)
                    {
                        _logger?.LogWarning(exPred, "⚠️ Predicción falló para movimiento: {Desc}",
    mov.Descripcion != null ? mov.Descripcion.Substring(0, Math.Min(50, mov.Descripcion.Length)) : "");
                        if (string.IsNullOrWhiteSpace(mov.CuentaPredicha))
                            mov.CuentaPredicha = "SIN PREDICCION";
                        mov.ScorePrediccion = 0;
                    }

                    CalcularDebitoCredito(mov);
                }
                catch (Exception ex)
                {
                    mov.CuentaPredicha = mov.CuentaPredicha ?? "ERROR";
                    mov.ScorePrediccion = 0;
                    _logger?.LogError(ex, "Error while enriching/predicting movement");
                }
            }

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
            ViewBag.LastSelectedAccountQB = LastSelectedAccountQB; // ← ADDED: expose the saved account to the view
            ViewBag.LastInterestCharged = anyInterestFound ? totalInterest : (decimal?)null;

            return View("Resultados", movimientosTotales);
        }

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
                    Debito = m.Debito,
                    Credito = m.Credito
                };

        private void CalcularDebitoCredito(Movimiento mov)
        {
            var desc = mov.Descripcion ?? "";
            var montoAbs = Math.Abs(mov.Monto);

            // Try to read bank id if available on the movimiento
            int? bankId = null;
            try
            {
                bankId = mov.BancoId;
            }
            catch
            {
                bankId = null;
            }

            // Special handling for Chase (BankId == 4): many Chase statements present all amounts as positive.
            if (bankId == 4)
            {
                // Use context tokens first (CREDIT/DEBIT, DEPOSITO, RETIRO, etc.)
                bool contextoCredito = regexCreditoContexto.IsMatch(desc) || Regex.IsMatch(desc, @"\bCR\b", RegexOptions.IgnoreCase);
                bool contextoDebito = regexDebitoContexto.IsMatch(desc) || Regex.IsMatch(desc, @"\bDR\b", RegexOptions.IgnoreCase);

                if (contextoCredito)
                {
                    mov.Credito = montoAbs;
                    mov.Debito = 0m;
                    return;
                }

                if (contextoDebito)
                {
                    mov.Debito = montoAbs;
                    mov.Credito = 0m;
                    return;
                }

                // Additional heuristics for Chase descriptions when values are all positive:
                var low = desc.ToLowerInvariant();

                var debitKeywords = new[]
                {
                    "purchase", "purch", "pos ", "withdrawal", "retiro", "retir", "charge", "pago", "payment", "autoriz", "authorization"
                };
                var creditKeywords = new[]
                {
                    "deposit", "deposito", "depósito", "credit", "credito", "cr", "refund", "payment received", "payment -"
                };

                if (debitKeywords.Any(k => low.Contains(k)))
                {
                    mov.Debito = montoAbs;
                    mov.Credito = 0m;
                    return;
                }

                if (creditKeywords.Any(k => low.Contains(k)))
                {
                    mov.Credito = montoAbs;
                    mov.Debito = 0m;
                    return;
                }

                // Fallback: preserve previous sign-based behavior if negative values are present.
                if (mov.Monto < 0)
                {
                    mov.Debito = montoAbs;
                    mov.Credito = 0m;
                    return;
                }
                if (mov.Monto > 0)
                {
                    // When we can't infer from description for Chase, prefer marking as Credito (common for deposit-like rows).
                    mov.Credito = montoAbs;
                    mov.Debito = 0m;
                    return;
                }

                // default
                mov.Debito = 0m;
                mov.Credito = 0m;
                return;
            }

            // Default logic for other banks (fixed to work with positive-only statements)
            bool contextoCreditoDefault = regexCreditoContexto.IsMatch(desc) || Regex.IsMatch(desc, @"\bCR\b", RegexOptions.IgnoreCase);
            bool contextoDebitoDefault = regexDebitoContexto.IsMatch(desc) || Regex.IsMatch(desc, @"\bDR\b", RegexOptions.IgnoreCase);

            if (contextoCreditoDefault)
            {
                // Description indicates a credit (e.g., DEPOSITO)
                mov.Credito = montoAbs;
                mov.Debito = 0m;
                return;
            }

            if (contextoDebitoDefault)
            {
                // Description indicates a debit (e.g., RETIRO, CHARGE)
                mov.Debito = montoAbs;
                mov.Credito = 0m;
                return;
            }

            // Extra heuristics for positive-only statements: check common keywords
            var lowDesc = desc.ToLowerInvariant();
            var debitHints = new[] { "purchase", "purch", "pos ", "withdrawal", "retiro", "retir", "charge", "payment", "pago", "autoriz", "authorization" };
            var creditHints = new[] { "deposit", "deposito", "depósito", "refund", "payment received", "payment -" };

            if (debitHints.Any(k => lowDesc.Contains(k)))
            {
                mov.Debito = montoAbs;
                mov.Credito = 0m;
                return;
            }

            if (creditHints.Any(k => lowDesc.Contains(k)))
            {
                mov.Credito = montoAbs;
                mov.Debito = 0m;
                return;
            }

            // Final fallback: use sign when available, otherwise treat positive as credit (most statements list credits as positive)
            if (mov.Monto < 0)
            {
                mov.Debito = montoAbs;
                mov.Credito = 0m;
            }
            else if (mov.Monto > 0)
            {
                mov.Credito = montoAbs;
                mov.Debito = 0m;
            }
            else
            {
                mov.Debito = 0m;
                mov.Credito = 0m;
            }
        }
        
        [HttpGet]
        public IActionResult EditTemporal(string batchId, string selectedAccountQB = "")
        {
            if (string.IsNullOrWhiteSpace(batchId))
            {
                return RedirectToAction("Index");
            }
            //******
            if (!_batchProgress.TryGetValue(batchId, out var progress))
            {
                TempData["ErrorMessage"] = "Batch no encontrado";
                return RedirectToAction("Index");
            }

            var movimientos = progress.Movimientos ?? new List<Movimiento>();

            var cuentaQB = !string.IsNullOrWhiteSpace(selectedAccountQB)
                ? selectedAccountQB
                : progress.SelectedAccountQB ?? "";

            ViewBag.BatchId = batchId;
            ViewBag.SelectedCuentaQB = cuentaQB;
            ViewBag.LastSelectedAccountQB = cuentaQB; // ← ADDED: expose the stored account for the view

            var primerMovimiento = movimientos.FirstOrDefault();
            var bancoId = primerMovimiento?.BancoId as int?;

            ViewBag.BankName = bancoId.HasValue
                ? (_context.Bank.FirstOrDefault(b => b.BankId == bancoId.Value)?.BankName ?? "")
                : "";
            // ✅ USAR EL BANKNAME GUARDADO EN PROGRESS
            ViewBag.BankName = progress.BankName ?? "";  // ← CAMBIAR ESTA LÍNEA

            
            ViewBag.ItemsCount = movimientos.Count;
            ViewBag.EmpresaQBName = progress.EmpresaQBName ?? "";  // ✅ AGREGAR ESTA LÍNEA
            ViewBag.RecognizedCompanies = movimientos
                .Where(m => !string.IsNullOrWhiteSpace(m.Empresa))
                .Select(m => m.Empresa)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            ViewBag.LastInterestCharged = AmexParser.LastInterestCharged;
            
            _logger?.LogInformation("📄 EditTemporal: Batch {BatchId}, Cuenta QB: {CuentaQB}, Movimientos: {Count}",
                batchId, cuentaQB, movimientos.Count);

            return View("Resultados", movimientos);
        }
        // ✅ NUEVO: Clase para el request de QB con AccountFullName
        public class QuickBooksChargeRequest
        {
            public string Vendor { get; set; } = "";
            public string Memo { get; set; } = "";
            public decimal Amount { get; set; }
            public string TxnDate { get; set; } = "";
            public string AccountFullName { get; set; } = "";  // ✅ CAMPO PRINCIPAL
            public string ExpenseAccount { get; set; } = "";
            public string TipoDocumento { get; set; } = ""; // ✅ AGREGADO
        }



        [HttpPost]
        public async Task<IActionResult> RegistrarEnQuickBooks([FromBody] QuickBooksChargeRequest request)
        {
            try
            {
                _logger?.LogInformation("📤 Registrando en QB: Vendor={Vendor}, Account={Account}, Amount={Amount}, Tipo={Tipo}",
                    request.Vendor, request.AccountFullName, request.Amount, request.TipoDocumento);

                // ===================================
                // VALIDACIONES
                // ===================================
                var tiposValidos = new[] {
            "CreditCardCharge",
            "Deposit",
            "Check",
            "CreditCardCredit"
        };

                if (!tiposValidos.Contains(request.TipoDocumento))
                {
                    _logger?.LogWarning("❌ Tipo de documento inválido: {Tipo}", request.TipoDocumento);
                    return Json(new
                    {
                        success = false,
                        message = $"Tipo de documento inválido: {request.TipoDocumento}. Tipos válidos: {string.Join(", ", tiposValidos)}"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Vendor))
                {
                    return Json(new { success = false, message = "Vendor es requerido" });
                }

                if (string.IsNullOrWhiteSpace(request.AccountFullName))
                {
                    return Json(new { success = false, message = "AccountFullName es requerido" });
                }

                if (request.Amount <= 0)
                {
                    return Json(new { success = false, message = "Amount debe ser mayor a 0" });
                }

                // ===================================
                // ✅ CONVERTIR FECHA AL FORMATO CORRECTO
                // ===================================
                string txnDateFormatted;
                try
                {
                    // Intentar parsear la fecha en varios formatos comunes
                    DateTime parsedDate;

                    if (DateTime.TryParseExact(request.TxnDate, "M/d/yyyy",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out parsedDate))
                    {
                        // Formato: 12/20/2024 o 1/5/2024
                        txnDateFormatted = parsedDate.ToString("yyyy-MM-dd");
                        _logger?.LogInformation("✅ Fecha parseada desde M/d/yyyy: {Original} → {Formatted}",
                            request.TxnDate, txnDateFormatted);
                    }
                    else if (DateTime.TryParseExact(request.TxnDate, "MM/dd/yyyy",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out parsedDate))
                    {
                        // Formato: 12/20/2024
                        txnDateFormatted = parsedDate.ToString("yyyy-MM-dd");
                        _logger?.LogInformation("✅ Fecha parseada desde MM/dd/yyyy: {Original} → {Formatted}",
                            request.TxnDate, txnDateFormatted);
                    }
                    else if (DateTime.TryParseExact(request.TxnDate, "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out parsedDate))
                    {
                        // Ya está en el formato correcto
                        txnDateFormatted = request.TxnDate;
                        _logger?.LogInformation("✅ Fecha ya en formato correcto: {Date}", txnDateFormatted);
                    }
                    else if (DateTime.TryParse(request.TxnDate, out parsedDate))
                    {
                        // Intento genérico
                        txnDateFormatted = parsedDate.ToString("yyyy-MM-dd");
                        _logger?.LogInformation("✅ Fecha parseada genéricamente: {Original} → {Formatted}",
                            request.TxnDate, txnDateFormatted);
                    }
                    else
                    {
                        _logger?.LogError("❌ No se pudo parsear la fecha: {Date}", request.TxnDate);
                        return Json(new
                        {
                            success = false,
                            message = $"Formato de fecha inválido: '{request.TxnDate}'. Use formato MM/dd/yyyy o yyyy-MM-dd"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "❌ Error parseando fecha: {Date}", request.TxnDate);
                    return Json(new
                    {
                        success = false,
                        message = $"Error parseando fecha: {ex.Message}"
                    });
                }

                // ===================================
                // PREPARAR PAYLOAD
                // ===================================
                var payload = new
                {
                    // Para CreditCardChargeAddRq
                    payeeFullName = request.Vendor,
                    accountRef = request.AccountFullName,
                    expenseAccountFullName = request.ExpenseAccount ?? "Uncategorized Expenses",
                    amount = request.Amount,
                    txnDate = txnDateFormatted,  // ✅ USAR LA FECHA FORMATEADA
                    memo = request.Memo ?? "",
                    refNumber = "",

                    // Campos adicionales para otros tipos
                    depositToAccountRef = request.AccountFullName,
                    vendor = request.Vendor,
                    tipoDocumento = request.TipoDocumento
                };

                var json = JsonConvert.SerializeObject(payload);
                _logger?.LogDebug("📦 Payload JSON: {Payload}", json);

                // ===================================
                // CONFIGURAR HTTPCLIENT
                // ===================================
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };

                using var httpClient = new HttpClient(handler, disposeHandler: true);
                httpClient.Timeout = TimeSpan.FromSeconds(120);
                httpClient.DefaultRequestHeaders.Connection.Clear();
                httpClient.DefaultRequestHeaders.ConnectionClose = false;

                // ===================================
                // DETERMINAR ENDPOINT CORRECTO
                // ===================================
                string baseUrl = "https://localhost:7059/api/quickbooks/";

                string endpoint = request.TipoDocumento switch
                {
                    "CreditCardCharge" => baseUrl + "charge",
                    "Deposit" => baseUrl + "deposit",
                    "Check" => baseUrl + "check",
                    "CreditCardCredit" => baseUrl + "registrar-credito",
                    _ => baseUrl + "charge"
                };

                _logger?.LogInformation("🌐 Endpoint seleccionado: {Endpoint}", endpoint);

                // ===================================
                // LÓGICA DE REINTENTOS
                // ===================================
                int maxRetries = 3;
                int retryDelayMs = 1000;
                string lastError = null;

                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        _logger?.LogInformation("🔄 Intento {Attempt}/{MaxRetries} llamando a {Endpoint}",
                            attempt, maxRetries, endpoint);

                        using var content = new StringContent(json, Encoding.UTF8, "application/json");
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

                        var response = await httpClient.PostAsync(endpoint, content, cts.Token);

                        // ===================================
                        // LEER RESPUESTA
                        // ===================================
                        string responseBody = string.Empty;
                        try
                        {
                            responseBody = await response.Content.ReadAsStringAsync();
                            _logger?.LogDebug("📥 Response body: {Body}",
                                responseBody?.Substring(0, Math.Min(500, responseBody?.Length ?? 0)));
                        }
                        catch (Exception readEx)
                        {
                            _logger?.LogWarning(readEx, "⚠️ No se pudo leer body de la respuesta");
                            lastError = $"Error leyendo respuesta: {readEx.Message}";

                            if (attempt == maxRetries)
                                throw;

                            await Task.Delay(retryDelayMs * attempt);
                            continue;
                        }

                        // ===================================
                        // VALIDAR STATUS CODE
                        // ===================================
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger?.LogError("❌ QuickBooks API error ({StatusCode}): {Body}",
                                response.StatusCode, responseBody);

                            lastError = $"HTTP {(int)response.StatusCode}: {responseBody}";

                            // Si es 4xx, no reintentar
                            if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                            {
                                return Json(new { success = false, message = lastError });
                            }

                            // Si es 5xx, reintentar
                            if (attempt < maxRetries)
                            {
                                _logger?.LogWarning("⏳ Error 5xx, reintentando en {Delay}ms...", retryDelayMs * attempt);
                                await Task.Delay(retryDelayMs * attempt);
                                continue;
                            }

                            return Json(new { success = false, message = lastError });
                        }

                        // ===================================
                        // PARSEAR RESPUESTA EXITOSA
                        // ===================================
                        dynamic qbResponse = null;
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(responseBody))
                            {
                                qbResponse = JsonConvert.DeserializeObject<dynamic>(responseBody);
                            }
                        }
                        catch (Exception parseEx)
                        {
                            _logger?.LogWarning(parseEx, "⚠️ Respuesta no JSON: {Body}",
                                responseBody?.Substring(0, Math.Min(200, responseBody?.Length ?? 0)));
                        }

                        var txnId = (string)(qbResponse?.txnId?.ToString() ??
                                            qbResponse?.txnID?.ToString() ??
                                            qbResponse?.TxnID?.ToString() ?? "");

                        _logger?.LogInformation("✅ Registro exitoso: TxnID={TxnId}, Tipo={Tipo}",
                            txnId, request.TipoDocumento);

                        return Json(new
                        {
                            success = true,
                            txnId = txnId,
                            tipoDocumento = request.TipoDocumento,
                            message = $"Registrado exitosamente como {request.TipoDocumento}"
                        });
                    }
                    catch (TaskCanceledException tcex) when (!tcex.CancellationToken.IsCancellationRequested)
                    {
                        _logger?.LogError(tcex, "⏱️ Timeout en intento {Attempt}/{MaxRetries}", attempt, maxRetries);
                        lastError = $"Timeout al llamar a QuickBooks API (intento {attempt}/{maxRetries})";

                        if (attempt < maxRetries)
                        {
                            _logger?.LogInformation("⏳ Reintentando en {Delay}ms...", retryDelayMs * attempt);
                            await Task.Delay(retryDelayMs * attempt);
                            continue;
                        }
                    }
                    catch (HttpRequestException httpEx)
                    {
                        _logger?.LogError(httpEx, "🌐 Error HTTP en intento {Attempt}/{MaxRetries}", attempt, maxRetries);
                        lastError = $"Error de conexión: {httpEx.Message}";

                        if (attempt < maxRetries)
                        {
                            _logger?.LogInformation("⏳ Reintentando en {Delay}ms...", retryDelayMs * attempt);
                            await Task.Delay(retryDelayMs * attempt);
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "💥 Error inesperado en intento {Attempt}/{MaxRetries}", attempt, maxRetries);
                        lastError = $"Error inesperado: {ex.Message}";

                        if (attempt < maxRetries)
                        {
                            await Task.Delay(retryDelayMs * attempt);
                            continue;
                        }
                    }
                }

                // ===================================
                // TODOS LOS INTENTOS FALLARON
                // ===================================
                _logger?.LogError("❌ Todos los intentos fallaron. Último error: {LastError}", lastError);

                return Json(new
                {
                    success = false,
                    message = $"No se pudo contactar al servicio QuickBooks después de {maxRetries} intentos. {lastError}"
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Error crítico en RegistrarEnQuickBooks");
                return Json(new { success = false, message = $"Error interno: {ex.Message}" });
            }
        }

             

        [HttpPost]
        public IActionResult UpdateTemporal(Guid batchId, List<Movimiento> movimientos)
        {
            if (movimientos == null)
                return BadRequest();

            _tempTables[batchId] = new TempBatch
            {
                Items = movimientos.Select(m => CloneMovimientoForTemp(m)).ToList(),
                BankId = _tempTables.TryGetValue(batchId, out var existing) ? existing.BankId : null,
                SelectedCuentaQB = _tempTables.TryGetValue(batchId, out var ex2) ? ex2.SelectedCuentaQB : null
            };

            return RedirectToAction(nameof(EditTemporal), new { batchId });
        }

        [HttpGet]
        public IActionResult ExportarTemporal(Guid batchId, string? filename = null)
        {
            if (!_tempTables.TryGetValue(batchId, out var batch) || batch.Items.Count == 0)
                return NotFound("Batch not found or empty");

            var bytes = BuildExcelBytes(batch.Items, out var fileName, filename);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

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

            var selected = dto.Selected.Trim().ToUpperInvariant();
            batch.Items[dto.Index].Empresa = selected;
            batch.Items[dto.Index].EmpresaExtraida = selected;

            try
            {
                var bankId = batch.BankId;
                AutoAddPattern(bankId, $"({Regex.Escape(selected)})", "User-selected: learned exact match");
            }
            catch
            {
            }

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

            if (!string.IsNullOrWhiteSpace(desiredFileName))
            {
                try
                {
                    var sanitized = desiredFileName;
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
            string text = (description ?? "").ToUpperInvariant();

            if (!string.IsNullOrWhiteSpace(cb.Naics))
            {
                var naics = cb.Naics;
                if (naics.StartsWith("722"))
                    return "Expenses:Restaurants";
                if (naics.StartsWith("447") || naics.StartsWith("324") || naics.Contains("FUEL"))
                    return "Expenses:Gasoline";
                if (naics.StartsWith("481") || naics.StartsWith("482") || naics.StartsWith("488"))
                    return "Expenses:Transportation";
                if (naics.StartsWith("5112") || naics.StartsWith("518") || naics.StartsWith("519"))
                    return "Expenses:Software";
            }

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

            return "Expenses:Uncategorized";
        }

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
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> StartProcesar(
    IFormFileCollection archivosPdf,
    int BancoId,
    string LastSelectedAccountQB,
    string EmpresaQBName)
        {
            try
            {
                // Validar que se enviaron archivos
                if (archivosPdf == null || archivosPdf.Count == 0)
                {
                    _logger?.LogWarning("❌ No se recibieron archivos PDF");
                    return Json(new { success = false, message = "No se seleccionaron archivos" });
                }

                var batchId = Guid.NewGuid().ToString();

                _logger?.LogInformation("📋 Creando batch {BatchId} con {FileCount} archivos",
                    batchId, archivosPdf.Count);
                // ✅ OBTENER EL NOMBRE DEL BANCO
                var bancoSeleccionado = await _context.Bank.FindAsync(BancoId);
                var bankName = bancoSeleccionado?.BankName ?? "";
                _logger?.LogInformation("📋 Creando batch {BatchId} para banco: {BankName}, empresa: {Empresa}",
    batchId, bankName, EmpresaQBName);
                _logger?.LogInformation("📋 Creando batch {BatchId} con {FileCount} archivos para banco: {BankName}",
                    batchId, archivosPdf.Count, bankName);
                // ✅ CREAR ENTRADA DE PROGRESO CON BANKNAME
                _batchProgress[batchId] = new BatchProgress
                {
                    BatchId = batchId,
                    TotalFiles = archivosPdf.Count,
                    ProcessedFiles = 0,
                    Status = "Guardando archivos...",
                    Done = false,
                    Percent = 0,
                    SelectedAccountQB = LastSelectedAccountQB ?? "",
                    BankName = bankName , // ← AGREGAR ESTA LÍNEA
                    EmpresaQBName = EmpresaQBName ?? ""  // ✅ AGREGAR ESTA LÍNEA
                };

                _logger?.LogInformation("📋 Batch {BatchId} - Cuenta QB: {CuentaQB}",
                    batchId, LastSelectedAccountQB);

                // ✅ GUARDAR ARCHIVOS FÍSICAMENTE ANTES DEL BACKGROUND TASK
                var tempPaths = new List<string>();

                try
                {
                    for (int i = 0; i < archivosPdf.Count; i++)
                    {
                        var archivoPdf = archivosPdf[i];  // ✅ Usar archivoPdf, no archivos
                        var tempPath = Path.GetTempFileName();

                        _logger?.LogDebug("💾 Guardando archivo {Index}/{Total}: {FileName}",
                            i + 1, archivosPdf.Count, archivoPdf.FileName);

                        using (var stream = new FileStream(tempPath, FileMode.Create))
                        {
                            await archivoPdf.CopyToAsync(stream);
                        }

                        tempPaths.Add(tempPath);

                        _logger?.LogInformation("✅ Archivo {Index}/{Total} guardado: {Path} ({Size} bytes)",
                            i + 1, archivosPdf.Count, tempPath, new FileInfo(tempPath).Length);
                    }

                    _logger?.LogInformation("✅ {Count} archivos guardados exitosamente, iniciando procesamiento",
                        tempPaths.Count);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "❌ Error guardando archivos temporales");

                    // Limpiar archivos que se hayan guardado
                    foreach (var path in tempPaths)
                    {
                        try { System.IO.File.Delete(path); } catch { }
                    }

                    return Json(new { success = false, message = "Error guardando archivos: " + ex.Message });
                }

                // ✅ INICIAR PROCESAMIENTO EN BACKGROUND CON LAS RUTAS GUARDADAS
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _logger?.LogInformation("▶️ Iniciando procesamiento en background para batch {BatchId}", batchId);

                        await ProcesarEnBackground(batchId, tempPaths, BancoId, LastSelectedAccountQB ?? "");

                        _logger?.LogInformation("✅ Procesamiento completado para batch {BatchId}", batchId);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "💥 Error en procesamiento background para batch {BatchId}", batchId);

                        if (_batchProgress.ContainsKey(batchId))
                        {
                            _batchProgress[batchId].Done = true;
                            _batchProgress[batchId].Status = "Error: " + ex.Message;
                            _batchProgress[batchId].Percent = 0;
                        }
                    }
                    finally
                    {
                        // ✅ LIMPIAR ARCHIVOS TEMPORALES
                        _logger?.LogInformation("🧹 Limpiando {Count} archivos temporales para batch {BatchId}",
                            tempPaths.Count, batchId);

                        foreach (var path in tempPaths)
                        {
                            try
                            {
                                if (System.IO.File.Exists(path))
                                {
                                    System.IO.File.Delete(path);
                                    _logger?.LogDebug("🗑️ Eliminado: {Path}", path);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning(ex, "⚠️ No se pudo eliminar archivo temporal: {Path}", path);
                            }
                        }
                    }
                });

                _logger?.LogInformation("✅ Retornando batchId al cliente: {BatchId}", batchId);

                return Json(new { success = true, batchId = batchId });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Error general en StartProcesar");
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }



        private async Task ProcesarEnBackground(
            string batchId,
            List<string> archivosPaths,
            int bancoId,
            string selectedAccountQB)
        {
            if (!_batchProgress.TryGetValue(batchId, out var progress))
            {
                _logger?.LogError("❌ Batch {BatchId} no encontrado en _batchProgress", batchId);
                return;
            }

            // ✅ CREAR UN SCOPE NUEVO PARA EL BACKGROUND TASK
            // Esto evita el ObjectDisposedException
            using var scope = _scopeFactory.CreateScope();
            var scopedContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var scopedClearbit = scope.ServiceProvider.GetRequiredService<IClearbitService>();

            try
            {
                _logger?.LogInformation("🚀 Iniciando ProcesarEnBackground para batch {BatchId}", batchId);

                progress.Status = "Procesando archivos PDF...";
                progress.TotalFiles = archivosPaths.Count;
                progress.Percent = 5;

                var allMovimientos = new List<Movimiento>();

                // ✅ USAR EL CONTEXTO DEL SCOPE, NO _context
                var bancoSeleccionado = await scopedContext.Bank.FindAsync(bancoId);

                if (bancoSeleccionado == null)
                {
                    _logger?.LogWarning("⚠️ Banco {BancoId} no encontrado", bancoId);
                }
                else
                {
                    _logger?.LogInformation("🏦 Banco seleccionado: {BankName} (ID: {BankId})",
                        bancoSeleccionado.BankName, bancoId);
                }

                decimal totalInterest = 0m;
                bool anyInterestFound = false;

                // ========================================
                // PASO 1: PARSEAR ARCHIVOS PDF
                // ========================================
                _logger?.LogInformation("📄 Parseando {Count} archivos PDF...", archivosPaths.Count);

                for (int i = 0; i < archivosPaths.Count; i++)
                {
                    var tempPath = archivosPaths[i];

                    progress.Status = $"Parseando archivo {i + 1} / {archivosPaths.Count}...";
                    progress.ProcessedFiles = i;
                    progress.Percent = 5 + (int)((i / (double)archivosPaths.Count) * 25);

                    try
                    {
                        if (!System.IO.File.Exists(tempPath))
                        {
                            _logger?.LogWarning("⚠️ Archivo temporal no encontrado: {Path}", tempPath);
                            continue;
                        }

                        var fileInfo = new FileInfo(tempPath);
                        _logger?.LogInformation("📄 Parseando archivo {Index}/{Total}: {Path} ({Size} bytes)",
                            i + 1, archivosPaths.Count, tempPath, fileInfo.Length);

                        // ✅ PARSEAR PDF
                        var movimientos = AmexParser.ParsePdf(tempPath, bancoSeleccionado) ?? new List<Movimiento>();

                        _logger?.LogInformation("✅ Archivo {Index}/{Total}: {Count} movimientos extraídos",
                            i + 1, archivosPaths.Count, movimientos.Count);

                        // Capturar Interest Charged
                        if (!anyInterestFound)
                        {
                            if (AmexParser.LastInterestCharged.HasValue)
                            {
                                totalInterest = AmexParser.LastInterestCharged.Value;
                                anyInterestFound = true;
                                _logger?.LogInformation("💰 LastInterestCharged capturado: {Amount:C}", totalInterest);
                            }
                            else if (AmexParser.PaymentsCredits.HasValue)
                            {
                                totalInterest = AmexParser.PaymentsCredits.Value;
                                anyInterestFound = true;
                                _logger?.LogInformation("💰 PaymentsCredits capturado: {Amount:C}", totalInterest);
                            }
                            else if (AmexParser.BalanceAnterior.HasValue)
                            {
                                totalInterest = AmexParser.BalanceAnterior.Value;
                                anyInterestFound = true;
                                _logger?.LogInformation("💰 BalanceAnterior capturado: {Amount:C}", totalInterest);
                            }
                            else if (AmexParser.SaldoAnteriorFees.HasValue)
                            {
                                totalInterest = AmexParser.SaldoAnteriorFees.Value;
                                anyInterestFound = true;
                                _logger?.LogInformation("💰 SaldoAnteriorFees capturado: {Amount:C}", totalInterest);
                            }
                        }

                        allMovimientos.AddRange(movimientos);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "❌ Error parseando archivo {Index}/{Total}: {Path}",
                            i + 1, archivosPaths.Count, tempPath);
                    }

                    progress.ProcessedFiles = i + 1;
                    progress.Percent = 5 + (int)(((i + 1) / (double)archivosPaths.Count) * 25);
                    progress.Status = $"Parseado {i + 1}/{archivosPaths.Count} archivos";

                    if ((i + 1) % 10 == 0 || i == archivosPaths.Count - 1)
                    {
                        _logger?.LogInformation("⏳ Progreso: {Processed}/{Total} archivos ({Percent}%)",
                            i + 1, archivosPaths.Count, progress.Percent);
                    }
                }

                _logger?.LogInformation("📊 Total de movimientos extraídos: {Count}", allMovimientos.Count);

                if (allMovimientos.Count == 0)
                {
                    _logger?.LogWarning("⚠️ No se extrajeron movimientos");
                    progress.Done = true;
                    progress.Status = "Advertencia: No se encontraron movimientos en los archivos";
                    progress.Percent = 100;
                    progress.Movimientos = new List<Movimiento>();
                    progress.SelectedAccountQB = selectedAccountQB;
                    return;
                }

                // ========================================
                // PASO 2: ENRIQUECER CON CLEARBIT Y PREDECIR
                // ========================================
                progress.Status = "Enriqueciendo datos...";
                progress.TotalMovements = allMovimientos.Count;
                progress.ProcessedMovements = 0;
                progress.Percent = 30;

                _logger?.LogInformation("🔍 Iniciando enriquecimiento de {Count} movimientos...", allMovimientos.Count);

                // ✅ USAR SCOPED CONTEXT
                var empresasReconocidas = await scopedContext.EmpresasReconocidas
                    .Where(e => !string.IsNullOrWhiteSpace(e.TextoOriginal))
                    .Select(e => e.TextoOriginal!)
                    .ToListAsync();

                _logger?.LogInformation("📋 Empresas reconocidas en BD: {Count}", empresasReconocidas.Count);

                var clearbitCache = new ConcurrentDictionary<string, ReadingPdf.Services.ClearbitResult?>();

                for (int mi = 0; mi < allMovimientos.Count; mi++)
                {
                    var mov = allMovimientos[mi];

                    try
                    {
                        // Detectar empresa
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
                        else if (string.IsNullOrWhiteSpace(mov.Empresa))
                        {
                            var extracted = ExtractNameFromMemo(mov.Descripcion, bancoId);
                            if (!string.IsNullOrWhiteSpace(extracted))
                                mov.Empresa = extracted.ToUpperInvariant();
                        }

                        // Enriquecimiento Clearbit
                        if (!string.IsNullOrWhiteSpace(mov.Empresa))
                        {
                            var key = mov.Empresa.Trim().ToUpperInvariant();

                            if (!clearbitCache.TryGetValue(key, out var cbResult))
                            // ✅ USAR SCOPED CLEARBIT SERVICE
                            {
                                var domain = await scopedClearbit.FindDomainByNameAsync(mov.Empresa);

                                if (!string.IsNullOrWhiteSpace(domain))
                                    cbResult = await scopedClearbit.EnrichByDomainAsync(domain);
                                else
                                    cbResult = await scopedClearbit.EnrichByDomainAsync(mov.Empresa);

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

                        // Predicción de cuenta vía API
                        try
                        {
                            var (predictedAccount, confidence) = await PredictAccountViaApi(
                                mov.Descripcion ?? "",
                                mov.Empresa ?? ""
                            );

                            if (!string.IsNullOrWhiteSpace(predictedAccount))
                            {
                                mov.CuentaPredicha = predictedAccount;
                                mov.ScorePrediccion = confidence;
                            }
                            else
                            {
                                if (string.IsNullOrWhiteSpace(mov.CuentaPredicha))
                                    mov.CuentaPredicha = "SIN PREDICCION";
                                mov.ScorePrediccion = 0;
                            }
                        }
                        catch (Exception exPred)
                        {
                            _logger?.LogWarning(exPred, "⚠️ Predicción falló para movimiento: {Desc}",
                                mov.Descripcion?.Substring(0, Math.Min(50, mov.Descripcion?.Length ?? 0)));

                            if (string.IsNullOrWhiteSpace(mov.CuentaPredicha))
                                mov.CuentaPredicha = "SIN PREDICCION";
                            mov.ScorePrediccion = 0;
                        }

                        // Calcular débito/crédito
                        CalcularDebitoCredito(mov);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "❌ Error procesando movimiento {Index}/{Total}",
                            mi + 1, allMovimientos.Count);
                        mov.CuentaPredicha = mov.CuentaPredicha ?? "ERROR";
                        mov.ScorePrediccion = 0;
                    }

                    progress.ProcessedMovements = mi + 1;
                    progress.Percent = 30 + (int)(((mi + 1) / (double)allMovimientos.Count) * 70);
                    progress.Status = $"Procesado {mi + 1}/{allMovimientos.Count} movimientos";

                    if ((mi + 1) % 10 == 0 || mi == allMovimientos.Count - 1)
                    {
                        _logger?.LogInformation("⏳ Progreso: {Processed}/{Total} movimientos ({Percent}%)",
                            mi + 1, allMovimientos.Count, progress.Percent);
                    }
                }

                // ========================================
                // PASO 3: GUARDAR RESULTADOS
                // ========================================
                progress.Movimientos = allMovimientos;
                progress.InterestCharged = anyInterestFound ? totalInterest : (decimal?)null;  // ✅ AGREGAR ESTO
                progress.Done = true;
                progress.Status = "Completado exitosamente";
                progress.SelectedAccountQB = selectedAccountQB;
                progress.Percent = 100;

                _logger?.LogInformation("✅ Batch {BatchId} completado exitosamente:", batchId);
                _logger?.LogInformation("   📊 Total movimientos: {Count}", allMovimientos.Count);
                _logger?.LogInformation("   💰 Interest capturado: {Interest:C}", totalInterest);
                _logger?.LogInformation("   🏦 Cuenta QB: {CuentaQB}", selectedAccountQB);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "💥 Error fatal en procesamiento background para batch {BatchId}", batchId);

                progress.Done = true;
                progress.Status = "Error: " + ex.Message;
                progress.Percent = 0;
                progress.Movimientos = new List<Movimiento>();
            }
        }

        [HttpGet]
        public IActionResult GetProgress(string batchId)
        {
            try
            {
                _logger?.LogInformation("📊 GetProgress: batchId={BatchId}, Total batches={Count}",
                    batchId, _batchProgress.Count);

                if (string.IsNullOrWhiteSpace(batchId))
                {
                    return BadRequest(new { error = "batchId requerido" });
                }

                if (!_batchProgress.TryGetValue(batchId, out var progress))
                {
                    _logger?.LogWarning("❌ Batch NO encontrado: {BatchId}", batchId);
                    _logger?.LogWarning("   IDs disponibles: {Ids}",
                        string.Join(", ", _batchProgress.Keys.Take(5)));
                    return NotFound(new { error = "Batch no encontrado" });
                }

                _logger?.LogInformation("✅ Batch encontrado: Percent={Percent}%, Done={Done}, Status={Status}",
                    progress.Percent, progress.Done, progress.Status);

                return Json(new
                {
                    percent = progress.Percent,
                    status = progress.Status,
                    done = progress.Done,
                    processedFiles = progress.ProcessedFiles,
                    totalFiles = progress.TotalFiles,
                    processedMovements = progress.ProcessedMovements,
                    totalMovements = progress.TotalMovements
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "💥 Error en GetProgress");
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpGet]
        public IActionResult ExportCsv(string batchId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(batchId))
                {
                    return BadRequest("BatchId requerido");
                }

                if (!_batchProgress.TryGetValue(batchId, out var progress))
                {
                    return NotFound("Batch no encontrado");
                }

                var movimientos = progress.Movimientos ?? new List<Movimiento>();

                if (movimientos.Count == 0)
                {
                    return NotFound("No hay movimientos para exportar");
                }

                var csv = new StringBuilder();

                // Header
                csv.AppendLine("Fecha,Empresa,Descripcion,Debito,Credito,Cuenta,TipoDocumento");

                // Datos
                foreach (var mov in movimientos)
                {
                    // Use computed Debito/Credito fields (works when statement values are all positive)
                    var debitValue = mov.Debito;
                    var creditValue = mov.Credito;

                    // Normalizar cuenta (quitar prefijo "Expense:" si existe)
                    var cuenta = mov.CuentaContableAplicada ?? mov.CuentaPredicha ?? "";
                    if (cuenta.StartsWith("Expense:", StringComparison.OrdinalIgnoreCase))
                    {
                        cuenta = cuenta.Substring("Expense:".Length).Trim();
                    }

                    // Formatear descripción (agregar comillas y escapar comillas internas)
                    var descripcion = mov.Descripcion ?? "";
                    if (descripcion.Contains(",") || descripcion.Contains("\"") || descripcion.Contains("\n"))
                    {
                        descripcion = "\"" + descripcion.Replace("\"", "\"\"") + "\"";
                    }

                    // Formatear empresa (agregar comillas si contiene comas)
                    var empresa = mov.Empresa ?? "";
                    if (empresa.Contains(",") || empresa.Contains("\""))
                    {
                        empresa = "\"" + empresa.Replace("\"", "\"\"") + "\"";
                    }

                    // Construir línea
                    var line = string.Join(",", new[]
                    {
                mov.Fecha.ToString("M/d/yyyy"),                          // Fecha
                empresa,                                                  // Empresa
                descripcion,                                              // Descripcion
                debitValue > 0 ? debitValue.ToString("0.##") : "0",     // Debito
                creditValue > 0 ? creditValue.ToString("0.##") : "0",   // Credito
                cuenta,                                                   // Cuenta
                mov.TipoDocumento ?? ""                                  // TipoDocumento
            });

                    csv.AppendLine(line);
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                var fileName = $"Transacciones_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                _logger?.LogInformation("✅ Exportando {Count} movimientos en CSV para batch {BatchId}",
                    movimientos.Count, batchId);

                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Error exportando CSV para batch {BatchId}", batchId);
                return StatusCode(500, "Error generando el archivo");
            }
        }

        [HttpPost]
        public async Task<IActionResult> GuardarProceso([FromBody] GuardarProcesoRequest request)
        {
            try
            {
                _logger?.LogInformation("💾 Iniciando guardado de proceso para batch {BatchId}", request?.BatchId);

                if (request == null)
                    return Json(new { success = false, message = "Request body is required" });

                var errors = new List<string>();

                if (string.IsNullOrWhiteSpace(request.BatchId))
                    errors.Add("BatchId requerido.");

                if (request.Movimientos == null || request.Movimientos.Count == 0)
                    errors.Add("Se requiere al menos un movimiento en 'Movimientos'.");

                if (!string.IsNullOrWhiteSpace(request.CuentaQB) && request.CuentaQB.Length > 200)
                    errors.Add("CuentaQB excede la longitud máxima de 200 caracteres.");

                if (request.Movimientos != null)
                {
                    for (int i = 0; i < request.Movimientos.Count; i++)
                    {
                        var mov = request.Movimientos[i];
                        if (mov == null)
                        {
                            errors.Add($"Movimiento[{i}]: objeto nulo.");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(mov.Fecha) || !DateTime.TryParse(mov.Fecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                        {
                            errors.Add($"Movimiento[{i}]: Fecha inválida o no parseable ('{mov.Fecha}').");
                        }

                        if (!string.IsNullOrWhiteSpace(mov.Empresa) && mov.Empresa.Length > 200)
                            errors.Add($"Movimiento[{i}]: Empresa excede 200 caracteres.");

                        if (!string.IsNullOrWhiteSpace(mov.Descripcion) && mov.Descripcion.Length > 500)
                            errors.Add($"Movimiento[{i}]: Descripcion excede 500 caracteres.");

                        if (!string.IsNullOrWhiteSpace(mov.CuentaPredicha) && mov.CuentaPredicha.Length > 200)
                            errors.Add($"Movimiento[{i}]: CuentaPredicha excede 200 caracteres.");

                        if (!string.IsNullOrWhiteSpace(mov.TipoDocumento) && mov.TipoDocumento.Length > 100)
                            errors.Add($"Movimiento[{i}]: TipoDocumento excede 100 caracteres.");

                        if (mov.Monto < -1000000000m || mov.Monto > 1000000000m)
                            errors.Add($"Movimiento[{i}]: Monto fuera de rango razonable ('{mov.Monto}').");

                        if (mov.ScorePrediccion < 0 || mov.ScorePrediccion > 100)
                            errors.Add($"Movimiento[{i}]: ScorePrediccion debe estar entre 0 y 100.");
                    }
                }

                if (errors.Count > 0)
                {
                    _logger?.LogWarning("❌ GuardarProceso: validation failed for batch {BatchId} with {ErrorCount} errors", request.BatchId, errors.Count);
                    return Json(new { success = false, message = "Validation failed", errors = errors });
                }

                if (!Guid.TryParse(request.BatchId, out var procesoGuid))
                {
                    _logger?.LogWarning("❌ GuardarProceso: BatchId no es un GUID válido: {BatchId}", request.BatchId);
                    return Json(new { success = false, message = "BatchId inválido" });
                }

                // Clean / limit company name for the generated process name
                var empresaNombre = request.EmpresaNombre ?? "Empresa";
                empresaNombre = System.Text.RegularExpressions.Regex.Replace(empresaNombre, @"[^\w\s-]", "").Trim();
                if (empresaNombre.Length > 30)
                    empresaNombre = empresaNombre.Substring(0, 30);

                var bancoNombre = request.Banco ?? "AMEX";
                var fechaFormateada = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var nombreProcesoGenerado = $"{empresaNombre}-{bancoNombre}-{fechaFormateada}";

                // Try to find an existing proceso and update it instead of creating a new one.
                var existingProceso = await _context.Procesos
                    .Include(p => p.Movimientos)
                    .FirstOrDefaultAsync(p => p.Id == procesoGuid);

                if (existingProceso != null)
                {
                    // Update relevant fields but DO NOT create a new Proceso row.
                    _logger?.LogInformation("🔁 Actualizando proceso existente {ProcesoId} en lugar de crear uno nuevo", existingProceso.Id);

                    existingProceso.Banco = request.Banco ?? existingProceso.Banco;
                    existingProceso.CuentaQBSeleccionada = request.CuentaQB ?? existingProceso.CuentaQBSeleccionada;
                    existingProceso.SaldoInicial = request.SaldoInicial ?? existingProceso.SaldoInicial;
                    // Optionally update the display name if caller provided EmpresaNombre (keeps backwards compatibility).
                    if (!string.IsNullOrWhiteSpace(request.EmpresaNombre))
                        existingProceso.Nombre = nombreProcesoGenerado;

                    existingProceso.FechaCreacion = existingProceso.FechaCreacion == default ? DateTime.UtcNow : existingProceso.FechaCreacion;
                    existingProceso.Estado = existingProceso.Estado ?? "Abierto";

                    // Remove existing MovimientoProceso rows for this proceso to replace with the new snapshot.
                    var toRemove = _context.MovimientosProceso.Where(m => m.ProcesoId == existingProceso.Id);
                    _context.MovimientosProceso.RemoveRange(toRemove);

                    // Add new MovimientoProceso snapshot rows
                    foreach (var mov in request.Movimientos ?? new List<MovimientoDto>())
                    {
                        var fechaParsed = ParseFecha(mov.Fecha);

                        var movimientoProceso = new MovimientoProceso
                        {
                            ProcesoId = existingProceso.Id,
                            Fecha = fechaParsed,
                            Empresa = mov.Empresa ?? "",
                            EmpresaOriginal = mov.Empresa ?? "",
                            Descripcion = mov.Descripcion ?? "",
                            Monto = mov.Monto,
                            CuentaPredicha = mov.CuentaPredicha ?? "",
                            CuentaAplicada = !string.IsNullOrWhiteSpace(mov.CuentaAplicada) ? mov.CuentaAplicada : (mov.CuentaPredicha ?? ""),
                            ScorePrediccion = mov.ScorePrediccion,
                            TipoDocumento = mov.TipoDocumento ?? "",
                            RegistradoQB = !string.IsNullOrWhiteSpace(mov.TxnIdQuickBooks),
                            QuickBooksTxnId = mov.TxnIdQuickBooks ?? null
                        };

                        _context.MovimientosProceso.Add(movimientoProceso);
                    }

                    await _context.SaveChangesAsync();

                    _logger?.LogInformation("✅ Proceso actualizado exitosamente: {ProcesoId} con {Count} movimientos",
                        existingProceso.Id, request.Movimientos?.Count ?? 0);

                    return Json(new { success = true, procesoId = existingProceso.Id.ToString(), updated = true });
                }
                else
                {
                    // Create new proceso (original behavior)
                    var newProceso = new Proceso
                    {
                        Id = procesoGuid,
                        Nombre = nombreProcesoGenerado,
                        Banco = request.Banco ?? "AMEX",
                        CuentaQBSeleccionada = request.CuentaQB ?? "",
                        SaldoInicial = request.SaldoInicial ?? 0m,
                        FechaCreacion = DateTime.UtcNow,
                        Estado = "Abierto"
                    };

                    _context.Procesos.Add(newProceso);

                    foreach (var mov in request.Movimientos ?? new List<MovimientoDto>())
                    {
                        var fechaParsed = ParseFecha(mov.Fecha);

                        var movimientoProceso = new MovimientoProceso
                        {
                            ProcesoId = newProceso.Id,
                            Fecha = fechaParsed,
                            Empresa = mov.Empresa ?? "",
                            EmpresaOriginal = mov.Empresa ?? "",
                            Descripcion = mov.Descripcion ?? "",
                            Monto = mov.Monto,
                            CuentaPredicha = mov.CuentaPredicha ?? "",
                            CuentaAplicada = !string.IsNullOrWhiteSpace(mov.CuentaAplicada) ? mov.CuentaAplicada : (mov.CuentaPredicha ?? ""),
                            ScorePrediccion = mov.ScorePrediccion,
                            TipoDocumento = mov.TipoDocumento ?? "",
                            RegistradoQB = !string.IsNullOrWhiteSpace(mov.TxnIdQuickBooks),
                            QuickBooksTxnId = mov.TxnIdQuickBooks ?? null
                        };

                        _context.MovimientosProceso.Add(movimientoProceso);
                    }

                    await _context.SaveChangesAsync();

                    _logger?.LogInformation("✅ Proceso guardado exitosamente: {ProcesoId} con {Count} movimientos",
                        newProceso.Id, request.Movimientos?.Count ?? 0);

                    return Json(new { success = true, procesoId = newProceso.Id.ToString(), updated = false });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Error guardando proceso");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Método auxiliar para parsear fechas
        private DateTime ParseFecha(string fechaStr)
        {
            try
            {
                return DateTime.Parse(fechaStr);
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }

        // DTO para el request
        public class GuardarProcesoRequest
        {
            public string BatchId { get; set; } = "";

            public string? Banco { get; set; }
            public string? CuentaQB { get; set; }
            public decimal? SaldoInicial { get; set; }
            public List<MovimientoDto>? Movimientos { get; set; }
            // ✅ AGREGAR ESTA LÍNEA:
            public string? EmpresaNombre { get; set; }  // ← Nombre de la empresa QB
        }

        public class MovimientoDto
        {
            public string Fecha { get; set; } = "";
            public string? Empresa { get; set; }
            public string? Descripcion { get; set; }
            public decimal Monto { get; set; }
            public decimal Debito { get; set; }
            public decimal Credito { get; set; }
            public string? CuentaPredicha { get; set; }
            public string? CuentaAplicada { get; set; }
            public string? TipoDocumento { get; set; }
            public string? TxnIdQuickBooks { get; set; }
            public int ScorePrediccion { get; set; }
        }

    }
}
