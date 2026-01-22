using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using ReadingPdf.Areas.Terceros.Models;
using ReadingPdf.Data;

using ReadingPdf.Library;


namespace ReadingPdf.Areas.Terceros.Pages.Account
{
    public class TercerosRegModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context; //Cubierto
        //private readonly LTipDocDrop _tipodoc;
        //private readonly LCiudadesDrop _ciudades;
        private static InputModel _dataInput;
        //private readonly Uploadimage _uploadImage;
        private static InputModelTerceros _dataClient1, _dataClient2;
        private readonly IWebHostEnvironment _environment;
        //private readonly LClientesn _clientes;
        //public readonly InputModelDireccionT _dataDirection;
        //private readonly LConPlanCuentaDrop _cuentas;
        //private readonly LTTiposGlobalDrop _personaTipo;
        //private LProveedor _proveedores;
        //private readonly LTipDocComDrop _tipodoccom;
        //private readonly LArticulosDrop _articulos;
        private readonly LTerceros _terceros;
        //private readonly LHisEstudios _estudios;



        public TercerosRegModel(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,

        IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _environment = environment;
            //_uploadImage = new Uploadimage();
            //_clientes = new LClientesn(context);
            //_tipodoc = new LTipDocDrop(context);
            //_ciudades = new LCiudadesDrop(context);
            //_cuentas = new LConPlanCuentaDrop(context);
            //_personaTipo = new LTTiposGlobalDrop(context);
            //_proveedores = new LProveedor(context);
            //_tipodoccom = new LTipDocComDrop(context);
            //_articulos = new LArticulosDrop(context);
            _terceros = new LTerceros(context);
            //_estudios = new LHisEstudios(context);
            //_direcciones = new LDirecciones(context);
        }




        public TTerceros TTerceros { get; set; }


        public void OnGet(int id)
        {
            _dataClient2 = null;
            if (id.Equals(0))
            {
                _dataClient2 = null;
                _dataInput = null;
            }
            if (_dataInput != null || _dataClient1 != null || _dataClient2 != null)
            {
                if (_dataInput != null)
                {
                    Input = _dataInput;
                    Input.AvatarImage = null;
                    //Input.Image = _dataClient2.Image;
                }
                else
                {
                    if (_dataClient1 != null || _dataClient2 != null)
                    {
                        if (_dataClient2 != null)
                            _dataClient1 = _dataClient2;
                        Input = new InputModel
                        {
                            IdTercero = _dataClient1.IdTercero,
                            Name = _dataClient1.Name,
                            NombreComercial = _dataClient1.NombreComercial,
                            IdTipTercero = _dataClient1.IdTipTercero,
                            TipoTerceroId = _dataClient1.TipoTerceroId,
                            Nid = _dataClient1.Nid,
                            IdTipDocumento = _dataClient1.IdTipDocumento,
                            Email = _dataClient1.Email,
                            Image = _dataClient1.Image,
                            Phone = _dataClient1.Phone,
                            Credit = _dataClient1.Credit,
                            TercCodContId = _dataClient1.TercCodContId,
                            TercModCompras = _dataClient1.TercModCompras,
                            TercModHis = _dataClient1.TercModHis,
                            TercModNomina = _dataClient1.TercModNomina,
                            TercModVenta = _dataClient1.TercModVenta,
                            //DireccionT = _context.TDireccionT.Where(u => u.TTercerosIdTercero.Equals(_dataClient1.IdTercero)).ToList(),
                            //Comisiones = _dataClient1.Comisiones,
                            //TipodocLista = _tipodoc.getTipDoc(0),
                            //CiudadLista = _ciudades.getCiudad(0),
                            //CuentasLista = _cuentas.getTConPlanCuentas(0),
                            //TipoPerLista = _personaTipo.getTTiposGlobal("TERCEROS-TP", 0),
                            //TipoTerLista = _personaTipo.getTTiposGlobal("TERCEROS-TIP", 0),
                            //DocCompras = _proveedores.getTComprasDoc(null, _dataClient1.IdTercero),
                            //tipodocCompLista = _tipodoccom.getTipDocCom(0),
                            //articulosLista = _articulos.getLArticulos(0),
                            //DataTerceros = _terceros.GetTClientesyy(_dataClient1.IdTercero),
                            //listaMedicos = _terceros.getListaDoctors(41),
                            //listaEstudios = _estudios.getTEstudios(0),
                        };
                        if (_dataInput != null)
                        {
                            Input.ErrorMessage = _dataInput.ErrorMessage;
                        }
                    }
                }
            }
            else
            {
                Input = new InputModel
                {
                   //TipodocLista = _tipodoc.getTipDoc(0),
                   // CiudadLista = _ciudades.getCiudad(0),
                    DireccionT = new List<TDireccionT>(),
                    //Comisiones = new List<THisComisiones>(),
                    //CuentasLista = _cuentas.getTConPlanCuentas(0),
                    //TipoPerLista = _personaTipo.getTTiposGlobal("TERCEROS-TP", 0),
                    //TipoTerLista = _personaTipo.getTTiposGlobal("TERCEROS-TIP", 0),
                    //DocCompras = _proveedores.getTComprasDoc(null, id),
                    //articulosLista = _articulos.getLArticulos(0),
                    //tipodocCompLista = _tipodoccom.getTipDocCom(0),
                    //listaMedicos = _terceros.getListaDoctors(41),
                    nuevo = true,
                    proveedor = id,


                };
            }


            _dataClient2 ??= _dataClient1;
            _dataClient1 = null;

        }
        [BindProperty]

        public InputModel Input { get; set; }
        public string Name { get; private set; }

        public List<TDireccionT> Direccionx { get; set; }

        //public List<THisComisiones> Comisionesx { get; set; }

        //public List<TComprasDocDet> ComprasDetallex { get; set; }

        //public List<TTercerosHisMov> Movimientosx { get; set; }

        //public InputModelDocCompras Comprasx { get; set; }


        public class InputModel : InputModelTerceros
        {
            public IFormFile AvatarImage { get; set; }

            //public InputModelDocCompras Compras { get; set; }






            //public new List<InputModelDocumentoCompras> DocCompras { get; set; }

            public InputModelTerceros DataTerceros { get; set; }

            //public new List<TTercerosHisMov> Movimientos { get; set; }

            public int proveedor { get; set; }

            public bool nuevo { get; set; }

            [TempData]

            public List<SelectListItem> articulosLista { get; set; }
            public List<SelectListItem> TipodocLista { get; set; }

            public List<SelectListItem> tipodocCompLista { get; set; }


            public List<SelectListItem> TipoPerLista { get; set; }

            public List<SelectListItem> CiudadLista { get; set; }
            public List<SelectListItem> CuentasLista { get; set; }

            public List<SelectListItem> TipoTerLista { get; set; }

            public List<SelectListItem> listaMedicos { get; set; }

            public List<SelectListItem> listaEstudios { get; set; }



        }

               

        //public async Task<IActionResult> OnPostAsync(
        //    String dataClient1,
        //    //InputModelDocCompras compras,
        //    List<TDireccionT> direccion,
        //    //List<TComprasDocDet> ComprasDetalle,
        //    //List<TTercerosHisMov> Movimientos,
        //    //List<THisComisiones> comisiones
        //    )
        //{
        //    Direccionx = direccion;
        //    //Comisionesx = comisiones;
        //    //ComprasDetallex = ComprasDetalle;
        //    //Movimientosx = Movimientos;


        //    //List<TComprasDoc> Comprasx = new List<TComprasDoc>();

        //    //if (Input.Compras != null)
        //    //{
        //    //    Comprasx.Add(new TComprasDoc
        //    //    {
        //    //        IdProveedor = Input.Compras.IdProveedor,
        //    //        IdTipDocCompras = Input.Compras.IdTipDocCompras,
        //    //        CompNroDoc = Input.Compras.CompNroDoc,
        //    //        CompFecha = Input.Compras.CompFecha,
        //    //        CompDescripcion = Input.Compras.CompDescripcion,
        //    //        VrDocumento = Input.Compras.VrDocumento,
        //    //        CompComprobante = Input.Compras.CompComprobante,
        //    //        tmodulo = "TERCEROS",
        //    //        TesForPagId = 1,
        //    //    });
        //    //}






        //    // Por ejemplo, eliminar direcciones con código postal vacío
        //    //Direccionx.RemoveAll(d => d.IdDirection == 0);

        //    //if (dataClient1 == null || ComprasDetalle.Count != 0)
        //    //{
        //    //    if (_dataClient2 == null)
        //    //    {
        //    //        if (await SaveAsync())
        //    //        {
        //    //            _dataClient2 = null;
        //    //            _dataClient1 = null;
        //    //            _dataInput = null;
        //    //            return Redirect("/Terceros/Terceros?area=Terceros");

        //    //        }
        //    //        else
        //    //        {
        //    //            return Redirect("/Clientes/Clientes");
        //    //        }
        //    //    }
        //    //    else
        //    //    {
        //    //        if (await UpdateAsync())
        //    //        {
        //    //            var url = $"/Terceros/TercerosDet?id={_dataClient2.IdTercero}";
        //    //            _dataClient2 = null;
        //    //            _dataClient1 = null;
        //    //            _dataInput = null;
        //    //            return Redirect(url);
        //    //        }
        //    //        else
        //    //        {
        //    //            return Redirect("/Clientes/Clientes?id=1");
        //    //        }
        //    //    }
        //    //}
        //    //else
        //    //{
        //    //    _dataClient1 = JsonConvert.DeserializeObject<InputModel>(dataClient1);
        //    //    return Redirect("/Terceros/TercerosReg?id=1");
        //    //}
        //}




        private async Task<bool> SaveAsync()
        {
            _dataInput = Input;
            bool valor = false;

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {

                    ModelState.Clear();

                    var nuevoTercero = new TTerceros
                    {
                        TipoTerceroId = Input.TipoTerceroId,
                        IdTipTercero = Input.IdTipTercero,
                        Nid = Input.Nid,
                        IdTipDocumento = Input.IdTipDocumento,
                        Name = Input.Name,
                        NombreComercial = Input.NombreComercial,
                        Email = Input.Email,
                        Phone = Input.Phone,
                        Credit = Input.Credit,
                        TercCodContId = Input.TercCodContId,
                        TercModCompras = Input.TercModCompras,
                        TercModHis = Input.TercModHis,
                        TercModNomina = Input.TercModNomina,
                        TercModVenta = Input.TercModVenta,
                        PorDefecto = false,
                        DireccionT = Direccionx,
                        //Comisiones = Comisionesx,
                    };

                    if (!TryValidateModel(nuevoTercero))
                    {
                        valor = false;
                        //return new JsonResult(new { success = false, message = "Error de validaci�n" });
                    }
                    await _context.Set<TTerceros>().AddAsync(nuevoTercero);
                    await _context.SaveChangesAsync();



                    transaction.Commit();


                    valor = true;
                }

            });


            return valor;
        }
    }
}

//private async Task<bool> UpdateAsync()
//{
//        _dataInput = Input;
//        var valor = false;



//        Direccionx.RemoveAll(d => d.IdDirection == 0);
//        //Comisionesx.RemoveAll(d => d.HisComisionId == 0);
//        //Movimientosx.RemoveAll(d => d.TerMovId == 0);




//        var strategy = _context.Database.CreateExecutionStrategy();
//        await strategy.ExecuteAsync(async () =>
//        {
//            using var transaction = await _context.Database.BeginTransactionAsync();
//            try
//            {
//                var existingClient = await _context.TTerceros
//                    .Include(c => c.DireccionT)
//                    //.Include(c => c.Comisiones) // AÑADIR ESTO
//                    //.Include(c => c.Movimientos) // AÑADIR ESTO
//                    //.Include(c => c.DocCompras) // AÑADIR ESTO
//                    .FirstOrDefaultAsync(c => c.Nid == Input.Nid);

//                if (existingClient != null)
//                {
//                    // Eliminar direcciones que ya no están o tienen IdDirection = 0
//                    var idsDireccionActuales = Direccionx.Select(d => d.IdDirection).ToList();
//                    var direccionesParaEliminar = existingClient.DireccionT
//                        .Where(d => !idsDireccionActuales.Contains(d.IdDirection) || d.IdDirection == 0)
//                        .ToList();

//                    foreach (var dir in direccionesParaEliminar)
//                    {
//                        _context.TDireccionT.Remove(dir);
//                    }

//                    // Eliminar direcciones que ya no están o tienen IdDirection = 0
//                    var idsPorcentajesActuales = Comisionesx.Select(d => d.HisComisionId).ToList();
//                    var idsComisionesActuales = Comisionesx.Select(d => d.HisComisionId).ToList();
//                    var comisionesParaEliminar = existingClient.Comisiones
//.Where(d => !idsPorcentajesActuales.Contains(d.HisComisionId) || d.HisComisionId == 0)
//.ToList();



//                    foreach (var dir in comisionesParaEliminar)
//                    {
//                        _context.THisComisiones.Remove(dir);
//                    }


//                    // Actualizar datos del cliente
//                    existingClient.IdTipDocumento = Input.IdTipDocumento;
//                    existingClient.Name = Input.Name;
//                    existingClient.NombreComercial = Input.NombreComercial;
//                    existingClient.Email = Input.Email;
//                    existingClient.Phone = Input.Phone;
//                    existingClient.TercCodContId = Input.TercCodContId;
//                    existingClient.TercModCompras = Input.TercModCompras;
//                    existingClient.TercModHis = Input.TercModHis;
//                    existingClient.TercModNomina = Input.TercModNomina;
//                    existingClient.TercModVenta = Input.TercModVenta;
//                    existingClient.Credit = Input.Credit;

//                    // Forzar que las direcciones con IdDirection >= 1000000 se traten como nuevas
//                    foreach (var direccion in Direccionx.Where(d => d.IdDirection >= 1000000))
//                    {
//                        direccion.IdDirection = 0;
//                    }

//                    // Forzar que las direcciones con IdDirection >= 1000000 se traten como nuevas
//                    foreach (var comision in Comisionesx.Where(d => d.HisComisionId >= 1000000))
//                    {
//                        comision.HisComisionId = 0;
//                    }

//                    // Actualizar o agregar direcciones
//                    foreach (var direccion in Direccionx)
//                    {
//                        var existingDireccion = existingClient.DireccionT
//                            .FirstOrDefault(d => d.IdDirection == direccion.IdDirection);

//                        if (existingDireccion != null)
//                        {
//                            existingDireccion.DirDescrip = direccion.DirDescrip;
//                            existingDireccion.Direccion = direccion.Direccion;
//                            existingDireccion.CodCiudad = direccion.CodCiudad;
//                        }
//                        else
//                        {
//                            existingClient.DireccionT.Add(direccion);
//                        }
//                    }








//                    // Actualizar o agregar Comisiones
//                    foreach (var comision in Comisionesx)
//                    {
//                        var existingComision = existingClient.Comisiones
//                            .FirstOrDefault(d => d.HisComisionId == comision.HisComisionId);


//                        if (existingComision != null)
//                        {
//                            existingComision.HisComisCodigo = comision.HisComisCodigo;
//                            existingComision.HisComisNombre = comision.HisComisNombre;
//                            existingComision.HisComisPorcentaje = comision.HisComisPorcentaje;
//                        }
//                        else
//                        {
//                            existingClient.Comisiones.Add(comision);

//                        }
//                    }

//                    //// Actualizar o agregar Documentos de Compras
//                    //foreach (var compra in Comprasx)
//                    //{
//                    //    var existingComision = existingClient.Comisiones
//                    //        .FirstOrDefault(d => d.HisComisionId == comision.HisComisionId);


//                    //    if (existingComision != null)
//                    //    {
//                    //        existingComision.HisComisCodigo = comision.HisComisCodigo;
//                    //        existingComision.HisComisNombre = comision.HisComisNombre;
//                    //        existingComision.HisComisPorcentaje = comision.HisComisPorcentaje;
//                    //    }
//                    //    else
//                    //    {
//                    //        existingClient.Comisiones.Add(comision);

//                    //    }
//                    //}


//                    //// Primero elimina los documentos anteriores si deseas reemplazarlos
//                    //var docsAntiguos = _context.TComprasDoc
//                    //    .Where(d => d.IdProveedor == existingClient.IdTercero)
//                    //    .ToList();

//                    //_context.TComprasDoc.RemoveRange(docsAntiguos);

//                    //// Agrega los nuevos documentos de compra
//                    //if (Comprasx != null && Comprasx.Any())
//                    //{
//                    //    foreach (var compra in Comprasx)
//                    //    {
//                    //        compra.IdProveedor = existingClient.IdTercero;
//                    //        compra.tmodulo = "TERCEROS";
//                    //        compra.TesForPagId = 1;

//                    //        _context.TComprasDoc.Add(compra);
//                    //    }
//                    //}







//                    // Actualizar o agregar direcciones
//                    foreach (var movimiento in Movimientosx)
//                    {
//                        var existingMovimientos = existingClient.Movimientos
//                            .FirstOrDefault(d => d.TerMovId == movimiento.TerMovId);


//                        if (existingMovimientos != null)
//                        {
//                            existingMovimientos.TerMovMedLee = movimiento.TerMovMedLee;

//                        }
//                        else
//                        {
//                            existingClient.Movimientos.Add(movimiento);

//                        }
//                    }
//                    _context.TTerceros.Update(existingClient);
//                    await _context.SaveChangesAsync();






//                    await transaction.CommitAsync();
//                    valor = true;
//                }
//                else
//                {
//                    _dataInput.ErrorMessage = $"El {Input.Nid} no fue encontrado";
//                    valor = false;
//                }
//            }
//            catch (Exception ex)
//            {
//                _dataInput.ErrorMessage = ex.Message;
//                await transaction.RollbackAsync();
//                valor = false;
//            }
//        });

//        return valor;
//    }