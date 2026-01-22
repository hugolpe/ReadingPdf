namespace ReadingPdf.Models
{
    public class Transaccion1
    {
        public string Fecha { get; set; }
        public string Linea1 { get; set; }   // primera línea de la descripción
        public string Linea2 { get; set; }   // segunda línea de la descripción
        public string Estado { get; set; }   // nuevo campo para estado
        public string Monto { get; set; }
    }
}
