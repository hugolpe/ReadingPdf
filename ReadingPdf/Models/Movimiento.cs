using System;

namespace ReadingPdf.Models
{
    public partial class Movimiento
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public string Descripcion { get; set; }
        public string Referencia { get; set; }
        public string Ciudad { get; set; }
        public string Estado { get; set; }
        public string Telefono { get; set; }
        public string Categoria { get; set; }
        public string Detalle { get; set; }
        public string ZipCode { get; set; }
        public decimal Monto { get; set; }
        public string Moneda { get; set; }

        // Campos especiales
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string Documento { get; set; }
        public string Pasajero { get; set; }
        public string TipoDocumento { get; set; }
        public string Notas { get; set; }
        public string Empresa { get; set; }
        public string CuentaPredicha { get; set; }
        public int ScorePrediccion { get; set; }

        // Enriquecimiento Clearbit
        public string EnrichedIndustry { get; set; }
        public string EnrichedSubIndustry { get; set; }
        public string EnrichedSector { get; set; }
        public string EnrichedNaics { get; set; }
        public string EnrichedTags { get; set; }
        public string EnrichedCompanyDomain { get; set; }

        // Cuenta contable final
        public string CuentaContableAplicada { get; set; }

        public object EmpresaExtraida { get; internal set; }
        public string CuentaContable { get; internal set; }

        // 🔥 NUEVO: Crédito / Débito
        public TipoMovimiento Tipo { get; set; }
    }

    // Enum correctamente definido en el namespace
    public enum TipoMovimiento
    {
        Credito,
        Debito
    }
}
