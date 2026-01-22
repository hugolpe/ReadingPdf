using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using ReadingPdf.Areas.Terceros.Models;
using ReadingPdf.Data;
using ReadingPdf.Library;

namespace ReadingPdf.Areas.Terceros.Pages.Account
{
    public class TercerosDetModel : PageModel
    {
        private readonly LTerceros _terceros;
        //private readonly LDireccionesDrop _direccion;
        private readonly ApplicationDbContext _context;
        //private static InputModelServicios _dataServicios2;
        //private static InputModel _dataInput;
        //public static InputModelServicios _dataServicios;
        //public static InputModelContratosAsi _dataContratos;
        public static InputModelTerceros _dataTerceros;


        public TercerosDetModel(ApplicationDbContext context)
        {
            _terceros = new LTerceros(context);
            //_direccion = new LDireccionesDrop(context);
            _context = context;
        }
        public void OnGet(int id)
        {
            //var data = _terceros.GetTClientesyy(id);
            //var cont = _contratos.getTContratos(null, id);
            //var serv = _servicios.getTClientServicios(null, id);
            //var preci = _precios.getTPrecios("SERVICIOS", 0, 0);
            //var cuent = _cuentas.getTConPlanCuentas(0);
            //var listaTipSer = _servicios1.getTipoServicio(0);
            //var listaEstados = _estado.getEstados("SERVICIOS", 0);
            //var listaDireccion = _direccion.getDirecciones(id);
            //var listaTipoPago = _tipagos.GetTTipoPagos(0);


            Input = new InputModel
            {
                //DataTerceros = data,

                //TipoTerLista = _personaTipo.getTTiposGlobal("TERCEROS-TIP", 0),
                //TipoPerLista = _personaTipo.getTTiposGlobal("TERCEROS-TP",0),
                //TipodocLista = _tipodoc.getTipDoc(0),
                //Documentos = _proveedores.getTComprasDoc(null, id),
                //ComprasDoc = _terceros.GetTClientesyy(id),  

                //Contratos = cont,
                ////DatosServicios = _dataServicios,
                ////DataContratos = _dataContratos,
                //Servicios = serv,
                //Precios = preci,
                //tiposerLista = listaTipSer,
                //ProfesionalLista = _profesional.getProfesional(0),
                //estadosLista = listaEstados,
                //direccionLista = listaDireccion,
                ////PreciosLista = _precios1.getPrecios("SERVICIOS", 0),
                ////tipopagoLista = listaTipoPago,
                //cuentasLista = _cuentas.getTConPlanCuentas(0),
                //CiudadLista = _ciudades.getCiudad(0),
                //listaMedicos = _terceros.getListaDoctors(41),
                //listaEstudios = _estudios.getTEstudios(0),
                //tipodocCompLista = _tipodoccom.getTipDocCom(0),
                //articulosLista = _articulos.getLArticulos(0),
                // tipopagoLista = _tipagos.GetTTipoPagos(0),
                //formapagoLista = _formapago.getFormaPago(0),

                //tipoContratoLista = _tipoContrato.getTipoContrato(0),

            };
        }

        //private int idClient;

        //[BindProperty]
        public InputModel Input { get; set; }






        //public bool TipoContratox { get; set; } = false;


        //public int IdIdentificax { get; set; }
        //public int ClientexId { get; set; }

        //public int pagoBanEfeId { get; set; }


        public class InputModel : InputModelTerceros
        {
                public InputModelTerceros DataTerceros { get; set; }


            
            public List<SelectListItem> tipopagoLista { get; set; }

            public List<SelectListItem> TipodocLista { get; set; }

            public List<SelectListItem> TipoPerLista { get; set; }

            public List<SelectListItem> TipoTerLista { get; set; }
        
        public List<SelectListItem> cuentasLista { get; set; }

            public List<SelectListItem> CiudadLista { get; set; }

            public List<SelectListItem> direccionLista { get; set; }

            public List<SelectListItem> tipodocCompLista { get; set; }
            public List<SelectListItem> formapagoLista { get; set; }

            public List<SelectListItem> articulosLista { get; set; }
            public List<SelectListItem> comisionesLista { get; set; }
            public List<SelectListItem> listaMedicos { get; set; }
            public List<SelectListItem> listaEstudios { get; set; }

        }
        //{
        //    public DateTime dFechaServicio { get; set; }

        //    public DateTime fechaPago { get; set; }

        //    //public TVentasPagDet PagosDetalle { get; set; } = new TVentasPagDet();
        //    //public TVentasPagDet PagosDetalle { get; set; }

        //    //internal object activityDates;


        //    //public InputModelServicios DatosServicios { get; set; }

        //    //public InputModelDocumentoVenta inputModelDocumentoVenta { get; set; }

        //    //public InputModelContratosAsi DataContratos { get; set; }

        //    //public List<InputModelDireccion> Direccion1 { get; set; }
        //    //public List<InputModelContratosAsi> Contratos { get; set; }
        //    //public List<InputModelServicios> Servicios { get; set; }

        //    //public List<InputModelPrecios> Precios { get; set; }
        //    public List<SelectListItem> tiposerLista { get; set; }

        //    public List<SelectListItem> tipopagoLista { get; set; }

        //    public List<SelectListItem> tipoContratoLista { get; set; }

        //    public List<SelectListItem> ProfesionalLista { get; set; }
        //    public List<SelectListItem> estadosLista { get; set; }
        //    public List<SelectListItem> direccionLista { get; set; }

        //    public List<SelectListItem> PreciosLista { get; set; }
        
        //}



        //public async Task<IActionResult> OnPost(
        //    String dataTerceros,
        //    int idClientex,
        //    int Identificacion,
        //    bool TipoContrato
        //    )

        //{
        //    //detallex = MesaDetalles;
        //    //detalleP = PagosDetalle;
        //    IdIdentificax = Identificacion;
        //    ClientexId = idClientex;
        //    TipoContratox = TipoContrato;

        //    if (_dataTerceros == null)
        //    {
        //        if (_terceros == null )
        //        {
        //            if (await SaveAsync())
        //            {
        //                _dataTerceros = null;

        //                //return Redirect("/Servicios/Servicios?area=Customers");
        //                return Redirect("/Terceros/TercerosDet?id=" + Convert.ToString(IdIdentificax));

        //            }
        //            else
        //            {
        //                return Redirect("Clientes/Servicios");

        //            }
        //        }
        //        else
        //        {
        //            if (await UpdateAsync())
        //            {
        //                string url = $"/Customers/Account/Details?id={_dataTerceros.IdTercero}";
        //                //        //_dataClient2 = null;
        //                //        //_dataClient1 = null;
        //                //        //_dataInput = null;
        //                return Redirect(url);
        //            }
        //            else
        //            {
        //                return Redirect("/Customers/Register?id=1");
        //            }

        //        }
        //    }
        //    else
        //    {
        //        _dataTerceros = JsonConvert.DeserializeObject<InputModelTerceros>(dataTerceros);
        //        return Redirect("/Terceros/TercerosReg?id=1");
        //    }
        //}

        //private async Task<bool> SaveAsync()
        //{
        //    _dataInput = Input;
        //    bool valor = false;

        //    var strategy = _context.Database.CreateExecutionStrategy();
        //    await strategy.ExecuteAsync(async () =>
        //    {
        //        using (var transaction = await _context.Database.BeginTransactionAsync())
        //        {
        //            if (!TipoContratox)
        //            {
        //                ModelState.Clear();

        //                var nuevoServicio = new TServicios
        //                {
        //                    IdTipServicios = Input.IdTipServicios,
        //                    FechaServicio = Input.FechaServicio,
        //                    HoraInicio = Input.HoraInicio,
        //                    HoraFinal = Input.HoraFinal,
        //                    HoraEntrada = Input.HoraEntrada,
        //                    HoraSalida = Input.HoraSalida,
        //                    TProfesionalsIdProfesional = Input.TProfesionalsIdProfesional,
        //                    IdEstadoServicio = Input.IdEstadoServicio,
        //                    KitLimpieza = Input.KitLimpieza,
        //                    IdDirection = Input.IdDirection,
        //                    PrecioServicio = Input.PrecioServicio,
        //                    IdPrecio = Input.PrecioId,
        //                    TClienteIdCliente = IdIdentificax // Relacionar con el cliente
        //                };

        //                if (!TryValidateModel(nuevoServicio))
        //                {
        //                    valor = false;
        //                    //return new JsonResult(new { success = false, message = "Error de validaci n" });
        //                }

        //                await _context.TServicios.AddAsync(nuevoServicio);
        //                await _context.SaveChangesAsync();


        //            }
        //            else
        //            {
        //                ModelState.Clear();
        //                TContratos contrato = new TContratos
        //                {
        //                    ConNombre = Input.DataContratos.ConNombre,
        //                    ConDescrip = Input.DataContratos.ConDescrip,
        //                    IdTipoContrato = Input.DataContratos.IdTipoContrato,
        //                    Lun = Input.DataContratos.Lun,
        //                    Mar = Input.DataContratos.Mar,
        //                    Mie = Input.DataContratos.Mie,
        //                    Jue = Input.DataContratos.Jue,
        //                    Vie = Input.DataContratos.Vie,
        //                    Sab = Input.DataContratos.Sab,
        //                    Dom = Input.DataContratos.Dom,
        //                    FijoProf = Input.DataContratos.FijoProf,
        //                    TProfesionalsIdProfesional = Input.DataContratos.TProfesionalsIdProfesional,
        //                    IdServicio = Input.DataContratos.IdServicio,
        //                    TClienteIdCliente = IdIdentificax,
        //                    IdDireccion = Input.DataContratos.IdDireccion,
        //                    PrecioServicio = Input.DataContratos.PrecioServicio,
        //                    PrecioId = Input.DataContratos.PrecioId,
        //                    PrecioContrato = Input.DataContratos.PrecioContrato,
        //                    PrecioAiu = Input.DataContratos.PrecioAiu
        //                };

        //                if (!TryValidateModel(contrato))
        //                {
        //                    valor = false;
        //                    //return new JsonResult(new { success = false, message = "Error de validaci n" });
        //                }
        //                await _context.TContratos.AddAsync(contrato);
        //                await _context.SaveChangesAsync();
        //            }

        //            transaction.Commit();
        //            valor = true;
        //        }
        //    });
        //    return valor;
        //}

        //private async Task<bool> UpdateAsync()
        //{
        //    _dataInput = Input;
        //    var valor = false;
        //    //var mesaId = detallex.LastOrDefault()?.MesDetMesaId;
        //    var numFactura = _numerofact.getUltimo("SPF");
        //    var vrDocumentoId = 0;
        //    var fechaId = new DateTime();
        //    var descripcionId = "";
        //    //var pagoBanEfeId = 0;
        //    var cuentaPagoCodConId = 0;

        //    if (!detalleP.Count().Equals(0))
        //    {
        //        vrDocumentoId = (int)detalleP.LastOrDefault().DocVentaPagDetVrPago;
        //        fechaId = (DateTime)detalleP.LastOrDefault().VentaDocPagDetFecha;
        //        descripcionId = detalleP.LastOrDefault().DocVentaDetNombre;
        //        pagoBanEfeId = 23;// _formapago.getFormPag(Input.IdFormaPago).TesForPagPag;
        //        cuentaPagoCodConId = detalleP.LastOrDefault().VentaDocPagDetCtaId;
        //    }

        //    //var tipComprobante = "11";
        //    //var currentDate = DateTime.Now;
        //    //var year = currentDate.Year;
        //    //var month = currentDate.Month;
        //    //var numComprobante = _correlativo.getUltimo(tipComprobante, year, month);
        //    //var cuentaProv = _context.TCliente.Where(u => u.IdCliente.Equals(Input.IdCliente)).ToList();
        //    //var cuentaBanco = _context.TTesBancosCta.Where(u => u.BancCuentaId.Equals(cuentaPagoCodConId)).ToList();
        //    //var tipoTransac = _context.TTesForPag.Where(u => u.TesForPagId.Equals(cuentaPagoCodConId)).ToList();

        //    List<TVentasDocDet> DetalleVentax = new List<TVentasDocDet>();

        //    for (int i = 0; i < 2; i++)
        //    {
        //        var datosArticulos = _articulos.getTArticulos(null, i + 2);

        //        // Verificar que la lista no esté vacía
        //        var ultimoArticulo = datosArticulos.LastOrDefault();

        //        if (ultimoArticulo != null) // Evita errores de referencia nula
        //        {
        //            TVentasDocDet datoDetalle = new TVentasDocDet
        //            {
        //                DocVentaDocCodArticuloId = ultimoArticulo.ArticuloId,
        //                DocVentaDetNombre = "DETALLE ESCRIBIR " + (i + 1),
        //                DocVentaDetCant = 1,
        //                DocVentaDetPrecio = ultimoArticulo.ArtPrecio, // Verificado
        //                DocVentaDetSubT = ultimoArticulo.ArtPrecio * 1,
        //                tipotransd = "DECREMENTAR",
        //                tmodulod = "CLIENTES",
        //                DocVentaDetCodCon = ultimoArticulo.ArtCodContInv // Verificado
        //            };

        //            DetalleVentax.Add(datoDetalle);
        //        }
        //        else
        //        {
        //            Console.WriteLine($"No se encontró información para el artículo con ID {i + 1}");
        //        }
        //    }



        //    List<TVentasPagDet> DetallePagox = new List<TVentasPagDet>();

        //    foreach (var detalle in detalleP)
        //    {
        //        TVentasPagDet pagoDetalle = new TVentasPagDet
        //        {
        //            VentaDocPagDetCtaId = detalle.VentaDocPagDetCtaId,
        //            //VentaDocPagDetFecha = fechaId,
        //            DocVentaDetNombre = "DETALLE ESCRIBIR",
        //            DocVentaPagDetVrPago = detalle.DocVentaPagDetVrPago,
        //            DocVentaPagDetAumento = true,
        //        };

        //        // Add to the list
        //        DetallePagox.Add(pagoDetalle);
        //    }

        //    //Aqui agrego registro de cuentas contables detalle asiento. Para guardar dato de cuenta contable si es credito o efectivo
        //    //var codigoconpag = 0;
        //    //if (pagoBanEfeId)
        //    //{
        //    //    codigoconpag = cuentaBanco.LastOrDefault().BancCodContable;
        //    //}
        //    //else
        //    //{
        //    //    codigoconpag = cuentaProv.LastOrDefault().ClieCodContId;
        //    //}

        //    var strategy = _context.Database.CreateExecutionStrategy();
        //    await strategy.ExecuteAsync(async () =>
        //    {
        //        using var transaction = _context.Database.BeginTransaction();
        //        try
        //        {
        //            if (!detalleP.Count().Equals(0))
        //            {
        //                var ventasDoc = new TVentasDoc
        //                {
        //                    IdCliente = ClientexId,
        //                    VentaNroDoc = numFactura, // 
        //                    IdTipDocVenta = 1,
        //                    TesForPagId = 1,
        //                    VentaFecha = fechaId,
        //                    VrDocumento = vrDocumentoId,
        //                    //VentaComprobante = numComprobante,
        //                    tmodulo = "CLIENTES",
        //                    VentaDescripcion = descripcionId,
        //                    VentasDetalle = DetalleVentax,
        //                    PagosDetalle = DetallePagox
        //                };
        //                await _context.AddAsync(ventasDoc);
        //                await _context.SaveChangesAsync();

        //                if (pagoBanEfeId == 23)
        //                {

        //                    var numTransac = _nroTransac.getUltimo(fechaId);

        //                    //var (numTransac, saldoAnterior) = getUltimo(miFecha);
        //                    //Console.WriteLine($"N�mero de Transacci�n: {numTransac}");
        //                    //Console.WriteLine($"Saldo Anterior: {saldoAnterior}");


        //                    var libroBanco = new TTesLibroBanco
        //                    {
        //                        //BancoLibCtaId = detalleP.LastOrDefault().VentaDocPagDetCtaId,
        //                        BancoLibCtaId = cuentaPagoCodConId,
        //                        BancoLibNroTra = numTransac.numTransac,
        //                        BancoLibFecha = fechaId,
        //                        BancoLibDescr = "PAGO DOC: " + numFactura,
        //                        BancoLibTipTra = 1,
        //                        BancoLibVrDebe = vrDocumentoId,
        //                        BancoLibVrHaber = 0,
        //                        BancoLibConCop = 1,
        //                    };
        //                    await _context.AddAsync(libroBanco);
        //                    await _context.SaveChangesAsync();
        //                }

        //                //// Obt�n el cliente existente
        //                //var existingMesa1 = await _context.TMesaDetalles
        //                //    .Where(m => m.MesDetMesaId == 1)
        //                //    .ToListAsync(); // Obt�n todos los detalles asociados a la mesa
        //                //if (existingMesa1.Any())
        //                //{
        //                //    // Elimina los detalles existentes
        //                //    _context.TMesaDetalles.RemoveRange(existingMesa1);
        //                //}
        //                //await _context.SaveChangesAsync();
        //                //}
        //                transaction.Commit();
        //                valor = true;
        //            }
        //            else
        //            {
        //                _dataInput.ErrorMessage = $"La mesa con ID {1} no fue encontrada.";
        //                valor = false;
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            _dataInput.ErrorMessage = $"Error: {ex.Message}\nStackTrace: {ex.StackTrace}";
        //            transaction.Rollback();
        //            valor = false;
        //        }
        //    });
        //    return valor;
        //}
    }
}
