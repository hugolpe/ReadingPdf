using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ReadingPdf.Areas.Terceros.Models;
using ReadingPdf.Data;
using ReadingPdf.Library;
using ReadingPdf.Models;

namespace ReadingPdf.Areas.Terceros.Controllers
{


    [Area("Terceros")]
    public class TercerosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly LTerceros _terceros;

        int cantidadRegistros = 10;
        private DataPaginador<InputModelTerceros> models;

        public TercerosController(ApplicationDbContext context, LTerceros terceros)
        {
            _context = context;
            _terceros = terceros;  // ✅ Ahora sí tiene una instancia
        }

        public IActionResult Terceros(int id, string filtrar, int? tipoTerceroId)
        {
            object[] objects = new object[3];

            var data = _terceros.GetTTerceros(filtrar, 0, tipoTerceroId ?? 0);

            if (data != null && data.Count > 0)
            {
                var url = $"{Request.Scheme}://{Request.Host.Value}";
                objects = new LPaginador<InputModelTerceros>()
                         .Paginador(data, id, cantidadRegistros, "Terceros", "Terceros", "Terceros", url);
            }
            else
            {
                objects[0] = "No hay datos que mostrar";
                objects[1] = "No hay datos que mostrar";
                objects[2] = new List<InputModelTerceros>();
            }

            //// ✅ Cargar tipos de terceros para el dropdown
            //ViewBag.tipoTerceroId = _context.TTiposGlobal

            //    .Where(t => t.TGrupo == "TERCEROS-TIP")
            //    .Select(t => new SelectListItem
            //    {
            //        Value = t.TTipoGId.ToString(),
            //        Text = t.TTipoGNombre,
            //        Selected = t.TTipoGId == (tipoTerceroId ?? 0)
            //    })
            //    .ToList();

            models = new DataPaginador<InputModelTerceros>
            {
                List = (List<InputModelTerceros>)objects[2],
                Pagi_info = (string)objects[0],
                Pagi_navegacion = (string)objects[1],
                Input = new InputModelTerceros
                {
                    TipoTerceroId = tipoTerceroId ?? 0,
                }
            };

            return View(models);
        }

    }

}
