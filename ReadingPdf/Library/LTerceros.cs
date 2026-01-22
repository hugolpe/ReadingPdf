using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ReadingPdf.Areas.Terceros.Models;
using ReadingPdf.Data;
using ReadingPdf.Library;


namespace ReadingPdf.Library
{
    public class LTerceros : ListObject
    {
        public LTerceros(ApplicationDbContext context)
        {
            _context = context;
        }

        
        public List<InputModelTerceros> GetTTerceros(string valor, int id, int id2)
        {
            var query = (
                from t in ((IQueryable<TTerceros>)_context.TTerceros)
                //join tipoTercero in _context.TTiposGlobal on t.TipoTerceroId equals tipoTercero.TTipoGId into tipoTerceroJoin
                //from tipoTercero in tipoTerceroJoin.DefaultIfEmpty()

                //join tipDocumento in _context.TTiposGlobal on t.IdTipDocumento equals tipDocumento.TTipoGId into tipDocumentoJoin
                //from tipDocumento in tipDocumentoJoin.DefaultIfEmpty()

                //join tipTercero in _context.TTiposGlobal on t.IdTipTercero equals tipTercero.TTipoGId into tipTerceroJoin
                //from tipTercero in tipTerceroJoin.DefaultIfEmpty()

                select new
                {
                    Tercero = t,
                //    NombreTipoTercero = tipoTercero != null ? tipoTercero.TTipoGNombre : "",
                //    NombreTipoDocumento = tipDocumento != null ? tipDocumento.TTipoGNombre : "",
                //    NombreTipTercero = tipTercero != null ? tipTercero.TTipoGNombre : ""
                });


            // Aplicar filtros solo si vienen parámetros
            if (!string.IsNullOrEmpty(valor))
            {
                query = query.Where(q =>
                    q.Tercero.Nid.StartsWith(valor) ||
                    q.Tercero.Name.Contains(valor) ||
                    q.Tercero.NombreComercial.Contains(valor) ||
                    q.Tercero.Email.Contains(valor));
            }

            if (id != 0)
            {
                query = query.Where(q => q.Tercero.IdTercero == id);
            }
            else
            {
                query = query.Where(q => q.Tercero.IdTipTercero == id2);
            }

            // Proyección final
            var tercerosList = query
                .OrderBy(q => q.Tercero.Name)
                .Select(q => new InputModelTerceros
                {
                    IdTercero = q.Tercero.IdTercero,
                    TipoTerceroId = q.Tercero.TipoTerceroId,
                    IdTipTercero = q.Tercero.IdTipTercero,
                    IdTipDocumento = q.Tercero.IdTipDocumento,
                    Nid = q.Tercero.Nid,
                    Name = q.Tercero.Name,
                    NombreComercial = q.Tercero.NombreComercial,
                    Email = q.Tercero.Email,
                    IdDirection = q.Tercero.IdDirection,
                    Phone = q.Tercero.Phone,
                    Credit = q.Tercero.Credit,
                    Image = q.Tercero.Image,
                    TercCodContId = q.Tercero.TercCodContId,
                    PorDefecto = q.Tercero.PorDefecto,

                    //NombreTipoTercero = q.NombreTipoTercero,
                    //NombreTipoDocumento = q.NombreTipoDocumento,
                    //NombreTipTercero = q.NombreTipTercero // opcional
                })
                .ToList();

            return tercerosList;
        }


        //public List<TTerceros> GetTClient(String Nid)
        //{
        //    var listTTerceros = new List<TTerceros>();
        //    using (var dbContext = new ApplicationDbContext())
        //    {
        //        listTTerceros = dbContext.TTerceros.Where(u => u.Nid.Equals(Nid)).ToList();
        //    }

        //    return listTTerceros;
        //}


        //public List<InputModelTerceros> GetTClientesx(String valor, int id)
        //{
        //    List<TTerceros> listTTerceros;
        //    var tercerosList = new List<InputModelTerceros>();
        //    if (valor == null && id.Equals(0))
        //    {
        //        listTTerceros = _context.TTerceros.ToList();
        //    }
        //    else
        //    {
        //        if (id.Equals(0))
        //        {
        //            listTTerceros = _context.TTerceros.Where(u => u.Nid.StartsWith(valor) ||
        //                                                        u.Name.StartsWith(valor) ||
        //                                                        u.NombreComercial.StartsWith(valor) ||
        //                                                        u.Email.StartsWith(valor)).ToList();
        //        }
        //        else
        //        {
        //            listTTerceros = _context.TTerceros.Where(u => u.IdTercero.Equals(id)).ToList();
        //        }
        //    }
        //    if (!listTTerceros.Count.Equals(0))
        //    {
        //        foreach (var item in listTTerceros)
        //        {
        //            tercerosList.Add(new InputModelTerceros
        //            {
        //                IdTercero = item.IdTercero,
        //                Nid = item.Nid,
        //                Name = item.Name,
        //                NombreComercial = item.NombreComercial,
        //            });
        //        }
        //    }
        //    return tercerosList;
        //}
        //public List<InputModelTerceros> GetTClientesy(String valor, int id)
        //{
        //    List<TTerceros> listTTerceros;
        //    var tercerosList = new List<InputModelTerceros>();
        //    if (valor == null && id.Equals(0))
        //    {
        //        listTTerceros = _context.TTerceros.ToList();
        //    }
        //    else
        //    {
        //        if (id.Equals(0))
        //        {
        //            listTTerceros = _context.TTerceros.Where(u => u.Nid.StartsWith(valor) ||
        //                                                        u.Name.StartsWith(valor) ||
        //                                                        u.NombreComercial.StartsWith(valor) ||
        //                                                        u.Email.StartsWith(valor)).ToList();
        //        }
        //        else
        //        {
        //            listTTerceros = _context.TTerceros.Where(u => u.IdTercero.Equals(id)).ToList();
        //        }
        //    }
        //    if (!listTTerceros.Count.Equals(0))
        //    {
        //        foreach (var item in listTTerceros)
        //        {
        //            tercerosList.Add(new InputModelTerceros
        //            {
        //                IdTercero = item.IdTercero,
        //                IdTipDocumento = item.IdTipDocumento,
        //                Nid = item.Nid,
        //                Name = item.Name,
        //                NombreComercial = item.NombreComercial,
        //                Email = item.Email,
        //                Phone = item.Phone,
        //                Credit = item.Credit,
        //                //Direccion = _context.TDireccion.Where(u => u.TClienteIdCliente.Equals(item.IdCliente)).ToList(),
        //                //Direccion = item.Direccion,
        //                //Image = item.Image,
        //            });
        //        }
        //    }
        //    return tercerosList;
        //}


        //public InputModelTerceros GetTClientesyy(int id)
        //{
        //    var data1 = (from a in _context.TTerceros
        //                 join b in _context.TTipDoc on a.IdTipDocumento equals b.IdTipDoc
        //                 join c in _context.TTiposGlobal on a.TipoTerceroId equals c.TTipoGId
        //                 where a.IdTercero == id
        //                 select new InputModelTerceros
        //                 {
        //                     IdTercero = a.IdTercero,
        //                     TipoTerceroId = a.TipoTerceroId,
        //                     IdTipTercero = a.IdTipTercero,
        //                     IdTipDocumento = a.IdTipDocumento,
        //                     NombreDoc = b.TipDocDescrip,
        //                     Nid = a.Nid,
        //                     Name = a.Name,
        //                     NombreComercial = a.NombreComercial,
        //                     Email = a.Email,
        //                     TercCodContId = a.TercCodContId,
        //                     Phone = a.Phone,
        //                     TercModHis = a.TercModHis,
        //                     TercModCompras = a.TercModCompras,
        //                     TercModNomina = a.TercModNomina,
        //                     TercModVenta = a.TercModVenta,
        //                     NombreTipoTercero = c.TTipoGNombre,
        //                     DireccionT = a.DireccionT,
        //                     Comisiones = a.Comisiones,
        //                     Prestaciones = a.Prestaciones,
        //                     TVehiculosT = a.TVehiculosT,
        //                     Movimientos = _context.TTercerosHisMov
        //                        .Where(m => m.TerMovCedula == a.Nid)
        //                        .Select(m => new TTercerosHisMov
        //                        {
        //                            TerMovId = m.TerMovId,
        //                            TerMovCodigo = m.TerMovCodigo,
        //                            TerMovFecEstudio = m.TerMovFecEstudio,
        //                            TerMovFecLectura = m.TerMovFecLectura,
        //                            TerMovFecPago = m.TerMovFecPago,
        //                            TerMovMedLee = m.TerMovMedLee,
        //                            TerMovVrEstudio = m.TerMovVrEstudio,
        //                            TerMovCedula = m.TerMovCedula,
        //                            TerMovInvoice = m.TerMovInvoice,
        //                            TerMoTErMovCodOrd = m.TerMoTErMovCodOrd,
        //                            TerMovPago = m.TerMovPago,
        //                            // Nuevo campo: nombre del estudio
        //                            NombreEstudio = _context.THisEstudios
        //                                .Where(e => e.HisEstCodigo == m.TerMovCodigo)
        //                                .Select(e => e.HisEstNombre)
        //                                .FirstOrDefault(),
        //                            NombreMedico = _context.TTerceros
        //                                .Where(f => f.Nid == m.TerMovMedLee)
        //                                .Select(f => f.Name)
        //                                .FirstOrDefault()
        //                        })
        //                        .ToList(),
        //                     Credit = a.Credit,
        //                 }).FirstOrDefault();

        //    return data1;
        //}

        //public InputModelTerceros GetTClientesyx(string id)
        //{
        //    var data1 = (from a in _context.TTerceros
        //                 join b in _context.TTipDoc on a.IdTipDocumento equals b.IdTipDoc
        //                 join c in _context.TTiposGlobal on a.TipoTerceroId equals c.TTipoGId
        //                 where a.Nid == id
        //                 select new InputModelTerceros
        //                 {
        //                     IdTercero = a.IdTercero,
        //                     TipoTerceroId = a.TipoTerceroId,
        //                     IdTipTercero = a.IdTipTercero,
        //                     IdTipDocumento = a.IdTipDocumento,
        //                     NombreDoc = b.TipDocDescrip,
        //                     Nid = a.Nid,
        //                     Name = a.Name,
        //                     NombreComercial = a.NombreComercial,
        //                     Email = a.Email,
        //                     TercCodContId = a.TercCodContId,
        //                     Phone = a.Phone,
        //                     TercModHis = a.TercModHis,
        //                     TercModCompras = a.TercModCompras,
        //                     TercModNomina = a.TercModNomina,
        //                     TercModVenta = a.TercModVenta,
        //                     NombreTipoTercero = c.TTipoGNombre,
        //                     DireccionT = a.DireccionT,
        //                     TerPolComId = a.TerPolComId,
        //                     TerPoliza = a.TerPoliza,
        //                     TerPolFecVen = a.TerPolFecVen,
        //                     TerConPoliza = a.TerConPoliza,
        //                     Comisiones = a.Comisiones,
        //                     Movimientos = _context.TTercerosHisMov
        //                        .Where(m => m.TerMovCedula == a.Nid)
        //                        .Select(m => new TTercerosHisMov
        //                        {
        //                            TerMovId = m.TerMovId,
        //                            TerMovCodigo = m.TerMovCodigo,
        //                            TerMovFecEstudio = m.TerMovFecEstudio,
        //                            TerMovFecLectura = m.TerMovFecLectura,
        //                            TerMovFecPago = m.TerMovFecPago,
        //                            TerMovMedLee = m.TerMovMedLee,
        //                            TerMovVrEstudio = m.TerMovVrEstudio,
        //                            TerMovCedula = m.TerMovCedula,
        //                            TerMovInvoice = m.TerMovInvoice,
        //                            TerMoTErMovCodOrd = m.TerMoTErMovCodOrd,
        //                            TerMovPago = m.TerMovPago,
        //                            // Nuevo campo: nombre del estudio
        //                            NombreEstudio = _context.THisEstudios
        //                                .Where(e => e.HisEstCodigo == m.TerMovCodigo)
        //                                .Select(e => e.HisEstNombre)
        //                                .FirstOrDefault(),
        //                            NombreMedico = _context.TTerceros
        //                                .Where(f => f.Nid == m.TerMovMedLee)
        //                                .Select(f => f.Name)
        //                                .FirstOrDefault()
        //                        })
        //                        .ToList(),
        //                     Credit = a.Credit,
        //                 }).FirstOrDefault();

        //    return data1;
        //}

        //public InputModelTerceros GetTClienteSelecto(bool selecto)
        //{
        //    var data1 = (from a in _context.TTerceros
        //                 join b in _context.TTipDoc on a.IdTipDocumento equals b.IdTipDoc
        //                 where a.PorDefecto == selecto
        //                 select new InputModelTerceros
        //                 {
        //                     IdTercero = a.IdTercero,
        //                     IdTipDocumento = a.IdTipDocumento,
        //                     NombreDoc = b.TipDocDescrip,
        //                     Nid = a.Nid,
        //                     Name = a.Name,
        //                     NombreComercial = a.NombreComercial,
        //                     TercCodContId = a.TercCodContId,
        //                     Email = a.Email,
        //                     Phone = a.Phone,
        //                     Credit = a.Credit,
        //                     PorDefecto = a.PorDefecto,

        //                 }).FirstOrDefault();

        //    return data1;
        //}


        //public List<SelectListItem> getListaDoctors(int tipoTerceroId)
        //{
        //    return _context.TTerceros
        //        .Where(t => t.IdTipTercero == tipoTerceroId)
        //        .Select(t => new SelectListItem
        //        {
        //            Value = t.Nid,
        //            Text = t.Name
        //        })
        //        .ToList();
        //}

        //public List<SelectListItem> getListaSeguros(int tipoTerceroId)
        //{
        //    return _context.TTerceros
        //        .Where(t => t.IdTipTercero == tipoTerceroId)
        //        .Select(t => new SelectListItem
        //        {
        //            Value = t.IdTercero.ToString(),
        //            Text = t.Name
        //        })
        //        .ToList();
        //}
    }
}
