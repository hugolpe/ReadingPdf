using ReadingPdf.Areas.Terceros.Models;

namespace ReadingPdf.Models
{
    public class DataPaginador<T>
    {
        public List<T> List { get; set; }
        public string Pagi_info { get; set; }
        public string Pagi_navegacion { get; set; }
        public T Input { get; set; }
        public string Search { get; set; }
        //public List<SelectList> ListaTipProv { get; internal set; }
        public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> ListaTipPro1 { get; internal set; }
        public string Placa { get; internal set; }
        public DateTime Fecha_entrada { get; internal set; }
        public DateTime Fecha_salida { get; internal set; }
        public decimal ValorPago { get; internal set; }

        public static implicit operator DataPaginador<T>(DataPaginador<InputModelTerceros> v)
        {
            throw new NotImplementedException();
        }
    }
}
