using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using ReadingPdf.Data;
using ReadingPdf.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ReadingPdf.Controllers
{
    public class EmpresaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmpresaController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Empresa
        public IActionResult Index()
        {
            var empresas = _context.EmpresasReconocidas.ToList();
            return View(empresas);
        }

        [HttpPost]
        public async Task<IActionResult> Aprender([FromBody] SeleccionDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.TextoSeleccionado))
                return BadRequest("Selección vacía");

            var empresa = new EmpresaReconocida
            {
                TextoOriginal = dto.TextoSeleccionado.Trim(),
                FechaRegistro = DateTime.UtcNow
            };

            _context.EmpresasReconocidas.Add(empresa);
            await _context.SaveChangesAsync();

            return Json(new { success = true, empresa = empresa.TextoOriginal });
        }

        // 🔹 NUEVO DTO para QuickBooks -> tabla Empresas
        public class EmpresaQuickBooksDto
        {
            public string Nombre { get; set; }
        }

        // 🔹 NUEVA ACCIÓN: guarda en tabla Empresas (NO en EmpresasReconocidas)
        //[HttpPost]
        //public async Task<IActionResult> GuardarDesdeQuickBooks([FromBody] EmpresaQuickBooksDto dto)
        //{
        //    if (dto == null || string.IsNullOrWhiteSpace(dto.Nombre))
        //        return BadRequest("Nombre de empresa vacío");

        //    var nombre = dto.Nombre.Trim();

        //    // Buscar ignorando mayúsculas/minúsculas
        //    var existente = await _context.Empresas
        //        .FirstOrDefaultAsync(e => e.EmpresaNombre.ToUpper() == nombre.ToUpper());

        //    if (existente != null)
        //    {
        //        return Json(new
        //        {
        //            success = true,
        //            Id = existente.Id, // ajusta si tu PK se llama distinto
        //            EmpresaNombre = existente.EmpresaNombre,
        //            creada = false
        //        });
        //    }

        //    // Crear nueva en tabla Empresas
        //    var empresa = new Empresa
        //    {
        //        EmpresaNombre = nombre,
        //        FechaCreacion = DateTime.UtcNow   // si tu modelo tiene esta columna
        //    };

        //    _context.Empresas.Add(empresa);
        //    await _context.SaveChangesAsync();

        //    return Json(new
        //    {
        //        success = true,
        //        empresaId = empresa.Id,
        //        nombre = empresa.EmpresaNombre,
        //        creada = true
        //    });
        //}

        [HttpPost]
        public async Task<IActionResult> GuardarDesdeQuickBooks([FromBody] EmpresaQuickBooksDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Nombre))
                return BadRequest("Nombre de empresa vacío");

            var nombre = dto.Nombre.Trim();

            // Buscar ignorando mayúsculas/minúsculas
            var existente = await _context.Empresas
                .FirstOrDefaultAsync(e => e.EmpresaNombre.ToUpper() == nombre.ToUpper());

            if (existente != null)
            {
                return Json(new
                {
                    success = true,
                    empresaId = existente.Id,
                    nombre = existente.EmpresaNombre,
                    creada = false
                });
            }

            var empresa = new Empresa
            {
                EmpresaNombre = nombre,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Empresas.Add(empresa);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                empresaId = empresa.Id,
                nombre = empresa.EmpresaNombre,
                creada = true
            });
        }

    }
}
