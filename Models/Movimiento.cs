public class Movimiento
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
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public string Documento { get; set; }
    public string Pasajero { get; set; }
    public string TipoDocumento { get; set; }
    public string Notas { get; set; }
    public string Empresa { get; set; }
    public string CuentaPredicha { get; set; }
    public int ScorePrediccion { get; set; }
    public string EnrichedIndustry { get; set; }
    public string EnrichedSubIndustry { get; set; }
    public string EnrichedSector { get; set; }
    public string EnrichedNaics { get; set; }
    public string EnrichedTags { get; set; }
    public string EnrichedCompanyDomain { get; set; }
    public string CuentaContableAplicada { get; set; }
    public string EmpresaExtraida { get; internal set; }
    public string CuentaContable { get; internal set; }
    public TipoMovimiento Tipo { get; set; }
    public int? BancoId { get; internal set; }
    public Guid ProcesoId { get; set; }
    public string? CuentaAplicada { get; set; }
    public decimal Debito { get; set; }
    public decimal Credito { get; set; }

    // Add this property to fix CS0117
    public string EmpresaOriginal { get; set; }
}public class Movimiento
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
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public string Documento { get; set; }
    public string Pasajero { get; set; }
    public string TipoDocumento { get; set; }
    public string Notas { get; set; }
    public string Empresa { get; set; }
    public string CuentaPredicha { get; set; }
    public int ScorePrediccion { get; set; }
    public string EnrichedIndustry { get; set; }
    public string EnrichedSubIndustry { get; set; }
    public string EnrichedSector { get; set; }
    public string EnrichedNaics { get; set; }
    public string EnrichedTags { get; set; }
    public string EnrichedCompanyDomain { get; set; }
    public string CuentaContableAplicada { get; set; }
    public string EmpresaExtraida { get; internal set; }
    public string CuentaContable { get; internal set; }
    public TipoMovimiento Tipo { get; set; }
    public int? BancoId { get; internal set; }
    public Guid ProcesoId { get; set; }
    public string? CuentaAplicada { get; set; }
    public decimal Debito { get; set; }
    public decimal Credito { get; set; }

    // Add this property to fix CS0117
    public string EmpresaOriginal { get; set; }
}