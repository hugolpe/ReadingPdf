using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReadingPdf.Data;
using ReadingPdf.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ReadingPdf.Controllers
{
    [ApiController]
    [Route("api/procesos")]
    public class ProcesosApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public ProcesosApiController(ApplicationDbContext db)
        {
            _db = db;
        }



        // =====================================================
        // 1️⃣ Crear nuevo proceso
        // =====================================================
        [HttpPost]
        public async Task<IActionResult> CrearProceso([FromBody] CrearProcesoDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                return BadRequest("El nombre del proceso es obligatorio");

            var proceso = new Proceso
            {
                Id = Guid.NewGuid(),
                Nombre = dto.Nombre,
                Banco = dto.Banco,
                SaldoInicial = dto.SaldoInicial,
                CuentaQBSeleccionada = dto.CuentaQB,
                Estado = "Abierto"
            };

            _db.Procesos.Add(proceso);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                proceso.Id,
                proceso.Nombre,
                proceso.Estado
            });
        }

        // =====================================================
        // 2️⃣ Guardar movimientos del proceso (snapshot)
        // =====================================================
        //[HttpPost("{procesoId}/movimientos")]
        //public async Task<IActionResult> GuardarMovimientos(
        //    Guid procesoId,
        //    [FromBody] List<MovimientoProcesoDto> movimientos)
        //{
        //    var proceso = await _db.Procesos.FindAsync(procesoId);
        //    if (proceso == null)
        //        return NotFound("Proceso no encontrado");

        //    // Limpiar snapshot previo (permite re-guardar)
        //    var existentes = _db.MovimientosProceso.Where(m => m.ProcesoId == procesoId);
        //    _db.MovimientosProceso.RemoveRange(existentes);

        //    var nuevos = movimientos.Select(m => new MovimientoProceso
        //    {
        //        ProcesoId = procesoId,
        //        Fecha = m.Fecha,
        //        Empresa = m.Empresa,
        //        EmpresaOriginal = m.EmpresaOriginal,
        //        Descripcion = m.Descripcion,
        //        Monto = m.Monto,
        //        CuentaPredicha = m.CuentaPredicha,
        //        CuentaAplicada = m.CuentaAplicada,
        //        ScorePrediccion = m.ScorePrediccion,
        //        TipoDocumento = m.TipoDocumento,
        //        RegistradoQB = m.RegistradoQB,
        //        QuickBooksTxnId = m.QuickBooksTxnId
        //    });

        //    _db.MovimientosProceso.AddRange(nuevos);
        //    await _db.SaveChangesAsync();

        //    return Ok(new { guardados = movimientos.Count });
        //}

        [HttpPost("{procesoId}/movimientos")]
        public async Task<IActionResult> GuardarMovimientos(
    Guid procesoId,
    [FromBody] List<MovimientoProcesoDto> movimientos)
        {
            try
            {
                var proceso = await _db.Procesos.FindAsync(procesoId);
                if (proceso == null)
                    return NotFound("Proceso no encontrado");

                var existentes = _db.MovimientosProceso
                    .Where(m => m.ProcesoId == procesoId);

                _db.MovimientosProceso.RemoveRange(existentes);

                var nuevos = movimientos.Select(m => new MovimientoProceso
                {
                    ProcesoId = procesoId,
                    Fecha = m.Fecha,
                    Empresa = m.Empresa,
                    EmpresaOriginal = m.EmpresaOriginal,
                    Descripcion = m.Descripcion,
                    Monto = m.Monto,
                    CuentaPredicha = m.CuentaPredicha,
                    CuentaAplicada = m.CuentaAplicada,
                    ScorePrediccion = m.ScorePrediccion,
                    TipoDocumento = m.TipoDocumento,
                    RegistradoQB = m.RegistradoQB,
                    QuickBooksTxnId = m.QuickBooksTxnId
                }).ToList();

                _db.MovimientosProceso.AddRange(nuevos);
                await _db.SaveChangesAsync();

                return Ok(new { guardados = nuevos.Count });
            }
            catch (Exception ex)
            {
                // 🔥 ESTO ES LO QUE NECESITAMOS VER
                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message,
                    stack = ex.StackTrace
                });
            }
        }


        // =====================================================
        // 3️⃣ Listar procesos (histórico)
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> ListarProcesos()
        {
            var procesos = await _db.Procesos
                .OrderByDescending(p => p.FechaCreacion)
                .Select(p => new
                {
                    p.Id,
                    p.Nombre,
                    p.Banco,
                    p.FechaCreacion,
                    p.Estado,
                    TotalMovimientos = p.Movimientos.Count
                })
                .ToListAsync();

            return Ok(procesos);
        }

        // =====================================================
        // 4️⃣ Reabrir proceso (recupera movimientos)
        // =====================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> ObtenerProceso(Guid id)
        {
            var proceso = await _db.Procesos
                .Include(p => p.Movimientos)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proceso == null)
                return NotFound();

            return Ok(proceso);
        }

        // =====================================================
        // 5️⃣ Cerrar proceso
        // =====================================================
        [HttpPost("{id}/cerrar")]
        public async Task<IActionResult> CerrarProceso(Guid id)
        {
            var proceso = await _db.Procesos.FindAsync(id);
            if (proceso == null)
                return NotFound();

            proceso.Estado = "Cerrado";
            await _db.SaveChangesAsync();

            return Ok(new { proceso.Id, proceso.Estado });
        }

        // =====================================================
        // 6️⃣ Marcar movimiento como registrado en QuickBooks
        // =====================================================
        [HttpPost("movimiento/{id}/registrado")]
        public async Task<IActionResult> MarcarRegistradoQB(
            int id,
            [FromBody] MarcarQBRequest dto)
        {
            var mov = await _db.MovimientosProceso.FindAsync(id);
            if (mov == null)
                return NotFound();

            mov.RegistradoQB = true;
            mov.QuickBooksTxnId = dto.QuickBooksTxnId;

            await _db.SaveChangesAsync();

            return Ok();
        }
    }

    // =====================================================
    // DTOs
    // =====================================================

    public class CrearProcesoDto
    {
        public string Nombre { get; set; }
        public string Banco { get; set; }
        public decimal? SaldoInicial { get; set; }
        public string CuentaQB { get; set; }
    }

    public class MovimientoProcesoDto
    {
        public DateTime Fecha { get; set; }
        public string Empresa { get; set; }
        public string EmpresaOriginal { get; set; }
        public string Descripcion { get; set; }
        public decimal Monto { get; set; }
        public string CuentaPredicha { get; set; }
        public string CuentaAplicada { get; set; }
        public int? ScorePrediccion { get; set; }
        public string TipoDocumento { get; set; }
        public bool RegistradoQB { get; set; }
        public string QuickBooksTxnId { get; set; }
    }

    public class MarcarQBRequest
    {
        public string QuickBooksTxnId { get; set; }
    }
}
