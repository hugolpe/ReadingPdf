using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReadingPdf.Data;

public class ProcesosController : Controller
{
    private readonly ApplicationDbContext _db;

    public ProcesosController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var procesos = await _db.Procesos
            .Include(p => p.Movimientos)
            .OrderByDescending(p => p.FechaCreacion)
            .ToListAsync();

        return View(procesos);
    }

    [HttpGet]
    public async Task<IActionResult> Cerrar(Guid id)
    {
        var proceso = await _db.Procesos.FindAsync(id);
        if (proceso == null) return NotFound();

        proceso.Estado = "Cerrado";
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
