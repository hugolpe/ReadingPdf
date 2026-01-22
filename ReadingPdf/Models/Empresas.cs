using System;
using System.ComponentModel.DataAnnotations;

namespace ReadingPdf.Models
{
    // Entity model used across the app (DbSet named `Empresas` and code expects a type `Empresa`)
    public class Empresa
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(250)]
        public string EmpresaNombre { get; set; } = string.Empty;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}