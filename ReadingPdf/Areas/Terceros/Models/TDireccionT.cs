//using ReadingPdf.Areas.Configuracion.Models;
using ReadingPdf.Areas.Terceros.Models;

using System.ComponentModel.DataAnnotations;

namespace ReadingPdf.Areas.Terceros.Models
{
    public class TDireccionT
    {
        [Key]
        public int IdDirection { get; set; }
        public string DirDescrip { get; set; }
        public string Direccion { get; set; }
        public int CodCiudad { get; set; }
        public TTerceros TTerceros { get; set; } = null!; // Required reference navigation to principal

        public int TTercerosIdTercero { get; internal set; }



        
    }
}
