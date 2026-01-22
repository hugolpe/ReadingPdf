namespace ReadingPdf.Models
{
    public class MovimientoEntrenamiento
    {
        public int Id { get; set; }

        public int EmpresaId { get; set; }

        public string Memo { get; set; } = string.Empty;

        public string? Nombre { get; set; }

        // LABEL
        public string CuentaContable { get; set; } = string.Empty;

        // Solo si el usuario confirmó o corrigió
        public bool Confirmado { get; set; }

        // Para evitar re-entrenar lo mismo
        public bool Entrenado { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}
