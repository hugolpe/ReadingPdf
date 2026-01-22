using ReadingPdf.Models;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Text.RegularExpressions;
using Tesseract;
using UglyToad.PdfPig;
using System;
using System.Globalization;
using System.IO;

namespace ReadingPdf.Parsers
{
    public static class AmexParser
    {
        // Public static property to expose the extracted Previous Balance for UI consumption.
        // After calling `ParsePdf(...)`, read `AmexParser.LastPreviousBalance` to show the value where you display the transaction count.
        public static decimal? LastPreviousBalance { get; private set; }

        // New: expose Interest Charged when present
        public static decimal? LastInterestCharged { get; private set; }
        public static decimal? BalanceAnterior { get; private set; }

        public static decimal? SaldoAnteriorFees { get; private set; }

        public static decimal? PaymentsCredits { get; private set; }

        // Flag para invertir clasificación cuando el bank detectado es Amex
        private static bool _invertAmexClassification = false;

        private static Regex GetRegexForEntidad(Bank banco)//|Paso 3
        {
            switch (banco.DateFormat)
            {
                case "MM/DD/YY" when banco.BankId != 7:
                    return new Regex(
    @"^(?<Transaccion>\d{2}/\d{2}/\d{2}\*?.*?(?:-?\$?[\d,]*\.\d{2}-?)(?:\s*⧫)?)",
    RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);




                //case "MM/DD/YY" when banco.BankId != 7:
                //    return new Regex(
                //        @"^(?<Transaccion>\d{2}/\d{2}/\d{2}\*?\s+.+?\s+-?\$?\d{1,3}(?:,\d{3})*\.\d{2})(?:\s*⧫)?$",
                //        RegexOptions.Multiline | RegexOptions.Compiled
                //    );


                case "MM/DD/YY" when banco.BankId == 7:
                    // Same anchoring for BankId == 7 variant
                    return new Regex(
                    @"^(?<Transaccion>\d{2}/\d{2}/\d{2}\*?.*?(?:\$?-?\d{1,3}(?:,\d{3})*\.\d{2})(?:\s*⧫)?)",
                    RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);
                case "MM/DD/YYYY" when banco.BankId != 9:
                    return new Regex(
                    @"(?<FechaInicio>\d{2}/\d{2}/\d{4})\s+(?<Descripcion>.*?)\s*(?<FechaFin>\d{2}/\d{2}/\d{4})\s+(?<Monto>[\d,]+\.\d{2})\s+(?<Saldo>[\d,]+\.\d{2})?",
                    RegexOptions.Compiled);
                case "MM/DD/YYYY" when banco.BankId == 9:
                    return new Regex(
@"(?s)(?<Transaccion2>
(?<Monto2>\d{1,3}(?:,\d{3})*\.\d{2})?

    \s*\d{2}/\d{2}/\d{4}                   # Fecha inicial
    .*?                                   # Descripción (multilínea posible)
    \d{2}/\d{2}/\d{4}\s+                  # Segunda fecha
    (?<Monto>-?\$?[\d,]*\.?\d{2})         # Monto de la transacción
)",
RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
                case "MM/DD" when banco.BankId != 3 && banco.BankId != 4:
                    // Anchor to line start for two-date style transactions too.
                    return new Regex(
                    @"^(?<Transaccion1>\d{2}/\d{2}\s+\d{2}/\d{2}\s+[\w\s\*&,.'\-]+?(?:\s+[A-Z0-9]+)*\s+-?\$?[\d,]*\.?\d{2})",
                    RegexOptions.Compiled | RegexOptions.Multiline);
                case "MM/DD" when banco.BankId == 3:
                    // Anchor to line start for BankId 3 variant
                    return new Regex(
                    @"^(?<Transaccion1>\d{2}/\d{2}\s+[A-Z0-9&\-\./' ]+?\s+-?\$?[\d,]*\.\d{2}\s+-?\$?[\d,]*\.\d{+2}\r?\n.+)",
                    RegexOptions.Compiled | RegexOptions.Multiline);
                case "MM/DD" when banco.BankId == 4:
                    return new Regex(
                        @"(?m)(?<Transaccion1>^\d{2}/\d{2}\b[\s\S]*?-?\$?\d{1,3}(?:,\d{3})*\.\d{2})",
                        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);

                //case "MM/DD" when banco.BankId == 4:
                //    return new Regex(
                //        @"(?ms)(?<Transaccion1>" +
                //        @"^\d{2}/\d{2}\s+.*?" +                     // 01/08 ...
                //        @"(?:\r?\n(?!\d{2}/\d{2}\s).*)*?" +         // líneas internas
                //        @"\$?-?\d{1,3}(?:,\d{3})*\.\d{2}" +         // monto
                //        @")(?=(?:\r?\n\s*\r?\n)*\d{2}/\d{2}\s|$)",  // 👈 PERMITE LÍNEAS VACÍAS
                //        RegexOptions.Compiled
                //    );

                default:
                    throw new NotSupportedException($"Formato de fecha no soportado: {banco.DateFormat}");
            }
        }

        private static readonly Regex regexFecha = new Regex(@"\d{2}/\d{2}/\d{2}\*?");

        private static readonly Regex regexFecha1 = new Regex(@"\d{2}/\d{2}\*?");

        private static readonly Regex regexFecha2 = new Regex(@"\d{2}/\d{2}/\d{4}\*?");

        // capture amounts INCLUDING optional leading minus sign
        private static readonly Regex regexMontos2 = new Regex(@"\$?(?<Montox1>-?\d{1,3}(?:,\d{3})*\.\d{2})(?:.*?\$?(?<Montox2>-?\d{1,3}(?:,\d{3})*\.\d{2}))?",
            RegexOptions.Singleline);

        private static readonly Regex regexSecondLine = new Regex(@"\r?\n");

        // capture sign inside the named group
        private static readonly Regex regexMonto = new Regex(@"(?<Monto>-?\$?[\d,]+\.\d{2})");
        private static readonly Regex regexMonto2 = new Regex(@"\$?(?<Monto2>-?\d{1,3}(?:,\d{3})*\.\d{2})");
        private static readonly Regex regexMonto3 = new Regex(@"\$?(?<Monto3>-?\d{1,3}(?:,\d{3})*\.\d{2})?");

        private static readonly Regex regexPrimeraLinea = new Regex(@"^(.+)$", RegexOptions.Multiline);

        private static readonly Regex regexPrimerParrafo =
            new Regex(@"^([\s\S]*?)(?:\r?\n\r?\n|$)");





        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        private static readonly Regex regexPreviousBalance = new Regex(
            @"Previous\s+Balance[:\s]*\(?\$?(?<PreviousBalance>-?\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline,
            RegexTimeout
        );

        // New regex: busca la palabra "Interest Charged" y captura el monto asociado
        private static readonly Regex regexInterestCharged = new Regex(
            @"Interest\s+Charged[:\s]*\(?\$?(?<Interest>-?\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline,
            RegexTimeout
        );
        private static readonly Regex regexFees = new Regex(
    @"Fees\s[:\s]*\(?\$?(?<Interest>-?\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\)?",
    RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline,
    RegexTimeout
);

        private static readonly Regex regexPaymentsCredits = new Regex(
    @"Payments(?:[\s\/&\-]+)Credits[:\s]*\(?\$?(?<PaymentsCredits>-?\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\)?",
    RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline,
    RegexTimeout
);

        // === CONTEXTO CRÉDITO (GENERAL, incluye Amex/Chase heuristics) ===
        private static readonly Regex regexCreditoContexto =
            new Regex(
                @"(?i)(" +
                @"\b(payment received|payment(?! to)|payment -|payment thank|payment by|payment received|credit memo|credit\b|cr\b|refund|returned|return|deposit|adjustment|payment posted)\b" +
                @")",
                RegexOptions.Compiled | RegexOptions.Singleline
            );

        // === CONTEXTO DÉBITO (GENERAL, incluye Amex/Chase heuristics) ===
        private static readonly Regex regexDebitoContexto =
            new Regex(
                @"(?i)(" +
                @"\b(card purchase|purchase|pos|charge|charged|authorization|merchant|atm|withdrawal|fee\b|fee:|transaction fee|online transfer to|payment to|zelle payment to|debited|purchase authorized|chargeback)\b" +
                @")",
                RegexOptions.Compiled | RegexOptions.Singleline
            );


        // New regex: busca la palabra "Interest Charged" y captura el monto asociado
        //private static readonly Regex regexBeginningBalance = new Regex(
        //    @"Beginning\s+balance[:\s]*\(?\$?(?<SaldoAnt>-?\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\)?",
        //    RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline
        //);
        private static readonly Regex regexBeginningBalance = new Regex(
    @"Beginning\s+balance(?:\s+on\s+[A-Za-z]+\s+\d{1,2},\s+\d{4})?\s*\(?\$?(?<SaldoAnt>-?\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\)?",
    RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline,
    RegexTimeout
);


        public static List<Movimiento> ParsePdf(string pdfPath, Bank banco)
        {
            string rawText = ExtractText(pdfPath);

            // Extract and expose the Previous Balance for UI
            LastPreviousBalance = ExtractPreviousBalance(rawText);
            // Extract and expose Interest Charged when present
            LastInterestCharged = ExtractInterestCharged(rawText);
            BalanceAnterior = ExtractBalanceAnterior(rawText);
            SaldoAnteriorFees = ExtractSaldoAnteriorFees(rawText);
            PaymentsCredits = ExtractPaymentsCredits(rawText);


            Console.WriteLine("Entidad detectada: " + DetectarEntidad(rawText, banco.BankIdentifier));
            Console.WriteLine("Previous Balance detected: " + (LastPreviousBalance?.ToString("F2") ?? "null"));
            Console.WriteLine("Interest Charged detected: " + (LastInterestCharged?.ToString("F2") ?? "null"));
            Console.WriteLine("Beginning Balance detected: " + (BalanceAnterior?.ToString("F2") ?? "null"));
            Console.WriteLine("Beginning Balance detected: " + (PaymentsCredits?.ToString("F2") ?? "null"));

            // Obtener regex adecuado según la entidad
            Regex regexSeleccionado = GetRegexForEntidad(banco);

            // Determinar si debemos invertir clasificación para Amex
            _invertAmexClassification = IsAmex(banco);

            return ParseText(rawText, regexSeleccionado);
        }

        // New helper that returns both movements and previous balance in a simple DTO.
        // Added PaymentsCredits field to return the extracted amex3 value.
        public record ParseResult(List<Movimiento> Movimientos, decimal? PreviousBalance, decimal? InterestCharged, decimal? PaymentsCredits);

        // PSEUDOCODE / PLAN (detallado):
        // 1. Extraer texto del PDF usando ExtractText.
        // 2. Extraer 'PreviousBalance' (prev), 'SaldoAnteriorFees' (amex1), 'InterestCharged' (amex2) y 'PaymentsCredits' (amex3).
        // 3. Determinar 'interest' eligiendo el valor válido entre amex2, amex3 y amex1:
        //    - Prioridad: amex2 (InterestCharged)
        //    - Si amex2 es null, usar amex3 (PaymentsCredits) como posible valor
        //    - Si amex3 es null, usar amex1 (SaldoAnteriorFees)
        // 4. Obtener la expresión regular para la entidad y parsear movimientos con ParseText.
        // 5. Sincronizar propiedades estáticas LastPreviousBalance, LastInterestCharged y PaymentsCredits con los valores calculados.
        // 6. Devolver un ParseResult que contiene la lista de movimientos, el previous balance, el interest seleccionado y paymentsCredits.
        public static ParseResult ParsePdfWithSummary(string pdfPath, Bank banco)
        {
            string rawText = ExtractText(pdfPath);

            // Extraer valores posibles
            var prev = ExtractPreviousBalance(rawText);
            var amex1 = ExtractSaldoAnteriorFees(rawText);    // posible valor alternativo
            var amex2 = ExtractInterestCharged(rawText);      // valor preferido para "interest"
            var amex3 = ExtractPaymentsCredits(rawText);      // nuevo valor a considerar (Payments/Credits)

            // Elegir el valor válido para 'interest'
            // Prioridad: amex2 (InterestCharged) si existe, sino amex3 (PaymentsCredits), sino amex1 (SaldoAnteriorFees)
            decimal? interest = amex2 ?? amex3 ?? amex1;

            var regexSeleccionado = GetRegexForEntidad(banco);

            // Determinar si debemos invertir clasificación para Amex
            _invertAmexClassification = IsAmex(banco);

            var movs = ParseText(rawText, regexSeleccionado);

            // Mantener propiedades estáticas en sincronía
            LastPreviousBalance = prev;
            LastInterestCharged = interest;
            PaymentsCredits = amex3;

            return new ParseResult(movs, prev, interest, amex3);
        }

        public static string ExtractText(string pdfPath)
        {
            var sb = new StringBuilder();

            using (var pdf = PdfDocument.Open(pdfPath))
            {
                foreach (var page in pdf.GetPages())
                {
                    var text = UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(page);

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(sb.ToString()))
            {
                Console.WriteLine("⚠ No se encontró texto digital. Aplicando OCR...");

                using (var pdf = PdfiumViewer.PdfDocument.Load(pdfPath))
                {
                    using (var engine = new Tesseract.TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
                    {
                        engine.SetVariable("tessedit_char_whitelist", "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.,-/$ ");

                        for (int i = 0; i < pdf.PageCount; i++)
                        {
                            using (var img = pdf.Render(i, 300, 300, true))
                            {
                                using (var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format24bppRgb))
                                {
                                    using (var g = Graphics.FromImage(bmp))
                                    {
                                        g.Clear(Color.White);
                                        g.DrawImage(img, 0, 0);
                                    }

                                    for (int y = 0; y < bmp.Height; y++)
                                    {
                                        for (int x = 0; x < bmp.Width; x++)
                                        {
                                            Color c = bmp.GetPixel(x, y);
                                            int gray = (c.R + c.G + c.B) / 3;
                                            gray = gray > 128 ? 255 : 0;
                                            bmp.SetPixel(x, y, Color.FromArgb(gray, gray, gray));
                                        }
                                    }

                                    using (var pix = PixHelper.BitmapToPix(bmp))
                                    using (var page = engine.Process(pix, PageSegMode.Auto))
                                    {
                                        sb.AppendLine(page.GetText());
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return sb.ToString();
        }

        public static class PixHelper
        {
            public static Pix BitmapToPix(Bitmap bmp)
            {
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    return Pix.LoadFromMemory(ms.ToArray());
                }
            }
        }

        private static string DetectarEntidad(string rawText, string textoBanco)
        {
            var match1 = regexPrimerParrafo.Match(rawText);

            if (match1.Success)
            {
                string primeraLinea = match1.Groups[1].Value.Trim();

                Console.WriteLine("Línea detectada: " + primeraLinea);

                if (primeraLinea.Contains(textoBanco))
                    return textoBanco;
                else
                    return "Desconocido";
            }
            return "No detectado";
        }

        public static List<Movimiento> ParseText(string input, Regex regex)
        {
            var movimientos = new List<Movimiento>();
            var matches = regex.Matches(input);

            foreach (Match m in matches)
            {
                var mov = new Movimiento();

                if (m.Groups["Transaccion"].Success)
                {
                    string block = m.Groups["Transaccion"].Value.Trim();
                    mov.Notas = block;

                    var fechaMatch = regexFecha.Match(block);
                    if (fechaMatch.Success && DateTime.TryParse(fechaMatch.Value.Replace("*", ""), out var fecha))
                        mov.Fecha = fecha;

                    // Prefer the most relevant amount in the block (currency-labeled or last amount)
                    var chosen = ExtractPreferredAmountFromBlock(block);
                    if (chosen.HasValue)
                    {
                        mov.Monto = chosen.Value;
                    }
                    else
                    {
                        var montoMatch = regexMonto.Match(block);
                        if (montoMatch.Success)
                        {
                            var raw = montoMatch.Groups["Monto"].Value;
                            // normalize: remove $ and commas but preserve leading minus
                            raw = raw.Replace("$", "").Replace(",", "").Trim();
                            if (Decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.Number, CultureInfo.InvariantCulture, out var monto))
                                mov.Monto = monto;
                        }
                    }

                    string desc = block;
                    desc = regexFecha.Replace(desc, "").Trim();
                    desc = regexMonto.Replace(desc, "").Trim();
                    desc = desc.Replace("⧫", "").Trim();

                    mov.Descripcion = desc;
                    mov.Empresa = ExtraerEmpresa(desc);

                    // === CLASIFICACIÓN CRÉDITO / DÉBITO (AMEX/GENERAL) ===
                    mov.Tipo = DetectarTipoMovimiento(mov.Notas, mov.Descripcion);

                    //// Normalize sign according to detected type
                    //if (mov.Tipo == TipoMovimiento.Debito && mov.Monto > 0)
                    //{
                    //    mov.Monto *= -1;
                    //}

                }

                if (m.Groups["Transaccion1"].Success)
                {
                    string block = m.Groups["Transaccion1"].Value.Trim();
                    mov.Notas = block;

                    var fechaMatch = regexFecha1.Match(block);
                    if (fechaMatch.Success && DateTime.TryParse(fechaMatch.Value.Replace("*", ""), out var fecha))
                        mov.Fecha = fecha;

                    // Prefer the most relevant amount in the block (currency-labeled or last amount)
                    var chosen1 = ExtractPreferredAmountFromBlock(block);
                    if (chosen1.HasValue)
                    {
                        mov.Monto = chosen1.Value;
                    }
                    else
                    {
                        var montoMatch = regexMonto.Match(block);
                        if (montoMatch.Success)
                        {
                            var raw = montoMatch.Groups["Monto"].Value;
                            raw = raw.Replace("$", "").Replace(",", "").Trim();
                            if (Decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.Number, CultureInfo.InvariantCulture, out var monto))
                                mov.Monto = monto;
                        }
                    }



                    string desc = block;
                    desc = regexFecha1.Replace(desc, "").Trim();
                    desc = regexMonto.Replace(desc, "").Trim();
                    desc = desc.Replace("⧫", "").Trim();
                    mov.Descripcion = desc;
                    mov.Empresa = ExtraerEmpresa(desc);

                    // === CLASIFICACIÓN CRÉDITO / DÉBITO (CHASE) ===
                    mov.Tipo = DetectarTipoMovimiento(mov.Notas, mov.Descripcion);

                    //// Normalizar signo según tipo
                    //if (mov.Tipo == TipoMovimiento.Debito && mov.Monto > 0)
                    //{
                    //    mov.Monto *= -1;
                    //}


                }

                if (m.Groups["Transaccion2"].Success)
                {
                    string block = m.Groups["Transaccion2"].Value.Trim();
                    mov.Notas = block;

                    var fechaMatch = regexFecha2.Match(block);
                    if (fechaMatch.Success && DateTime.TryParse(fechaMatch.Value.Replace("*", ""), out var fecha))
                        mov.Fecha = fecha;

                    // Prefer the most relevant amount in the block (currency-labeled or last amount)
                    var chosen2 = ExtractPreferredAmountFromBlock(block);
                    if (chosen2.HasValue)
                    {
                        mov.Monto = chosen2.Value;
                    }
                    else
                    {
                        // fallback: previous logic (try several patterns)
                        var montoMatch = regexMonto.Match(block);
                        if (montoMatch.Success)
                        {
                            var raw = montoMatch.Groups["Monto"].Value;
                            raw = raw.Replace("$", "").Replace(",", "").Trim();
                            if (Decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.Number, CultureInfo.InvariantCulture, out var monto))
                                mov.Monto = monto;
                        }

                        var montoMatch1 = regexMonto3.Match(block);
                        if (montoMatch1.Success)
                        {
                            var raw = montoMatch1.Groups["Monto3"].Value;
                            raw = raw.Replace("$", "").Replace(",", "").Trim();
                            if (Decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.Number, CultureInfo.InvariantCulture, out var monto3))
                                mov.Monto = monto3;
                        }

                        var montoMatch2 = regexMontos2.Match(block);

                        if (montoMatch2.Success)
                        {
                            string monto1 = montoMatch2.Groups["Montox1"].Value;
                            string monto2 = montoMatch2.Groups["Montox2"].Value;

                            string montoFinal = monto1; // por defecto

                            if (!string.IsNullOrWhiteSpace(monto2))
                            {
                                int idxMonto1 = block.IndexOf(monto1);
                                if (idxMonto1 >= 0 && idxMonto1 + monto1.Length < block.Length)
                                {
                                    char nextChar = block[idxMonto1 + monto1.Length];

                                    if (char.IsWhiteSpace(nextChar))
                                    {
                                        montoFinal = monto2; // si hay salto de línea o espacio → usar segundo monto
                                    }
                                }
                            }

                            if (Decimal.TryParse(montoFinal.Replace("$", "").Replace(",", ""), NumberStyles.AllowLeadingSign | NumberStyles.Number, CultureInfo.InvariantCulture, out var montox))
                                mov.Monto = montox;
                        }
                    }

                    // === CLASIFICACIÓN CRÉDITO / DÉBITO (CHASE) ===

                    string desc = block;
                    desc = regexFecha2.Replace(desc, "").Trim();
                    desc = regexMonto2.Replace(desc, "").Trim();
                    desc = desc.Replace("⧫", "").Trim();
                    mov.Descripcion = desc;
                    mov.Empresa = ExtraerEmpresa(desc);
                    // === CLASIFICACIÓN CRÉDITO / DÉBITO (CHASE) ===
                    mov.Tipo = DetectarTipoMovimiento(mov.Notas, mov.Descripcion);

                    //// Normalizar signo según tipo
                    //if (mov.Tipo == TipoMovimiento.Debito && mov.Monto > 0)
                    //{
                    //    mov.Monto *= -1;
                    //}


                }

                if (!string.IsNullOrEmpty(mov.Descripcion))
                {
                    if (mov.Descripcion.Contains("RESTAURANT")) mov.Categoria = "RESTAURANT";
                    else if (mov.Descripcion.Contains("WHOLESALE CLUB")) mov.Categoria = "WHOLESALE CLUB";
                    else if (mov.Descripcion.Contains("DIGITAL GOODS")) mov.Categoria = "DIGITAL GOODS";
                    else if (mov.Descripcion.Contains("PASSENGER TICKET")) mov.TipoDocumento = "PASSENGER TICKET";

                    var ticketMatch = Regex.Match(mov.Descripcion, @"Ticket Number:\s*(?<Doc>\d+)");
                    if (ticketMatch.Success) mov.Documento = ticketMatch.Groups["Doc"].Value;

                    var passengerMatch = Regex.Match(mov.Descripcion, @"Passenger Name:\s*(?<Name>.+)");
                    if (passengerMatch.Success) mov.Pasajero = passengerMatch.Groups["Name"].Value.Trim();

                    var arr = Regex.Match(mov.Descripcion, @"Arrival Date\s*(\d{2}/\d{2}/\d{2})");
                    if (arr.Success && DateTime.TryParse(arr.Groups[1].Value, out var fechaIni)) mov.FechaInicio = fechaIni;

                    var dep = Regex.Match(mov.Descripcion, @"Departure Date\s*(\d{2}/\d{2}/\d{2})");
                    if (dep.Success && DateTime.TryParse(dep.Groups[1].Value, out var fechaFin)) mov.FechaFin = fechaFin;
                }

                movimientos.Add(mov);
            }

            return movimientos;
        }

        /// <summary>
        /// Extrae el valor de "Previous Balance" del texto proporcionado.
        /// Retorna null si no se encuentra o no se puede parsear.
        /// </summary>
        public static decimal? ExtractPreviousBalance(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var m = regexPreviousBalance.Match(text);
            if (!m.Success)
                return null;

            var raw = m.Groups["PreviousBalance"].Value.Trim();

            // Normalizar: remover símbolo de dólar y comas.
            // Convertir paréntesis negativos en signo menos si fuera necesario.
            if (raw.StartsWith("(") && raw.EndsWith(")"))
            {
                raw = "-" + raw.Substring(1, raw.Length - 2);
            }

            raw = raw.Replace("$", "").Replace(",", "").Trim();

            // Intentar parsear con InvariantCulture (punto decimal).
            if (decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                return value;

            // Fallback: intentar con la cultura actual.
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out value))
                return value;

            return null;
        }

        /// <summary>
        /// Extrae el valor de "Interest Charged" del texto proporcionado.
        /// Retorna null si no se encuentra o no se puede parsear.
        /// </summary>
        public static decimal? ExtractInterestCharged(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var m = regexInterestCharged.Match(text); if (!m.Success)
                if (!m.Success)
                    return null;



            var raw = m.Groups["Interest"].Value.Trim();

            // Normalizar paréntesis negativos
            if (raw.StartsWith("(") && raw.EndsWith(")"))
            {
                raw = "-" + raw.Substring(1, raw.Length - 2);
            }

            raw = raw.Replace("$", "").Replace(",", "").Trim();

            if (decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                return value;

            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out value))
                return value;

            return null;
        }

        public static decimal? ExtractSaldoAnteriorFees(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var m = regexFees.Match(text); if (!m.Success)
                if (!m.Success)
                    return null;


            var raw = m.Groups["Interest"].Value.Trim();

            // Normalizar paréntesis negativos
            if (raw.StartsWith("(") && raw.EndsWith(")"))
            {
                raw = "-" + raw.Substring(1, raw.Length - 2);
            }

            raw = raw.Replace("$", "").Replace(",", "").Trim();

            if (decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                return value;

            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out value))
                return value;

            return null;
        }

        public static decimal? ExtractPaymentsCredits(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var m = regexPaymentsCredits.Match(text); if (!m.Success)
                if (!m.Success)
                    return null;


            var raw = m.Groups["PaymentsCredits"].Value.Trim();

            // Normalizar paréntesis negativos
            if (raw.StartsWith("(") && raw.EndsWith(")"))
            {
                raw = "-" + raw.Substring(1, raw.Length - 2);
            }

            raw = raw.Replace("$", "").Replace(",", "").Trim();

            if (decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                return value;

            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out value))
                return value;

            return null;
        }
        public static decimal? ExtractBalanceAnterior(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var m = regexBeginningBalance.Match(text); if (!m.Success)
                if (!m.Success)
                    return null;

            var raw = m.Groups["SaldoAnt"].Value.Trim();

            // Normalizar paréntesis negativos
            if (raw.StartsWith("(") && raw.EndsWith(")"))
            {
                raw = "-" + raw.Substring(1, raw.Length - 2);
            }

            raw = raw.Replace("$", "").Replace(",", "").Trim();

            if (decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                return value;

            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out value))
                return value;

            return null;
        }

        /// <summary>
        /// Extract the most relevant amount from a transaction block.
        /// Heuristics:
        ///  - If an amount is followed (within a short window) by a currency word like "Mexican" or "Pesos", prefer it.
        ///  - Otherwise prefer the last numeric amount in the block.
        /// </summary>
        public static decimal? ExtractPreferredAmountFromBlock(string block)
        {
            if (string.IsNullOrWhiteSpace(block))
                return null;

            // Normalize simple OCR artifacts
            block = block.Replace("﹩", "$").Replace("S$", "$");

            //// 1) Buscar la ÚLTIMA ocurrencia explícita de $ seguida de número (permite espacios o ':' entre ellos)
            //var explicitDollarRegex = new Regex(@"\$\s*[:\-]?\s*([0-9]{1,3}(?:,[0-9]{3})*\.[0-9]{2})", RegexOptions.RightToLeft);
            //var explicitDollarRegex = new Regex(@"\$\s*[:\-]?\s*([0-9]{1,3}(?:,[0-9]{3})*\.[0-9]{2})", RegexOptions.RightToLeft);
            var explicitDollarRegex = new Regex(
    @"(?<!\w)(-?\$?\s*[0-9]{1,3}(?:,[0-9]{3})*\.[0-9]{2})",
    RegexOptions.RightToLeft
);


            var mDollar = explicitDollarRegex.Match(block);
            if (mDollar.Success)
            {
                var raw = mDollar.Groups[1].Value;
                if (decimal.TryParse(raw.Replace(",", ""), NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v))
                    return v;
            }

            // 1b) Caso OCR donde $ y número están separados y regex anterior falla:
            // buscar un '$' cercano antes de un monto (hasta 6 caracteres entre $ y cifra)
            var looseDollarRegex = new Regex(@"\$\s{0,6}([0-9]{1,3}(?:,[0-9]{3})*\.[0-9]{2})", RegexOptions.RightToLeft);
            var mLoose = looseDollarRegex.Match(block);
            if (mLoose.Success)
            {
                //var raw = mLoose.Groups[1].Value;
                //if (decimal.TryParse(raw.Replace(",", ""), NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v))
                //    return v;
                var raw = mDollar.Groups[1].Value
    .Replace("$", "")
    .Replace(",", "")
    .Replace(" ", "");

                if (decimal.TryParse(
                    raw,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var v))
                {
                    return v;
                }

            }

            // 2) Si no hay $ (o no se pudo parsear), preferir monto seguido de palabra moneda (Mexican / Pesos)
            var monedaRegex = new Regex(@"([0-9]{1,3}(?:,[0-9]{3})*\.[0-9]{2})\s*(?i:(Mexican|Pesos|MXN|USD|Dollars|dólares|pesos))");
            var mMon = monedaRegex.Match(block);
            if (mMon.Success)
            {
                var raw = mMon.Groups[1].Value;
                if (decimal.TryParse(raw.Replace(",", ""), NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v))
                    return v;
            }

            //// 3) Fallback: devolver el último monto numérico en el bloque
            //var anyAmountRegex = new Regex(@"([0-9]{1,3}(?:,[0-9]{3})*\.[0-9]{2})", RegexOptions.RightToLeft);
            //var mAny = anyAmountRegex.Match(block);
            //if (mAny.Success)
            //{
            //    var raw = mAny.Groups[1].Value;
            //    if (decimal.TryParse(raw.Replace(",", ""), NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v))
            //        return v;
            //}

            var anyAmountRegex = new Regex(@"[0-9]{1,3}(?:,[0-9]{3})*.[0-9]{2}");
            var matchesAmounts = anyAmountRegex.Matches(block);
            if (matchesAmounts.Count > 0)
            {
                // Buscar desde la derecha la primera que tenga '$' en un rango cercano
                for (int i = matchesAmounts.Count - 1; i >= 0; i--)
                {
                    var mm = matchesAmounts[i];
                    int start = mm.Index;
                    int end = mm.Index + mm.Length - 1;
                    int lookBehind = Math.Max(0, start - 6);
                    int lookAhead = Math.Min(block.Length - 1, end + 6);
                    string context = block.Substring(lookBehind, lookAhead - lookBehind + 1);
                    if (context.Contains("$"))
                    {
                        var raw = mm.Value;
                        if (decimal.TryParse(raw.Replace(",", ""), NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v))
                            return v;
                    }
                }
                // Si ninguna cantidad cercana a '$', devolver la última cantidad encontrada
                var lastMatch = matchesAmounts[matchesAmounts.Count - 1].Value;
                if (decimal.TryParse(lastMatch.Replace(",", ""), NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var lastV))
                    return lastV;


            }
            return null;
        }

        private static string ExtraerEmpresa(string descripcion)
        {
            if (string.IsNullOrWhiteSpace(descripcion))
                return "";

            // 1. Eliminar el monto si quedara
            descripcion = Regex.Replace(descripcion, @"\$\d+(\.\d{2})?", "").Trim();

            // 2. Dividir por espacios, eliminar números y códigos de estado
            var partes = descripcion.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var listaEmpresa = new List<string>();

            foreach (var parte in partes)
            {
                // Ignorar si es un número o código de estado tipo FL, WA, 3053564066
                if (Regex.IsMatch(parte, @"^\d+$") || Regex.IsMatch(parte, @"^(FL|WA|NY|CA|TX)$", RegexOptions.IgnoreCase))
                    continue;

                listaEmpresa.Add(parte);
            }

            // 3. Reconstruir cadena limpia
            return string.Join(" ", listaEmpresa).Trim();
        }

        // Suma montos positivos y negativos (negativos conservan su signo).
        public static (decimal PositiveTotal, decimal NegativeTotal) SumPositivesAndNegatives(IEnumerable<Movimiento> movimientos)
        {
            decimal positives = 0m;
            decimal negatives = 0m;

            if (movimientos == null)
                return (0m, 0m);

            foreach (var m in movimientos)
            {
                var value = m?.Monto ?? 0m;
                if (value >= 0m) positives += value;
                else negatives += value; // acumulado como valor negativo
            }

            return (positives, negatives);
        }

        // Variante que devuelve el total de negativos como valor absoluto (suma de débitos).
        public static (decimal PositiveTotal, decimal NegativeTotalAbsolute) SumPositivesAndNegativesAbsolute(IEnumerable<Movimiento> movimientos)
        {
            decimal positives = 0m;
            decimal negativesAbs = 0m;

            if (movimientos == null)
                return (0m, 0m);

            foreach (var m in movimientos)
            {
                var value = m?.Monto ?? 0m;
                if (value >= 0m) positives += value;
                else negativesAbs += Math.Abs(value); // sumar el absoluto para obtener total de débitos
            }

            return (positives, negativesAbs);
        }
        //private static TipoMovimiento DetectarTipoMovimiento(string notas, string descripcion)
        //{
        //    string contexto = $"{notas} {descripcion}".ToLowerInvariant();

        //    // First try keyword heuristics (covers Amex, Chase and general patterns)
        //    if (regexCreditoContexto.IsMatch(contexto))
        //        return _invertAmexClassification ? TipoMovimiento.Debito : TipoMovimiento.Credito;

        //    if (regexDebitoContexto.IsMatch(contexto))
        //        return _invertAmexClassification ? TipoMovimiento.Credito : TipoMovimiento.Debito;

        //    // If parentheses around amounts are present in the notes, many statements use parentheses to show negative amounts (debits)
        //    if (Regex.IsMatch(notas + " " + descripcion, @"\(\$?\d"))
        //        return _invertAmexClassification ? TipoMovimiento.Credito : TipoMovimiento.Debito;

        //    // If there's an explicit leading '-' before an amount in the raw text, treat as debit
        //    if (Regex.IsMatch(notas + " " + descripcion, @"-\s*\$?\d"))
        //        return _invertAmexClassification ? TipoMovimiento.Credito : TipoMovimiento.Debito;

        //    // Fallback safe default
        //    return _invertAmexClassification ? TipoMovimiento.Credito : TipoMovimiento.Debito;
        //}
        //private static TipoMovimiento DetectarTipoMovimiento(string notas, string descripcion)
        //{
        //    string texto = $"{notas} {descripcion}";

        //    // 1️⃣ REGLA ABSOLUTA: signo negativo o paréntesis = DÉBITO
        //    if (Regex.IsMatch(texto, @"\(\s*\$?\d") ||   // ($5.35)
        //        Regex.IsMatch(texto, @"-\s*\$?\d"))      // -$5.35
        //    {
        //        return _invertAmexClassification
        //            ? TipoMovimiento.Credito
        //            : TipoMovimiento.Debito;
        //    }

        //    string contexto = texto.ToLowerInvariant();

        //    // 2️⃣ Heurísticas semánticas
        //    if (regexCreditoContexto.IsMatch(contexto))
        //        return _invertAmexClassification
        //            ? TipoMovimiento.Debito
        //            : TipoMovimiento.Credito;

        //    if (regexDebitoContexto.IsMatch(contexto))
        //        return _invertAmexClassification
        //            ? TipoMovimiento.Credito
        //            : TipoMovimiento.Debito;

        //    // 3️⃣ Fallback seguro
        //    return _invertAmexClassification
        //        ? TipoMovimiento.Credito
        //        : TipoMovimiento.Debito;
        //}
        private static TipoMovimiento DetectarTipoMovimiento(string notas, string descripcion)
        {
            string texto = $"{notas} {descripcion}";

            // Si el monto es negativo en el texto → Débito
            if (Regex.IsMatch(texto, @"-\s*\$?\d") ||
                Regex.IsMatch(texto, @"\(\s*\$?\d"))
            {
                return TipoMovimiento.Debito;
            }

            string contexto = texto.ToLowerInvariant();

            if (regexCreditoContexto.IsMatch(contexto))
                return TipoMovimiento.Credito;

            if (regexDebitoContexto.IsMatch(contexto))
                return TipoMovimiento.Debito;

            // Fallback conservador
            return TipoMovimiento.Debito;
        }

        // Helper para detectar si el Bank es Amex (por nombre o identificador)
        private static bool IsAmex(Bank banco)
        {
            if (banco == null) return false;
            if (!string.IsNullOrWhiteSpace(banco.BankName) && banco.BankName.IndexOf("amex", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (!string.IsNullOrWhiteSpace(banco.BankIdentifier) && banco.BankIdentifier.IndexOf("amex", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            // No forzar por BankId aquí; si necesitan un id específico, se puede añadir.
            return false;
        }






    }

}