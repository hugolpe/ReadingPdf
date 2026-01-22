using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ReadingPdf.Models
{
    public class Proceso
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Nombre { get; set; }

        [MaxLength(100)]
        public string Banco { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Saldo inicial (ej: Interest Charged o Beginning Balance)
        /// </summary>
        public decimal? SaldoInicial { get; set; }

        /// <summary>
        /// Cuenta QuickBooks seleccionada al inicio
        /// </summary>
        [MaxLength(200)]
        public string CuentaQBSeleccionada { get; set; }

        /// <summary>
        /// Abierto | Cerrado | Exportado
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Estado { get; set; } = "Abierto";

        // ==========================
        // Relaciones
        // ==========================
        public ICollection<MovimientoProceso> Movimientos { get; set; } = new List<MovimientoProceso>();
    }
}
