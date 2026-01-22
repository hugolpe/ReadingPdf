using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReadingPdf.Models
{
    [Table("MovimientoHistorico")]
    public class MovimientoHistorico
    {
        [Key]
        public int Id { get; set; }

        // Optional FK to an Empresa table (nullable to allow importing historic rows
        // that don't have a matched Empresa record yet).
        public int? EmpresaId { get; set; }

        // Company name used by the trainer. Training code expects a "company/name"
        // value so expose it explicitly.
        [Required]
        [MaxLength(250)]
        public string EmpresaNombre { get; set; } = "";

        // Memo (description) used as the text feature for training.
        [Required]
        [MaxLength(1000)]
        public string Memo { get; set; } = "";

        // Label / account to learn.
        [Required]
        [MaxLength(100)]
        public string CuentaContable { get; set; } = "";

        // Optional navigation property if you have an Empresa entity.
        // Change type to your concrete Empresa class if available.
        [ForeignKey(nameof(EmpresaId))]
        public virtual object? Empresa { get; set; }

        // Timestamps help trace training data provenance.
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

}