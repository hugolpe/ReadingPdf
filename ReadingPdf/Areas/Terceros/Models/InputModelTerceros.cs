using Microsoft.AspNetCore.Mvc;
using ReadingPdf.Areas.Terceros.Models;
using ReadingPdf.Models;
using System.ComponentModel.DataAnnotations;


namespace ReadingPdf.Areas.Terceros.Models
{
    public class InputModelTerceros
    {

        public int IdTercero { get; set; }
        public int TipoTerceroId { get; set; }
        //[Required(ErrorMessage = "El campo Nit / NRo. Documento es obligatorio.")]
        //[RegularExpression("^[0-9]+$", ErrorMessage = "El campo Nit / NRo. Documento debe contener solo números.")]
        public string Nid { get; set; }

        //[Required(ErrorMessage = "El campo Tipo de Documento es obligatorio.")]

        public int IdTipDocumento { get; set; }

        public int IdTipTercero { get; set; }
        //[Required(ErrorMessage = "El campo nombre es obligatorio.")]
        public string Name { get; set; }
        //[Required(ErrorMessage = "El campo apellido es obligatorio.")]
        public string NombreComercial { get; set; }
        //[Required(ErrorMessage = "El campo Email es obligatorio.")]
        public string Email { get; set; }
        //[Required(ErrorMessage = "El campo Direccion es obligatorio.")]
        public int IdDirection { get; set; }

        public bool TerConPoliza { get; set; }

        public int TerPolComId { get; set; }

        public string TerPoliza { get; set; }

        public DateTime TerPolFecVen { get; set; }



        [Required(ErrorMessage = "El campo telefono es obligatorio.")]
        [DataType(DataType.PhoneNumber)]
        [RegularExpression(@"^\(?([0-9]{3})\)?[-. ]?([0-9]{2})[-. ]?([0-9]{5})$", ErrorMessage = "El formato de telefono no es valido.")]

        public string Phone { get; set; }

        //[Required(ErrorMessage = "El campo Credito es obligatorio.")]
        public bool Credit { get; set; }

        public byte[] Image { get; set; }


        public List<TDireccionT> DireccionT { get; set; }


        public int IdCliente { get; set; }

        public int TercCodContId { get; set; }

        public string NombreTipoTercero { get; set; }
        public string NombreTipoDocumento { get; set; }

        public string NombreTipTercero { get; set; }

        public bool TercModVenta { get; set; }

        public bool TercModCompras { get; set; }

        public bool TercModHis { get; set; }
        public bool TercModNomina { get; set; }

        public bool TerEmbarazo { get; set; }
        public bool TerEmergencia { get; set; }

        public bool TerEntregaRec { get; set; }

        public bool TerConVehiculo { get; set; }

        public int TerSemanasEmb { get; set; }

        public string TerReferido { get; set; }

        public string TerNcf { get; set; }

        public decimal TerValorComp { get; set; }

        public int TerMedCab { get; set; }

        public string TerMedAti { get; set; }

        public string TerMedTec { get; set; }

        public decimal TerValReclamo { get; set; }

        public decimal TerValPagMed { get; set; }



        [TempData]
        public string ErrorMessage { get; set; }
        //public object Id { get; internal set; }
        //public object NombreDoc { get; internal set; }
        //public object Contratos { get; internal set; }

        public int IdTipoDoc { get; internal set; }
        public bool PorDefecto { get; set; }

        public DateTime pFechaIni { get; set; }
        public DateTime pFechaFin { get; set; }

        public string TerDireccionVacia { get; set; }


    }



        //public DateTime? pFechaIni { get; set; }
        //public DateTime? pFechaFin { get; set; }
        //public object Input { get; internal set; }


    public class InputModelDireccionT
    {

        public string DirDescrip { get; set; }

        public string DireccionT { get; set; }

        public int CodCiudad { get; set; }
    }

    public class InputModelVehiculosT
    {

        public int VehId { get; set; }

        public string VehPlaca { get; set; }
        public int VehTipo { get; set; }
        public int VehTarifa { get; set; }
        public bool VehEstado { get; set; }
        public bool VehArriendo { get; set; }

        public bool VehPropietario { get; set; }

        public DateTime VehFecFact { get; set; }

        public DateTime VehFecCorte { get; set; }

        public decimal VehVrMensual { get; set; }

    }

}



