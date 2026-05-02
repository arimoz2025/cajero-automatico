// ============================================================
//  Base_de_Datos/TransferenciaDAO.cs
//  CRUD para Transferencias entre usuarios
// ============================================================

using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using CajeroAutomatico.Modelos;

namespace CajeroAutomatico.Base_de_Datos
{
    public class TransferenciaDAO
    {
        // ----------------------------------------------------------------
        // HELPER
        // ----------------------------------------------------------------

        private static Transferencia MapearTransferencia(SqliteDataReader r) =>
            new Transferencia
            {
                IdTransferencia = r.GetInt32(r.GetOrdinal("id_transferencia")),
                IdUsuarioOrigen = r.GetInt32(r.GetOrdinal("id_usuario_origen")),
                IdUsuarioDestino = r.GetInt32(r.GetOrdinal("id_usuario_destino")),
                Monto = r.GetDouble(r.GetOrdinal("monto")),
                SaldoOrigenAntes = r.GetDouble(r.GetOrdinal("saldo_origen_antes")),
                SaldoOrigenDespues = r.GetDouble(r.GetOrdinal("saldo_origen_despues")),
                SaldoDestinoAntes = r.GetDouble(r.GetOrdinal("saldo_destino_antes")),
                SaldoDestinoDespues = r.GetDouble(r.GetOrdinal("saldo_destino_despues")),
                Descripcion = r.IsDBNull(r.GetOrdinal("descripcion"))
                    ? null
                    : r.GetString(r.GetOrdinal("descripcion")),
                Fecha = r.GetString(r.GetOrdinal("fecha"))
            };

        // ----------------------------------------------------------------
        // REGISTRAR TRANSFERENCIA
        // ----------------------------------------------------------------

        /// <summary>
        /// Registra una transferencia entre dos usuarios y actualiza ambos saldos
        /// de forma atómica (dentro de una transacción SQLite).
        /// </summary>
        public bool RegistrarTransferencia(
            int idUsuarioOrigen,
            int idUsuarioDestino,
            double monto,
            double saldoOrigenAntes,
            double saldoOrigenDespues,
            double saldoDestinoAntes,
            double saldoDestinoDespues,
            string? descripcion = null)
        {
            const string sqlTransferencia = @"
                INSERT INTO Transferencias
                    (id_usuario_origen, id_usuario_destino, monto,
                     saldo_origen_antes, saldo_origen_despues,
                     saldo_destino_antes, saldo_destino_despues,
                     descripcion)
                VALUES
                    (@idOrigen, @idDestino, @monto,
                     @saldoOrigenAntes, @saldoOrigenDespues,
                     @saldoDestinoAntes, @saldoDestinoDespues,
                     @desc);";

            const string sqlActualizarOrigen = @"
                UPDATE Usuarios SET saldo = @nuevoSaldo
                WHERE id_usuario = @idUsuario;";

            const string sqlActualizarDestino = @"
                UPDATE Usuarios SET saldo = @nuevoSaldo
                WHERE id_usuario = @idUsuario;";

            try
            {
                using var con = ConexionDB.ObtenerConexion();
                using var tx = con.BeginTransaction();
                try
                {
                    // Insertar transferencia
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = sqlTransferencia;
                        cmd.Parameters.AddWithValue("@idOrigen", idUsuarioOrigen);
                        cmd.Parameters.AddWithValue("@idDestino", idUsuarioDestino);
                        cmd.Parameters.AddWithValue("@monto", monto);
                        cmd.Parameters.AddWithValue("@saldoOrigenAntes", saldoOrigenAntes);
                        cmd.Parameters.AddWithValue("@saldoOrigenDespues", saldoOrigenDespues);
                        cmd.Parameters.AddWithValue("@saldoDestinoAntes", saldoDestinoAntes);
                        cmd.Parameters.AddWithValue("@saldoDestinoDespues", saldoDestinoDespues);
                        cmd.Parameters.AddWithValue("@desc", descripcion ?? (object)DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }

                    // Actualizar saldo del usuario origen
                    using (var cmdOrigen = con.CreateCommand())
                    {
                        cmdOrigen.Transaction = tx;
                        cmdOrigen.CommandText = sqlActualizarOrigen;
                        cmdOrigen.Parameters.AddWithValue("@nuevoSaldo", saldoOrigenDespues);
                        cmdOrigen.Parameters.AddWithValue("@idUsuario", idUsuarioOrigen);
                        cmdOrigen.ExecuteNonQuery();
                    }

                    // Actualizar saldo del usuario destino
                    using (var cmdDestino = con.CreateCommand())
                    {
                        cmdDestino.Transaction = tx;
                        cmdDestino.CommandText = sqlActualizarDestino;
                        cmdDestino.Parameters.AddWithValue("@nuevoSaldo", saldoDestinoDespues);
                        cmdDestino.Parameters.AddWithValue("@idUsuario", idUsuarioDestino);
                        cmdDestino.ExecuteNonQuery();
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
                Console.WriteLine($"[TransferenciaDAO.RegistrarTransferencia] Error: {ex.Message}");
                return false;
            }
        }

        // ----------------------------------------------------------------
        // CONSULTAS DE HISTORIAL
        // ----------------------------------------------------------------

        /// <summary>Devuelve todas las transferencias de un usuario (como origen o destino).</summary>
        public List<Transferencia> ObtenerHistorial(int idUsuario)
        {
            var lista = new List<Transferencia>();
            const string sql = @"
                SELECT * FROM Transferencias
                WHERE id_usuario_origen = @idU OR id_usuario_destino = @idU
                ORDER BY fecha DESC;";

            try
            {
                using var con = ConexionDB.ObtenerConexion();
                using var cmd = con.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@idU", idUsuario);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    lista.Add(MapearTransferencia(reader));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TransferenciaDAO.ObtenerHistorial] Error: {ex.Message}");
            }

            return lista;
        }

        /// <summary>Devuelve las últimas N transferencias de un usuario.</summary>
        public List<Transferencia> ObtenerUltimas(int idUsuario, int cantidad = 10)
        {
            var lista = new List<Transferencia>();
            const string sql = @"
                SELECT * FROM Transferencias
                WHERE id_usuario_origen = @idU OR id_usuario_destino = @idU
                ORDER BY fecha DESC
                LIMIT @cantidad;";

            try
            {
                using var con = ConexionDB.ObtenerConexion();
                using var cmd = con.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@idU", idUsuario);
                cmd.Parameters.AddWithValue("@cantidad", cantidad);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    lista.Add(MapearTransferencia(reader));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TransferenciaDAO.ObtenerUltimas] Error: {ex.Message}");
            }

            return lista;
        }
    }
}