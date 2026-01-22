using System.ComponentModel.DataAnnotations.Schema;

namespace ReadingPdf.Models
{
    // Partial class to add a non-mapped helper property for extraction/trazabilidad.
    public partial class Movimiento
    {
        // Computed/auxiliary fields for UI and processing.
        // Not mapped to DB if you're using EF.
        [NotMapped]
        public decimal Debito { get; set; }

        [NotMapped]
        public decimal Credito { get; set; }
    }
}