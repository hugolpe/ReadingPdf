using ReadingPdf.Models;
using Microsoft.EntityFrameworkCore; // Add this if needed for DbContext

namespace ReadingPdf.Services
{
    public class ClasificacionService
    {
        private readonly DbContext _db; // Replace DbContext with your actual context type

        public ClasificacionService(DbContext db) // Replace DbContext with your actual context type
        {
            _db = db;
        }

        public async Task RegistrarMovimientoConfirmadoAsync(
            int empresaId,
            string memo,
            string? nombre,
            string cuentaContable)
        {
            var mov = new MovimientoEntrenamiento
            {
                EmpresaId = empresaId,
                Memo = memo,
                Nombre = nombre,
                CuentaContable = cuentaContable,
                Confirmado = true,
                Entrenado = false
            };

            _db.Set<MovimientoEntrenamiento>().Add(mov); // Use Set<T>() if DbSet is not directly available
            await _db.SaveChangesAsync();
        }
    }
}
