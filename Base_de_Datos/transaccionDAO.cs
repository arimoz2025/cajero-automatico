// ============================================================
//  Base_de_Datos/TransaccionDAO.cs  (feature-database)
//  CRUD para Transacciones + registro de auditoría de sesiones
// ============================================================

using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using CajeroAutomatico.Modelos;

namespace CajeroAutomatico.Base_de_Datos
{
    public class TransaccionDAO
    {
        // ----------------------------------------------------------------
        // HELPER
        // ----------------------------------------------------------------

        private static Transaccion MapearTransaccion(SqliteDataReader r) =>
            new Transaccion
            {
                IdTransaccion  = r.GetInt32(r.GetOrdinal("id_transaccion")),
                IdUsuario      = r.GetInt32(r.GetOrdinal("id_usuario")),
                TipoOperacion  = Enum.Parse<TipoOperacion>(
                                     r.GetString(r.GetOrdinal("tipo_operacion"))),
                Monto          = r.GetDouble(r.GetOrdinal("monto")),
                SaldoAnterior  = r.GetDouble(r.GetOrdinal("saldo_anterior")),
                SaldoPosterior = r.GetDouble(r.GetOrdinal("saldo_posterior")),
                Descripcion    = r.IsDBNull(r.GetOrdinal("descripcion"))
                                     ? null
                                     : r.GetString(r.GetOrdinal("descripcion")),
                Fecha          = r.GetString(r.GetOrdinal("fecha")),
                Exitosa        = r.GetInt32(r.GetOrdinal("exitosa")) == 1
            };

        // ----------------------------------------------------------------
        // REGISTRAR TRANSACCIÓN
        // ----------------------------------------------------------------

        /// <summary>
        /// Inserta una transacción Y actualiza el saldo del usuario
        /// de forma atómica (dentro de una transacción SQLite).
        /// </summary>
        public bool RegistrarTransaccion(
            int           idUsuario,
            TipoOperacion tipo,
            double        monto,
            double        saldoAnterior,
            double        saldoPosterior,
            string?       descripcion = null,
            bool          exitosa = true)
        {
            const string sqlTransaccion = @"
                INSERT INTO Transacciones
                    (id_usuario, tipo_operacion, monto,
                     saldo_anterior, saldo_posterior, descripcion, exitosa)
                VALUES
                    (@idU, @tipo, @monto, @saldoAnt, @saldoPost, @desc, @exitosa);";

            const string sqlSaldo = @"
                UPDATE Usuarios SET saldo = @nuevoSaldo
                WHERE id_usuario = @idU;";

            try
            {
                using var con = ConexionDB.ObtenerConexion();
                using var tx  = con.BeginTransaction();
                try
                {
                    // Insertar transacción
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = sqlTransaccion;
                        cmd.Parameters.AddWithValue("@idU",      idUsuario);
                        cmd.Parameters.AddWithValue("@tipo",     tipo.ToString());
                        cmd.Parameters.AddWithValue("@monto",    monto);
                        cmd.Parameters.AddWithValue("@saldoAnt", saldoAnterior);
                        cmd.Parameters.AddWithValue("@saldoPost",saldoPosterior);
                        cmd.Parameters.AddWithValue("@desc",     descripcion ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@exitosa",  exitosa ? 1 : 0);
                        cmd.ExecuteNonQuery();
                    }

                    // Actualizar saldo solo si la operación fue exitosa
                    if (exitosa && tipo != TipoOperacion.CONSULTA)
                    {
                        using var cmdSaldo = con.CreateCommand();
                        cmdSaldo.Transaction = tx;
                        cmdSaldo.CommandText = sqlSaldo;
                        cmdSaldo.Parameters.AddWithValue("@nuevoSaldo", saldoPosterior);
                        cmdSaldo.Parameters.AddWithValue("@idU",        idUsuario);
                        cmdSaldo.ExecuteNonQuery();
                    }

                    tx.Commit();
                    return true;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TransaccionDAO.RegistrarTransaccion] Error: {ex.Message}");
                return false;
            }
        }

        // ----------------------------------------------------------------
        // CONSULTAS DE HISTORIAL
        // ----------------------------------------------------------------

        /// <summary>Devuelve todas las transacciones de un usuario.</summary>
        public List<Transaccion> ObtenerHistorial(int idUsuario)
        {
            var lista = new List<Transaccion>();
            const string sql = @"
                SELECT * FROM Transacciones
                WHERE id_usuario = @idU
                ORDER BY fecha DESC;";

            try
            {
                using var con = ConexionDB.ObtenerConexion();
                using var cmd = con.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@idU", idUsuario);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    lista.Add(MapearTransaccion(reader));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TransaccionDAO.ObtenerHistorial] Error: {ex.Message}");
            }

            return lista;
        }

        /// <summary>
        /// Devuelve las últimas N transacciones de un usuario.
        /// Por defecto las últimas 10.
        /// </summary>
        public List<Transaccion> ObtenerUltimas(int idUsuario, int cantidad = 10)
        {
            var lista = new List<Transaccion>();
            const string sql = @"
                SELECT * FROM Transacciones
                WHERE id_usuario = @idU
                ORDER BY fecha DESC
                LIMIT @cantidad;";

            try
            {
                using var con = ConexionDB.ObtenerConexion();
                using var cmd = con.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@idU",      idUsuario);
                cmd.Parameters.AddWithValue("@cantidad", cantidad);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    lista.Add(MapearTransaccion(reader));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TransaccionDAO.ObtenerUltimas] Error: {ex.Message}");
            }

            return lista;
        }

        /// <summary>Filtra el historial por tipo de operación.</summary>
        public List<Transaccion> ObtenerPorTipo(int idUsuario, TipoOperacion tipo)
        {
            var lista = new List<Transaccion>();
            const string sql = @"
                SELECT * FROM Transacciones
                WHERE id_usuario = @idU AND tipo_operacion = @tipo
                ORDER BY fecha DESC;";

            try
            {
                using var con = ConexionDB.ObtenerConexion();
                using var cmd = con.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@idU",  idUsuario);
                cmd.Parameters.AddWithValue("@tipo", tipo.ToString());

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    lista.Add(MapearTransaccion(reader));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TransaccionDAO.ObtenerPorTipo] Error: {ex.Message}");
            }

            return lista;
        }

        // ----------------------------------------------------------------
        // AUDITORÍA DE SESIONES
        // ----------------------------------------------------------------

        /// <summary>Registra un intento de acceso (exitoso o fallido).</summary>
        public bool RegistrarSesion(string numeroTarjeta, bool exitosa, string? ip = null)
        {
            const string sql = @"
                INSERT INTO SesionesAuditoria (numero_tarjeta, exitosa, ip_origen)
                VALUES (@tarjeta, @exitosa, @ip);";

            try
            {
                using var con = ConexionDB.ObtenerConexion();
                using var cmd = con.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@tarjeta", numeroTarjeta);
                cmd.Parameters.AddWithValue("@exitosa", exitosa ? 1 : 0);
                cmd.Parameters.AddWithValue("@ip",      ip ?? (object)DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TransaccionDAO.RegistrarSesion] Error: {ex.Message}");
                return false;
            }
        }
    }
}