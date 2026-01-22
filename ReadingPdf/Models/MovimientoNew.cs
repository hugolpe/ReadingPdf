namespace ReadingPdf.Models
{
    public class MovimientoNew
    {
        public DateTime Fecha { get; set; }
        public string Descripcion { get; set; }
        public string Ciudad { get; set; }
        public string Estado { get; set; }
        public string Telefono { get; set; }
        public string Categoria { get; set; }
        public decimal Monto { get; set; }
        public string Documento { get; set; }
        public string Pasajero { get; set; }
        public string TipoDocumento { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string Notas { get; set; }

        public string Empresa { get; set; }
    }
}
