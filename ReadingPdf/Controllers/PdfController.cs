using AspNetCoreGeneratedDocument;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
using ReadingPdf.Models;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

public class PdfController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

   

    [HttpPost]
    public async Task<IActionResult> Procesar(IFormFile archivoPdf)
    {
        if (archivoPdf == null || archivoPdf.Length == 0)
            return View("Index");

        // Use Task.Run to make the method truly asynchronous
        var textoDesdePagina3 = await Task.Run(() =>
        {
            var sb = new StringBuilder();
            using (var stream = archivoPdf.OpenReadStream())
            using (var documento = PdfDocument.Open(stream))
            {
                if (documento.NumberOfPages >= 3)
                {
                    foreach (var pagina in documento.GetPages().Skip(2))
                    {
                        sb.AppendLine(pagina.Text);
                    }
                }
            }
            return sb.ToString();
        });

        var textoPlano = textoDesdePagina3.ToString();





        // Contar cuántos "Amount" hay en la página 3 solamente
        string textoPagina3;
        using (var stream = archivoPdf.OpenReadStream())
        using (var documento = PdfDocument.Open(stream))
        {
            textoPagina3 = documento.NumberOfPages >= 3
                ? documento.GetPage(3).Text
                : string.Empty;
        }

        var matchesAmount = Regex.Matches(textoPagina3, @"Amount", RegexOptions.None);
        int cantidadAmount = matchesAmount.Count;




        //var matches = Regex.Matches(textoPlano, @"Amount", RegexOptions.None);

        if (matchesAmount.Count >= 3)
        {
            int indice = matchesAmount[2].Index;
            textoPlano = textoPlano.Substring(indice + matchesAmount[2].Length).Trim();
        }
        else if (matchesAmount.Count == 2)
        {
            int indice = matchesAmount[1].Index;
            textoPlano = textoPlano.Substring(indice + matchesAmount[1].Length).Trim();
        }
        else if (matchesAmount.Count == 1)
        {
            int indice = matchesAmount[0].Index;
            // textoPlano = textoPlano.Substring(indice + matches[0].Length).Trim(); // opcional
        }


        // Extraer texto antes de la primera aparición de "Fees" desde página 3 en adelante
        int indiceFees = textoPlano.IndexOf("Total Fees", StringComparison.OrdinalIgnoreCase);
        string operacionesAntesDeFees = indiceFees > 0
            ? textoPlano.Substring(0, indiceFees).Trim()
            : textoPlano.Trim(); // Si no se encuentra "Fees", usar todo


    
    System.IO.File.WriteAllText("wwwroot/textoExtraido.txt", textoPlano);

        // Procesar solo las líneas filtradas
        var transacciones = ExtraerTransacciones(operacionesAntesDeFees);

        var movimientos = ExtraerTransacciones2(operacionesAntesDeFees);

        ViewBag.CantidadAmount = cantidadAmount;

        return GenerarArchivoTexto1(transacciones);
    }


    private void GuardarExcel(PdfDataModel modelo)
    {
        // Fix for CS0200 and CS0246:
        // Set the license context using the correct static property and enum.
        //OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

        //ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

        using var paquete = new ExcelPackage();
        var hoja = paquete.Workbook.Worksheets.Add("Datos");

        hoja.Cells[1, 1].Value = "Montos";
        hoja.Cells[1, 2].Value = "Fechas";

        for (int i = 0; i < Math.Max(modelo.Montos.Count, modelo.Fechas.Count); i++)
        {
            if (i < modelo.Montos.Count)
                hoja.Cells[i + 2, 1].Value = modelo.Montos[i];
            if (i < modelo.Fechas.Count)
                hoja.Cells[i + 2, 2].Value = modelo.Fechas[i];
        }

        var ruta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "datos.xlsx");
        paquete.SaveAs(new FileInfo(ruta));
    }
    public void GuardarEnTextoPlano(PdfDataModel modelo)
    {
        var ruta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "datos.txt");
        using var escritor = new StreamWriter(ruta, false, Encoding.UTF8);

        escritor.WriteLine("=== Montos y Fechas extraídos del PDF ===");
        escritor.WriteLine();
        escritor.WriteLine("{0,-25} {1,-25}", "Monto", "Fecha");
        escritor.WriteLine(new string('-', 50));

        int filas = Math.Max(modelo.Montos.Count, modelo.Fechas.Count);
        for (int i = 0; i < filas; i++)
        {
            
            string monto = i < modelo.Montos.Count ? modelo.Montos[i] : "";
            string fecha = i < modelo.Fechas.Count ? modelo.Fechas[i] : "";
            escritor.WriteLine("{0,-25} {1,-25}", monto, fecha);
        }
    }

    public FileResult GenerarArchivoTexto2(PdfDataModel modelo)
    {
        var stream = new MemoryStream();
        using var escritor = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

        escritor.WriteLine("=== Montos y Fechas extraídos del PDF ===");
        escritor.WriteLine();
        escritor.WriteLine("{0,-25} {1,-25}", "Monto", "Fecha");
        escritor.WriteLine(new string('-', 50));

        int filas = Math.Max(modelo.Montos.Count, modelo.Fechas.Count);
        for (int i = 0; i < filas; i++)
        {
            string monto = i < modelo.Montos.Count ? modelo.Montos[i] : "";
            string fecha = i < modelo.Fechas.Count ? modelo.Fechas[i] : "";
            escritor.WriteLine("{0,-25} {1,-25}", monto, fecha);
        }

        escritor.Flush();
        stream.Position = 0;

        return File(stream, "text/plain", "datos.txt");
    }



    public List<Transaccion1> ExtraerTransacciones1(string texto)
    {
        var transacciones1 = new List<Transaccion1>();


        var patron = new Regex(
        @"(?<fecha>\d{2}/\d{2}/\d{2})\s+" +          // Fecha
        @"(?<linea1>.+?)\r?\n" +                     // Primera línea de descripción
        @"(?<linea2>.+?)\r?\n" +                     // Segunda línea de descripción
        @"(?<estado>[A-Z ]+)\s+" +                   // Estado (ej: APPROVED / DECLINED)
        @"(?<monto>\$\d{1,3}(,\d{3})*(\.\d{2})?)",   // Monto
        RegexOptions.Multiline);

        var matches = patron.Matches(texto);

        foreach (Match match in matches)
        {
            transacciones1.Add(new Transaccion1
            {
                Fecha = match.Groups["fecha"].Value.Trim(),
                Linea1 = match.Groups["linea1"].Value.Trim(),
                Linea2 = match.Groups["linea2"].Value.Trim(),
                Estado = match.Groups["estado"].Value.Trim(),
                Monto = match.Groups["monto"].Value.Trim()
            });
        }


        return transacciones1;
    }


    public List<Transaccion> ExtraerTransacciones(string texto)
    {
        var transacciones = new List<Transaccion>();

        var patron = new Regex(
            @"(?<fecha>\d{2}/\d{2}/\d{2})(?<descripcion>.*?)(?<monto>\$\d{1,3}(,\d{3})*(\.\d{2})?)",
            RegexOptions.Singleline);

        var matches = patron.Matches(texto);

        foreach (Match match in matches)
        {
            transacciones.Add(new Transaccion
            {
                Fecha = match.Groups["fecha"].Value.Trim(),
                Descripcion = match.Groups["descripcion"].Value.Trim(),
                Monto = match.Groups["monto"].Value.Trim()
            });
        }

        return transacciones;
    }


    public List<Movimiento> ExtraerTransacciones2(string texto)
    {
        var movimientos = new List<Movimiento>();

        string pattern = @"(?m)^(?<Fecha>\d{2}/\d{2}/\d{2})\s+(?<Descripcion>[A-Z0-9*' ]+)\s+(?<Referencia>[A-Z0-9]+)?\s*(?<Ciudad>[A-Z ]+)\s+(?<Estado>[A-Z]{2})\s*\r?\n?(?<Telefono>\d{7,12})?\s*\r?\n?(?<Categoria>[A-Z ]+)?\s*\r?\n?\$(?<Monto>\d+\.\d{2})";

        var matches = Regex.Matches(texto, pattern);

        foreach (Match match in matches)
        {
            Console.WriteLine($"Fecha: {match.Groups["Fecha"].Value}");
            Console.WriteLine($"Descripcion: {match.Groups["Descripcion"].Value}");
            Console.WriteLine($"Referencia: {match.Groups["Referencia"].Value}");
            Console.WriteLine($"Ciudad: {match.Groups["Ciudad"].Value}");
            Console.WriteLine($"Estado: {match.Groups["Estado"].Value}");
            Console.WriteLine($"Telefono: {match.Groups["Telefono"].Value}");
            Console.WriteLine($"Categoria: {match.Groups["Categoria"].Value}");
            Console.WriteLine($"Monto: {match.Groups["Monto"].Value}");
            Console.WriteLine("------");
        }
        return movimientos;
    }





    public FileResult GenerarArchivoTexto1(List<Transaccion> transacciones)
    {
        var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.WriteLine("Fecha\t\tDescripción 1\t\tDescripción 2\t\tMonto");
        writer.WriteLine(new string('-', 100));

        foreach (var t in transacciones)
        {
            writer.WriteLine($"{t.Fecha}\t{t.Linea1}\t{t.Linea2}\t{t.Monto}");
        }

        writer.Flush();
        stream.Position = 0;

        return File(stream, "text/plain", "transacciones.txt");
    }


    public FileResult GenerarArchivoTexto(List<Transaccion> transacciones)
    {
        var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.WriteLine("Fecha\t\tDescripción\t\tMonto");
        writer.WriteLine(new string('-', 80));

        foreach (var t in transacciones)
        {
            writer.WriteLine($"{t.Fecha}\t{t.Descripcion}\t{t.Monto}");
        }

        writer.Flush();
        stream.Position = 0;

        return File(stream, "text/plain", "transacciones.txt");
    }

    public FileResult GenerarArchivoTexto1x(List<Transaccion1> transacciones1)
    {
        var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.WriteLine("Fecha\t\tDescripción 1\t\tDescripción 2\t\tEstado\t\tMonto");
        writer.WriteLine(new string('-', 120));

        foreach (var t in transacciones1)
        {
            writer.WriteLine($"{t.Fecha}\t{t.Linea1}\t{t.Linea2}\t{t.Estado}\t{t.Monto}");
        }

        writer.Flush();
        stream.Position = 0;

        return File(stream, "text/plain", "transacciones1.txt");
    }



}
