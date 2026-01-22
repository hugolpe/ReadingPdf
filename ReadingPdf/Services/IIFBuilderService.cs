using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using ReadingPdf.Models;

namespace ReadingPdf.Services
{
    public class QuickBooksAccountMap
    {
        public string BankAccount { get; set; }
        public Dictionary<string, string> Map { get; set; }
    }

    public interface IIIFBuilderService
    {
        string BuildIIF(List<Movimiento> items);
    }

    public class IIFBuilderService : IIIFBuilderService
    {
        private readonly QuickBooksAccountMap _config;

        // Accept IWebHostEnvironment via DI; optional so tests/manual instantiation still funciona.
        public IIFBuilderService(IWebHostEnvironment? env = null)
        {
            // Determinar ruta del archivo de configuración de forma robusta.
            string configPath;
            if (env != null)
            {
                configPath = Path.Combine(env.ContentRootPath, "Config", "QuickBooksMapping.json");
            }
            else
            {
                // Fallback a la carpeta de la aplicación (útil para pruebas, CLI, etc.)
                configPath = Path.Combine(AppContext.BaseDirectory, "Config", "QuickBooksMapping.json");
            }

            // Valores por defecto seguros
            var defaultMap = new QuickBooksAccountMap
            {
                BankAccount = "Bank",
                Map = new Dictionary<string, string>
                {
                    { "Uncategorized Expense", "Ask My Accountant" }
                }
            };

            try
            {
                if (!File.Exists(configPath))
                {
                    // No está el archivo en la ruta calculada: usar default y no romper la app.
                    _config = defaultMap;
                }
                else
                {
                    var json = File.ReadAllText(configPath);
                    var parsed = JsonConvert.DeserializeObject<QuickBooksAccountMap>(json);

                    // Si el JSON está mal formado o faltan campos, aplicar defaults parciales
                    if (parsed == null)
                    {
                        _config = defaultMap;
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(parsed.BankAccount))
                            parsed.BankAccount = defaultMap.BankAccount;
                        if (parsed.Map == null)
                            parsed.Map = defaultMap.Map;

                        _config = parsed;
                    }
                }
            }
            catch
            {
                // En caso de cualquier error de I/O o parseo, usar valores por defecto seguros.
                _config = defaultMap;
            }
        }

        public string BuildIIF(List<Movimiento> items)
        {
            var sb = new StringBuilder();

            // Encabezado requerido por QuickBooks
            sb.AppendLine("!TRNS\tTRNSTYPE\tDATE\tACCNT\tAMOUNT\tNAME\tMEMO");
            sb.AppendLine("!SPL\tTRNSTYPE\tDATE\tACCNT\tAMOUNT\tNAME\tMEMO");
            sb.AppendLine("!ENDTRNS");

            foreach (var m in items)
            {
                if (m == null)
                    continue;

                string fecha = m.Fecha.ToString("MM/dd/yyyy");
                string memo = m.Descripcion?.Replace("\t", " ") ?? "";
                decimal amount = m.Monto;

                // Seguridad en el JSON
                string bankAccount = _config?.BankAccount ?? "Bank";

                // Cuenta SIGEFINT que viene de tu modelo
                string cuentaSIGEFINT = m.CuentaContable ?? "Uncategorized Expense";

                // Mapeo seguro (sin nulls)
                string cuentaDestino = "Ask My Accountant";
                if (_config != null && _config.Map != null &&
                    _config.Map.ContainsKey(cuentaSIGEFINT))
                {
                    cuentaDestino = _config.Map[cuentaSIGEFINT];
                }

                const string TRNSTYPE = "GENERAL JOURNAL";

                sb.AppendLine(
                    $"TRNS\t{TRNSTYPE}\t{fecha}\t{bankAccount}\t{-amount}\t\t{memo}"
                );

                sb.AppendLine(
                    $"SPL\t{TRNSTYPE}\t{fecha}\t{cuentaDestino}\t{amount}\t\t{memo}"
                );

                sb.AppendLine("ENDTRNS");
            }

            return sb.ToString();
        }
    }
}
