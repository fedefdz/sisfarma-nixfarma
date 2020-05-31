﻿using Sisfarma.Sincronizador.Core.Config;
using Sisfarma.Sincronizador.Domain.Core.Repositories.Farmacia;
using Sisfarma.Sincronizador.Domain.Entities.Farmacia;
using Sisfarma.Sincronizador.Nixfarma.Infrastructure.Data;
using System;
using System.Collections.Generic;

namespace Sisfarma.Sincronizador.Nixfarma.Infrastructure.Repositories.Farmacia
{
    public class PedidosRepository : FarmaciaRepository, IPedidosRepository
    {
        private readonly IProveedorRepository _proveedorRepository;
        private readonly IFarmacoRepository _farmacoRepository;
        private readonly ICategoriaRepository _categoriaRepository;
        private readonly IFamiliaRepository _familiaRepository;
        private readonly ILaboratorioRepository _laboratorioRepository;

        public PedidosRepository(LocalConfig config) : base(config)
        { }

        public PedidosRepository(
            IProveedorRepository proveedorRepository,
            IFarmacoRepository farmacoRepository,
            ICategoriaRepository categoriaRepository,
            IFamiliaRepository familiaRepository,
            ILaboratorioRepository laboratorioRepository)
        {
            _proveedorRepository = proveedorRepository ?? throw new ArgumentNullException(nameof(proveedorRepository));
            _farmacoRepository = farmacoRepository ?? throw new ArgumentNullException(nameof(farmacoRepository));
            _categoriaRepository = categoriaRepository ?? throw new ArgumentNullException(nameof(categoriaRepository));
            _familiaRepository = familiaRepository ?? throw new ArgumentNullException(nameof(familiaRepository));
            _laboratorioRepository = laboratorioRepository ?? throw new ArgumentNullException(nameof(laboratorioRepository));
        }

        public IEnumerable<Pedido> GetAllByFechaGreaterOrEqual(DateTime fecha)
        {
            var pedidos = new List<Pedido>();
            var conn = FarmaciaContext.GetConnection();
            try
            {
                var sqlExtra = string.Empty;
                var sql = $@"SELECT * FROM (
                    SELECT * From appul.ad_pedidos WHERE to_char(fecha_pedido, 'YYYYMMDD') >= '{fecha.ToString("yyyyMMdd")}' Order by pedido ASC)
                    WHERE rownum <= 999";

                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var rFechaPedido = Convert.ToDateTime(reader["FECHA_PEDIDO"]);
                    var rPedido = Convert.ToInt64(reader["PEDIDO"]);
                    var rEmpCodigo = Convert.ToString(reader["EMP_CODIGO"]);

                    var pedido = new Pedido
                    {
                        Fecha = rFechaPedido,
                        Numero = rPedido,
                        Empresa = rEmpCodigo
                    };

                    pedidos.Add(pedido);
                }

                reader.Close();
                reader.Dispose();

                return pedidos;
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                conn.Close();
                conn.Dispose();
            }
        }

        internal class PedidoCompositeKey
        {
            internal short Id { get; set; }

            internal int Proveedor { get; set; }
        }

        public IEnumerable<Pedido> GetAllByIdGreaterOrEqual(long numeroPedido)
        {
            var pedidos = new List<Pedido>();
            var conn = FarmaciaContext.GetConnection();
            try
            {
                var sqlExtra = string.Empty;
                var sql = $@"SELECT * FROM (
                    SELECT * From appul.ad_pedidos
                    WHERE pedido >= {numeroPedido})
                    Order by pedido ASC) WHERE rownum <= 999";

                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var rFechaPedido = Convert.ToDateTime(reader["FECHA_PEDIDO"]);
                    var rPedido = Convert.ToInt64(reader["PEDIDO"]);
                    var rEmpCodigo = Convert.ToString(reader["EMP_CODIGO"]);

                    var pedido = new Pedido
                    {
                        Fecha = rFechaPedido,
                        Numero = rPedido,
                        Empresa = rEmpCodigo
                    };

                    pedidos.Add(pedido);
                }

                reader.Close();
                reader.Dispose();

                return pedidos;
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                conn.Close();
                conn.Dispose();
            }
        }

        public IEnumerable<PedidoDetalle> GetAllDetalleByPedido(long numero, string empresa, int anio)
        {
            var detalle = new List<PedidoDetalle>();
            var conn = FarmaciaContext.GetConnection();
            try
            {
                var sqlExtra = string.Empty;
                var sql = $@"
                    select * from appul.ad_linped where pedido='{numero}' AND emp_codigo ='{empresa}' and ejercicio = {anio}";

                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var rArtCodigo = Convert.ToString(reader["ART_CODIGO"]);
                    var rLinea = Convert.ToInt32(reader["LINEA"]);
                    var rCantPedida = !Convert.IsDBNull(reader["CANT_PEDIDA"]) ? Convert.ToInt64(reader["CANT_PEDIDA"]) : 0L;
                    var rPedido = Convert.ToInt64(reader["PEDIDO"]);
                    var rEmpCodigo = Convert.ToString(reader["EMP_CODIGO"]);

                    var rPvpIvaEuros = !Convert.IsDBNull(reader["PVP_IVA_EUROS"]) ? (decimal?)Convert.ToDecimal(reader["PVP_IVA_EUROS"]) : null;
                    var rPcIvaEuros = !Convert.IsDBNull(reader["PC_IVA_EUROS"]) ? (decimal?)Convert.ToDecimal(reader["PC_IVA_EUROS"]) : null;

                    var farmaco = _farmacoRepository.GetOneOrDefaultById(rArtCodigo);
                    Farmaco farmacoPedido = null;
                    if (farmaco != null)
                    {
                        sql = $@"select max(actuales) as stock from appul.ac_existencias where art_codigo = '{rArtCodigo}' group by art_codigo";
                        cmd.CommandText = sql;
                        var readerStock = cmd.ExecuteReader();
                        var stock = readerStock.Read() ? Convert.ToInt64(readerStock["stock"]) : 0L;
                        farmaco.Stock = stock;

                        var proveedor = _proveedorRepository.GetOneOrDefaultByCodigoNacional(rArtCodigo);
                        var categoria = _categoriaRepository.GetOneOrDefaultById(rArtCodigo);

                        Familia familia = null;
                        Familia superFamilia = null;
                        if (string.IsNullOrWhiteSpace(farmaco.SubFamilia))
                        {
                            familia = new Familia { Nombre = string.Empty };
                            superFamilia = _familiaRepository.GetOneOrDefaultById(farmaco.Familia)
                                ?? new Familia { Nombre = string.Empty };
                        }
                        else
                        {
                            familia = _familiaRepository.GetSubFamiliaOneOrDefault(farmaco.Familia, farmaco.SubFamilia)
                                ?? new Familia { Nombre = string.Empty };
                            superFamilia = _familiaRepository.GetOneOrDefaultById(farmaco.Familia)
                                ?? new Familia { Nombre = string.Empty };
                        }

                        var laboratorio = !farmaco.Laboratorio.HasValue ? new Laboratorio { Codigo = string.Empty, Nombre = "<Sin Laboratorio>" }
                            : _laboratorioRepository.GetOneOrDefaultByCodigo(farmaco.Laboratorio.Value, farmaco.Clase, farmaco.ClaseBot)
                                ?? new Laboratorio { Codigo = string.Empty, Nombre = "<Sin Laboratorio>" };

                        farmacoPedido = new Farmaco
                        {
                            Id = farmaco.Id,
                            Codigo = farmaco.Codigo,
                            PrecioCoste = farmaco.PUC,
                            Proveedor = proveedor,
                            Categoria = categoria,
                            Familia = familia,
                            Laboratorio = laboratorio,
                            Denominacion = farmaco.Denominacion,
                            Precio = farmaco.PrecioMedio,
                            Stock = farmaco.Existencias ?? 0
                        };
                    }

                    var item = new PedidoDetalle
                    {
                        Linea = rLinea,
                        CantidadPedida = rCantPedida,
                        FarmacoCodigo = rArtCodigo,
                        EmpresaCodigo = rEmpCodigo,
                        PedidoCodigo = rPedido,
                        Farmaco = farmacoPedido
                    };

                    detalle.Add(item);
                }

                reader.Close();
                reader.Dispose();

                return detalle;
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                conn.Close();
                conn.Dispose();
            }
        }

        //public IEnumerable<Pedido> GetByIdGreaterOrEqual(long? pedido)
        //{
        //    using (var db = FarmaciaContext.Create(_config))
        //    {
        //        var sql = @"SELECT * From pedido WHERE IdPedido >= @pedido Order by IdPedido ASC";
        //        return db.Database.SqlQuery<Pedido>(sql,
        //            new SqlParameter("pedido", pedido ?? SqlInt64.Null))
        //            .ToList();
        //    }
        //}

        //public IEnumerable<Pedido> GetByFechaGreaterOrEqual(DateTime fecha)
        //{
        //    using (var db = FarmaciaContext.Create(_config))
        //    {
        //        var sql = @"SELECT * From pedido WHERE Fecha >= @fecha Order by IdPedido ASC";
        //        return db.Database.SqlQuery<Pedido>(sql,
        //            new SqlParameter("fecha", fecha))
        //            .ToList();
        //    }
        //}

        //public IEnumerable<LineaPedido> GetLineasByPedido(int pedido)
        //{
        //    using (var db = FarmaciaContext.Create(_config))
        //    {
        //        var sql = @"select * from lineaPedido where IdPedido = @pedido";
        //        return db.Database.SqlQuery<LineaPedido>(sql,
        //            new SqlParameter("pedido", pedido))
        //            .ToList();
        //    }
        //}
    }
}