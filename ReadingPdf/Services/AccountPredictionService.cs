using Microsoft.ML;
using Microsoft.ML.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ReadingPdf.Services
{
    public class AccountPredictionService
    {
        private readonly MLContext _ml;

        public AccountPredictionService()
        {
            _ml = new MLContext();
        }

        public (string cuenta, float score) PredictForCompany(
    string memo,
    string company,
    List<string> planCuentas,
    string modelPath,
    string tokensPath,
    string labelsPath)
        {
            try
            {
                // Normalizar entrada
                memo = (memo ?? "").ToUpperInvariant().Trim();
                company = (company ?? "").ToUpperInvariant().Trim();

                // 0) Overrides manuales
                var overrideCuenta = MapVendorOverride(memo, company);
                if (!string.IsNullOrWhiteSpace(overrideCuenta))
                    return (overrideCuenta, 1.0f);

                // 1) Memo limpio
                var memoClean = CleanMemoForPrediction(memo);

                if (string.IsNullOrWhiteSpace(memoClean))
                {
                    var heur = MapHeuristicFallback(memo, company);
                    if (!string.IsNullOrWhiteSpace(heur))
                        return (heur, 0.50f);

                    return ("Expenses:Uncategorized", 0.20f);
                }

                // 2) Company opcional
                if (string.IsNullOrWhiteSpace(company))
                {
                    var heur = MapHeuristicFallback(memoClean, "");
                    if (!string.IsNullOrWhiteSpace(heur))
                        return (heur, 0.40f);

                    company = ""; // NO usar "UNKNOWN"
                }

                // 3) SimilarWordsCount (solo si hay empresa)
                var tokens = LoadTokens(tokensPath);
                float simScore = 0;
                if (!string.IsNullOrWhiteSpace(company))
                    simScore = ComputeSimilarWordsScore(memoClean, company, tokens);

                // 4) Modelo
                var model = LoadModelOrNull(modelPath);
                if (model != null)
                {
                    var engine = _ml.Model.CreatePredictionEngine<PredItem, PredOutput>(model);

                    var input = new PredItem
                    {
                        Memo = memoClean,
                        Company = company,
                        SimilarWordsCount = simScore,
                        Label = ""
                    };

                    var pred = engine.Predict(input);

                    if (pred != null)
                    {
                        // La cuenta viene directamente del modelo
                        string cuentaML = pred.PredictedLabel ?? string.Empty;

                        // Score = probabilidad máxima entre las clases
                        float scoreML = 0f;
                        if (pred.Score != null && pred.Score.Length > 0)
                            scoreML = pred.Score.Max();

                        string cuentaFiltrada = FilterAccountToPlan(cuentaML, planCuentas);

                        if (!string.IsNullOrWhiteSpace(cuentaFiltrada) &&
                            cuentaFiltrada != "SIN PREDICCION")
                        {
                            if (scoreML >= 0.15f)
                                return (cuentaFiltrada, scoreML);

                            if (simScore >= 0.30f)
                                return (cuentaFiltrada, simScore);
                        }
                    }

                }

                // 5) Heurística secundaria
                var heur2 = MapHeuristicFallback(memoClean, company);
                if (!string.IsNullOrWhiteSpace(heur2))
                    return (heur2, Math.Max(simScore, 0.30f));

                // 6) Fallback basado en palabras genéricas del plan
                var generic2 = GuessFromPlanByKeywords(memoClean, planCuentas);
                if (!string.IsNullOrWhiteSpace(generic2))
                    return (generic2, Math.Max(simScore, 0.20f));

                // 7) Último recurso
                return ("Expenses:Uncategorized", 0.10f);
            }
            catch
            {
                return ("Expenses:Uncategorized", 0);
            }
        }


        // -----------------------------------------------------------------------
        //   LIMPIEZA DE MEMO (MUY IMPORTANTE PARA BUENAS PREDICCIONES)
        // -----------------------------------------------------------------------
        private string CleanMemoForPrediction(string memo)
        {
            if (string.IsNullOrWhiteSpace(memo))
                return "";

            // Aquí asumimos que memo ya está en MAYÚSCULAS
            string t = memo;

            string[] noise =
            {
                "ACH", "DEBIT", "CREDIT", "WEB", "PMTS", "PMT", "PAYMENT",
                "PAY", "TRANSFER", "TRF", "ONLINE", "DBT", "CR", "POS",
                "CARD", "VISA", "MASTERCARD"
            };

            foreach (var n in noise)
                t = t.Replace(n + " ", " ");

            // Quitar números, dejar letras, comas, puntos, & y espacios
            var sb = new System.Text.StringBuilder();
            foreach (char c in t)
            {
                if (char.IsLetter(c) || c == ' ' || c == ',' || c == '.' || c == '&')
                    sb.Append(c);
            }

            t = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();

            return t;
        }

        // -----------------------------------------------------------------------
        //   OVERRIDES POR PROVEEDORES IMPORTANTES
        // -----------------------------------------------------------------------
        private string? MapVendorOverride(string memo, string? company)
        {
            var txt = (memo ?? "").ToUpperInvariant();
            var comp = (company ?? "").ToUpperInvariant();

            // SaaS de property management
            if (txt.Contains("APPFOLIO") || comp.Contains("APPFOLIO"))
                return "Expenses:Software";

            // Uber y similares
            if (txt.Contains("UBER") || txt.Contains("LYFT"))
                return "Expenses:Transportation";

            // Gasolina (marcas)
            if (txt.Contains("SHELL") || txt.Contains("EXXON") || txt.Contains("BP "))
                return "Expenses:Gasoline";

            // Subscriptions conocidas
            if (txt.Contains("SPOTIFY") || txt.Contains("NETFLIX"))
                return "Expenses:Software";

            // Puedes ir agregando más overrides aquí según tus estados reales

            return null;
        }

        // -----------------------------------------------------------------------
        //   HEURÍSTICA DE FALLBACK PARA MEMOS/COMPANIES NO RECONOCIDOS
        // -----------------------------------------------------------------------
        private string? MapHeuristicFallback(string memo, string? company)
        {
            var txt = (memo ?? "").ToUpperInvariant();
            var comp = (company ?? "").ToUpperInvariant();

            // Restaurantes / comida
            if (txt.Contains("RESTAURANT") || txt.Contains("FOOD") || txt.Contains("CAFE") || txt.Contains("CAFÉ"))
                return "Expenses:Restaurants";

            // Combustible / gasolineras
            if (txt.Contains("GAS") || txt.Contains("FUEL") || txt.Contains("PETROLEUM") || txt.Contains("STATION"))
                return "Expenses:Gasoline";

            // Viajes
            if (txt.Contains("HOTEL") || txt.Contains("MOTEL") || txt.Contains("AIRLINES") || txt.Contains("AIRLINE") ||
                txt.Contains("UBER") || txt.Contains("LYFT") || txt.Contains("TAXI"))
                return "Expenses:Travel";

            // Software / suscripción
            if (txt.Contains("SOFT") || txt.Contains("SAAS") || txt.Contains("SUBSCRIPTION") ||
                txt.Contains("MICROSOFT") || txt.Contains("ADOBE") || txt.Contains("GOOGLE") || txt.Contains("APPLE"))
                return "Expenses:Software";

            // Oficina / suministros
            if (txt.Contains("OFFICE") || txt.Contains("SUPPLIES") || txt.Contains("STAPLES"))
                return "Expenses:Supplies";

            // Renta
            if (txt.Contains("RENT") || comp.Contains("RENT"))
                return "Expenses:Rent";

            // Servicios (utilities)
            if (txt.Contains("UTILITY") || txt.Contains("UTILITIES") ||
                txt.Contains("ELECTRIC") || txt.Contains("WATER") || txt.Contains("GAS COMPANY"))
                return "Expenses:Utilities";

            return null;
        }

        // -----------------------------------------------------------------------
        //   CÁLCULO DE SIMILARIDAD BASADO EN PALABRAS
        // -----------------------------------------------------------------------
        private float ComputeSimilarWordsScore(string memo, string company, Dictionary<string, List<string>> tokens)
        {
            if (string.IsNullOrWhiteSpace(company) || tokens == null)
                return 0;

            string key = company.ToUpperInvariant();

            if (!tokens.TryGetValue(key, out var wordList) || wordList == null || wordList.Count == 0)
                return 0;

            var memoWords = memo
                .ToUpperInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            int matches = wordList.Count(w => memoWords.Contains(w.ToUpperInvariant()));

            if (matches == 0) return 0f;
            if (matches == 1) return 0.25f;
            if (matches == 2) return 0.45f;
            if (matches >= 3) return 0.65f;

            return 0.8f;
        }

        // -----------------------------------------------------------------------
        //   SI EL MODELO PREDICE UNA CUENTA QUE NO ESTÁ EN EL PLAN → AJUSTAR
        // -----------------------------------------------------------------------
        private string FilterAccountToPlan(string cuenta, List<string> plan)
        {
            if (plan == null || plan.Count == 0)
                return cuenta ?? "SIN PREDICCION";

            if (string.IsNullOrWhiteSpace(cuenta))
                return "SIN PREDICCION";

            // Coincidencia exacta (case-insensitive)
            var exact = plan.FirstOrDefault(p =>
                string.Equals(p, cuenta, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(exact))
                return exact;

            // Coincidencia por categoría antes de los ':'
            var category = cuenta.Split(':')[0];
            var byPrefix = plan.FirstOrDefault(p =>
                p.StartsWith(category, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(byPrefix))
                return byPrefix;

            // Búsqueda por palabras clave genéricas
            if (cuenta.ToUpperInvariant().Contains("RESTAURANT"))
            {
                var r = plan.FirstOrDefault(p => p.ToUpperInvariant().Contains("RESTAURANT") || p.ToUpperInvariant().Contains("MEALS"));
                if (!string.IsNullOrWhiteSpace(r)) return r;
            }

            if (cuenta.ToUpperInvariant().Contains("GAS"))
            {
                var r = plan.FirstOrDefault(p => p.ToUpperInvariant().Contains("GAS"));
                if (!string.IsNullOrWhiteSpace(r)) return r;
            }

            if (cuenta.ToUpperInvariant().Contains("SOFTWARE"))
            {
                var r = plan.FirstOrDefault(p => p.ToUpperInvariant().Contains("SOFTWARE"));
                if (!string.IsNullOrWhiteSpace(r)) return r;
            }

            if (cuenta.ToUpperInvariant().Contains("TRAVEL"))
            {
                var r = plan.FirstOrDefault(p => p.ToUpperInvariant().Contains("TRAVEL"));
                if (!string.IsNullOrWhiteSpace(r)) return r;
            }

            return "SIN PREDICCION";
        }

        // Pequeña ayuda: intentar elegir una cuenta genérica del plan según palabras clave
        private string? GuessFromPlanByKeywords(string memo, List<string> plan)
        {
            var txt = memo.ToUpperInvariant();
            var planUpper = plan.Select(p => new { Original = p, Upper = p.ToUpperInvariant() }).ToList();

            if (txt.Contains("RESTAURANT") || txt.Contains("FOOD") || txt.Contains("CAFE"))
            {
                var r = planUpper.FirstOrDefault(p => p.Upper.Contains("RESTAURANT") || p.Upper.Contains("MEALS"));
                if (r != null) return r.Original;
            }

            if (txt.Contains("GAS") || txt.Contains("FUEL") || txt.Contains("PETROLEUM"))
            {
                var r = planUpper.FirstOrDefault(p => p.Upper.Contains("GAS"));
                if (r != null) return r.Original;
            }

            if (txt.Contains("HOTEL") || txt.Contains("AIRLINES") || txt.Contains("UBER") || txt.Contains("LYFT"))
            {
                var r = planUpper.FirstOrDefault(p => p.Upper.Contains("TRAVEL"));
                if (r != null) return r.Original;
            }

            if (txt.Contains("SOFT") || txt.Contains("SAAS") || txt.Contains("SUBSCRIPTION"))
            {
                var r = planUpper.FirstOrDefault(p => p.Upper.Contains("SOFTWARE") || p.Upper.Contains("SUBSCRIPTION"));
                if (r != null) return r.Original;
            }

            return null;
        }

        // -----------------------------------------------------------------------
        //   TRAIN & SAVE (stub por ahora)
        // -----------------------------------------------------------------------
        public void TrainAndSave(
     IEnumerable<(string Label, string Memo, string Company)> rowsForTrain,
     string modelPath,
     string tokensPath,
     string labelsPath)
        {
            var ml = _ml;

            // Convertir a lista segura
            var dataList = rowsForTrain
                .Select(r => new TrainingItem
                {
                    Label = (r.Label ?? "").Trim(),
                    Memo = (r.Memo ?? "").Trim().ToUpperInvariant(),
                    Company = (r.Company ?? "").Trim().ToUpperInvariant(),
                    SimilarWordsCount = 0 // si luego quieres usar tokens, lo podemos activar
                })
                .ToList();

            if (dataList.Count < 2)
                throw new Exception("Se requieren al menos 2 filas para entrenar.");

            int clases = dataList.Select(x => x.Label).Distinct().Count();
            if (clases < 2)
                throw new Exception("Se requieren al menos 2 categorías (Label) distintas para entrenar.");

            // Crear IDataView
            var dataView = ml.Data.LoadFromEnumerable(dataList);

            // -------------------------------
            // PIPELINE CORRECTO
            // -------------------------------
            var pipeline =
                ml.Transforms.Text.FeaturizeText(
                        outputColumnName: "MemoFeats",
                        inputColumnName: nameof(TrainingItem.Memo))
                .Append(ml.Transforms.Text.FeaturizeText(
                        outputColumnName: "CompanyFeats",
                        inputColumnName: nameof(TrainingItem.Company)))
                .Append(ml.Transforms.Concatenate(
                        "Features",
                        "MemoFeats",
                        "CompanyFeats",
                        nameof(TrainingItem.SimilarWordsCount)))
                .Append(ml.Transforms.Conversion.MapValueToKey(
                        outputColumnName: "Label",
                        inputColumnName: nameof(TrainingItem.Label)))
                .Append(ml.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                .Append(ml.Transforms.Conversion.MapKeyToValue(
                        outputColumnName: "PredictedLabel",
                        inputColumnName: "PredictedLabel"));

            var model = pipeline.Fit(dataView);



            // -------------------------------
            // GUARDAR MODELO
            // -------------------------------
            ml.Model.Save(model, dataView.Schema, modelPath);

            // -------------------------------
            // GUARDAR LABELS (para referencia)
            // -------------------------------
            var uniqueLabels = dataList.Select(x => x.Label).Distinct().ToList();
            File.WriteAllLines(labelsPath, uniqueLabels);

            // -------------------------------
            // GUARDAR TOKENS DE EMPRESA OPCIONAL
            // -------------------------------
            var tokens = new Dictionary<string, List<string>>();

            foreach (var g in dataList.GroupBy(x => x.Company))
            {
                if (string.IsNullOrWhiteSpace(g.Key)) continue;

                var words = g.SelectMany(x => x.Memo.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                             .Distinct()
                             .ToList();

                tokens[g.Key] = words;
            }

            File.WriteAllText(tokensPath, JsonConvert.SerializeObject(tokens, Formatting.Indented));
        }

        public class TrainingItem
        {
            // Etiqueta/categoría real (“Expenses:Gasoline”, “Expenses:Software”)
            public string Label { get; set; } = "";

            // Memo ya normalizado y limpio
            public string Memo { get; set; } = "";

            // Empresa normalizada (“APPFOLIO”, “UBER”, etc.)
            public string Company { get; set; } = "";

            // Puntuación opcional para tokens de similitud
            public float SimilarWordsCount { get; set; }
        }


        private Dictionary<string, List<string>> LoadTokens(string tokensPath)
        {
            if (string.IsNullOrWhiteSpace(tokensPath) || !File.Exists(tokensPath))
                return new Dictionary<string, List<string>>();

            var json = File.ReadAllText(tokensPath);
            var tokens = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json)
                         ?? new Dictionary<string, List<string>>();

            // Normalizar claves y valores a MAYÚSCULAS para comparación consistente
            var normalized = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in tokens)
            {
                var keyUpper = kvp.Key.ToUpperInvariant();
                var wordsUpper = kvp.Value?
                    .Select(w => w.ToUpperInvariant())
                    .Distinct()
                    .ToList() ?? new List<string>();

                normalized[keyUpper] = wordsUpper;
            }

            return normalized;
        }

        private ITransformer? LoadModelOrNull(string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
                return null;

            try
            {
                using var fileStream = new FileStream(modelPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return _ml.Model.Load(fileStream, out _);
            }
            catch
            {
                return null;
            }
        }

        private string GetLabelById(int predictedId, string labelsPath)
        {
            if (string.IsNullOrWhiteSpace(labelsPath) || !File.Exists(labelsPath))
                return "SIN PREDICCION";

            var labels = File.ReadAllLines(labelsPath);
            if (predictedId >= 0 && predictedId < labels.Length)
                return labels[predictedId];

            return "SIN PREDICCION";
        }
    }

    // ********************************************************************************************
    //   MODELOS PARA EL PREDICTOR ML.NET
    // ********************************************************************************************
    public class PredItem
    {
        public string Label { get; set; } = "";
        public string Memo { get; set; } = "";
        public string Company { get; set; } = "";
        public float SimilarWordsCount { get; set; }
    }

    public class PredOutput
    {
        // Etiqueta predicha por el modelo (generalmente nombre de la cuenta)
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; } = "";

        // Vector de probabilidades por clase
        public float[] Score { get; set; } = Array.Empty<float>();
    }

}
