using System.ComponentModel.DataAnnotations;
using ReadingPdf.Models;

namespace ReadingPdf.Areas.Terceros.Models
{
    public class TTerceros
    {
        [Key]
        public int IdTercero { get; set; }

        public int TipoTerceroId { get; set; }

        public int IdTipTercero { get; set; }
        public string Nid { get; set; }
        public int IdTipDocumento { get; set; }
        public string Name { get; set; }
        public string NombreComercial { get; set; }
        public string Email { get; set; }
        public int IdDirection { get; set; }
        public int TelefonoId { get; set; }

        public string Phone { get; set; }
        public bool Credit { get; set; }
        public byte[] Image { get; set; }

        
        public List<TDireccionT> DireccionT { get; set; }

        


        public int TercCodContId { get; set; }
        public bool PorDefecto { get; set; }

        public bool TercModVenta { get; set; }

        public bool TercModCompras { get; set; }

        public bool TercModHis { get; set; }
        public bool TercModNomina { get; set; }

        public bool TerConPoliza { get; set; }

        public bool TerConVehiculo { get; set; }

        public int TerPolComId { get; set; }

        public string TerPoliza { get; set; }

        public DateTime TerPolFecVen { get; set; }


    }
}
