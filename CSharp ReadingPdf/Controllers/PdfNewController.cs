// Reemplaza el bloque de extracción en el bucle foreach por:
var extracted = ExtractNameFromMemo(mov.Descripcion, BancoId);
mov.EmpresaExtraida = extracted; // siempre guardar para depuración / Excel
if (string.IsNullOrWhiteSpace(mov.Empresa) && !string.IsNullOrWhiteSpace(extracted))
{
    mov.Empresa = extracted;
}

// (Opcional) si quieres ver por consola durante pruebas:
if (!string.IsNullOrWhiteSpace(mov.EmpresaExtraida))
{
    Console.WriteLine($"Extracted company: {mov.EmpresaExtraida} from memo: {mov.Descripcion}");
}// Reemplaza el bloque de extracción en el bucle foreach por:
var extracted = ExtractNameFromMemo(mov.Descripcion, BancoId);
mov.EmpresaExtraida = extracted; // siempre guardar para depuración / Excel
if (string.IsNullOrWhiteSpace(mov.Empresa) && !string.IsNullOrWhiteSpace(extracted))
{
    mov.Empresa = extracted;
}

// (Opcional) si quieres ver por consola durante pruebas:
if (!string.IsNullOrWhiteSpace(mov.EmpresaExtraida))
{
    Console.WriteLine($"Extracted company: {mov.EmpresaExtraida} from memo: {mov.Descripcion}");
}