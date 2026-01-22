using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReadingPdf.Models
{
    public class MovimientoProceso
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid ProcesoId { get; set; }

        [ForeignKey(nameof(ProcesoId))]
        public Proceso Proceso { get; set; }

        // ==========================
        // Datos del movimiento
        // ==========================
        [Required]
        public DateTime Fecha { get; set; }

        [MaxLength(200)]
        public string Empresa { get; set; }

        [MaxLength(200)]
        public string EmpresaOriginal { get; set; }

        [MaxLength(500)]
        public string Descripcion { get; set; }

        [Required]
        public decimal Monto { get; set; }

        // ==========================
        // Clasificación / ML
        // ==========================
        [MaxLength(200)]
        public string CuentaPredicha { get; set; }

        [MaxLength(200)]
        public string CuentaAplicada { get; set; }

        public int? ScorePrediccion { get; set; }

        // ==========================
        // Documento / Estado
        // ==========================
        [MaxLength(100)]
        public string TipoDocumento { get; set; }

        /// <summary>
        /// Indica si ya fue registrado en QuickBooks
        /// </summary>
        public bool RegistradoQB { get; set; } = false;

        /// <summary>
        /// TxnID devuelto por QuickBooks (si aplica)
        /// </summary>
        [MaxLength(100)]
        public string QuickBooksTxnId { get; set; }
    }
}
