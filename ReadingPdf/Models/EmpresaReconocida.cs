using System;

namespace ReadingPdf.Models
{
    public class EmpresaReconocida
    {
        public int Id { get; set; }
        public string TextoOriginal { get; set; } = "";
        public DateTime FechaRegistro { get; set; }
    }
}