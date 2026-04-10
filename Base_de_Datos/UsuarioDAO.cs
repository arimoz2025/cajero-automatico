// ============================================================
//  Base_de_Datos/UsuarioDAO.cs
//  CRUD completo para la tabla Usuarios
//  + lógica de autenticación y bloqueo de cuenta
// ============================================================

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using CajeroAutomatico.Modelos;

namespace CajeroAutomatico.Base_de_Datos
{
    public class UsuarioDAO
    {
        // ----------------------------------------------------------------
        // HELPERS PRIVADOS
        // ----------------------------------------------------------------

        /// <summary>Convierte un PIN plano en hash SHA-256.</summary>
        public static string HashearPin(string pin)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(pin));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        /// <summary>Mapea una fila del reader a un objeto Usuario.</summary>
        private static Usuario MapearUsuario(SqliteDataReader r) => new Usuario
        {
            IdUsuario          = r.GetInt32(r.GetOrdinal("id_usuario")),
            NombreTitular      = r.GetString(r.GetOrdinal("nombre_titular")),
            NumeroTarjeta      = r.GetString(r.GetOrdinal("numero_tarjeta")),
            Pin                = r.GetString(r.GetOrdinal("pin")),
            Saldo              = r.GetDouble(r.GetOrdinal("saldo")),
            IntentosFallidos   = r.GetInt32(r.GetOrdinal("intentos_fallidos")),
            Bloqueado          = r.GetInt32(r.GetOrdinal("bloqueado")) == 1,
            FechaCreacion      = r.GetString(r.GetOrdinal("fecha_creacion")),
            FechaActualizacion = r.GetString(r.GetOrdinal("fecha_actualizacion"))
        };

        // ----------------------------------------------------------------
        // CREATE
        // ----------------------------------------------------------------

        /// <summary>Inserta un nuevo titular en la base de datos.</summary>
        /// <returns>El id generado, o -1 si hubo error.</returns>
        public int CrearUsuario(string nombreTitular, string numeroTarjeta,
                                string pin, double saldoInicial = 0.00)
        {
            const string sql = @"
                INSERT INTO Usuarios (nombre_titular, numero_tarjeta, pin, saldo)
                VALUES (@nombre, @tarjeta, @pin, @saldo);
                SELECT last_insert_rowid();";

            try
            {
                using var con = ConexionDB.ObtenerConexion();
                using var cmd = con.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@nombre",  nombreTitular);
                cmd.Parameters.AddWithValue("@tarjeta", numeroTarjeta);
                cmd.Parameters.AddWithValue("@pin",     HashearPin(pin));
                cmd.Parameters.AddWithValue("@saldo",   saldoInicial);

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UsuarioDAO.CrearUsuario] Error: {ex.Message}");
                return -1;
            }
        }

        // ----------------------------------------------------------------
        // READ
        // ----------------------------------------------------------------

        /// <summary>Busca un usuario por número de tarjeta.</summary>
        public Usuario? ObtenerPorTarjeta(string numeroTarjeta)
        {
            const string sql = @"
                SELECT * FROM Usuarios
                WHERE numero_tarjeta = @tarjeta
                LIMIT 1;";

            try
            {
                using var con = ConexionDB.ObtenerConexion();
                using var cmd = con.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@tarjeta", numeroTarjeta);

                using var reader = cmd.ExecuteReader();
                return reader.Read() ? MapearUsuario(reader) : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UsuarioDAO.ObtenerPorTarjeta] Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>Busca un usuario por su ID.</summary>
        public Usuario? ObtenerPorId(int idUsuario)
        {
            const string sql = "SELECT * FROM Usuarios WHERE id_usuario = @id LIMIT 1;";

            try
            {
                using var con = ConexionDB.ObtenerConexion();
                using var cmd = con.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", idUsuario);

                using var reader = cmd.ExecuteReader();
                return reader.Read() ? MapearUsuario(reader) : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UsuarioDAO.ObtenerPorId] Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>Devuelve todos los usuarios (útil para administración).</summary>
        public List<Usuario> ObtenerTodos()
        {
            var lista = new List<Usuario>();
            const string sql = "SELECT * FROM Usuarios ORDER BY nombre_titular;";

            try
            {
                using var con = ConexionDB.ObtenerConexion();
                using var cmd = con.CreateCommand();
                cmd.CommandText = sql;

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    lista.Add(MapearUsuario(reader));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UsuarioDAO.ObtenerTodos] Error: {ex.Message}");
            }

            return lista;
        }

        // ----------------------------------------------------------------
        // UPDATE – saldo
        // ----------------------------------------------------------------

        /// <summary>Actualiza el saldo del usuario.</summary>
        public bool ActualizarSaldo(int idUsuario, double nuevoSaldo)
        {
            const string sql = @"
                UPDATE Usuarios SET saldo = @saldo
                WHERE id_usuario = @id;";

            try
            {
                using var con = ConexionDB.ObtenerConexion();
                using var cmd = con.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@saldo", nuevoSaldo);
                cmd.Parameters.AddWithValue("@id",    idUsuario);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UsuarioDAO.ActualizarSaldo] Error: {ex.Message}");
                return false;
            }
        }

        // ----------------------------------------------------------------
        // UPDATE – intentos y bloqueo
        // ----------------------------------------------------------------

        /// <summary>Incrementa el contador de intentos fallidos.</summary>
        public bool IncrementarIntentosFallidos(int idUsuario)
        {
            const string sql = @"
                UPDATE Usuarios SET intentos_fallidos = intentos_fallidos + 1
                WHERE id_usuario = @id;";

            try
            {
                using var con = ConexionDB.ObtenerConexion();
                using var cmd = con.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", idUsuario);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UsuarioDAO.IncrementarIntentosFallidos] Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>Reinicia el contador de intentos tras un login exitoso.</summary>
        public bool ReiniciarIntentos(int idUsuario)
        {
            const string sql = @"
                UPDATE Usuarios SET intentos_fallidos = 0
                WHERE id_usuario = @id;";

            try
            {
                using var con = ConexionDB.ObtenerConexion();
                using var cmd = con.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", idUsuario);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UsuarioDAO.ReiniciarIntentos] Error: {ex.Message}");
                return false;
            }
        }

        // ----------------------------------------------------------------
        // DELETE
        // ----------------------------------------------------------------

        /// <summary>Elimina un usuario por ID (uso administrativo).</summary>
        public bool EliminarUsuario(int idUsuario)
        {
            const string sql = "DELETE FROM Usuarios WHERE id_usuario = @id;";

            try
            {
                using var con = ConexionDB.ObtenerConexion();
                using var cmd = con.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", idUsuario);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UsuarioDAO.EliminarUsuario] Error: {ex.Message}");
                return false;
            }
        }

        // ----------------------------------------------------------------
        // AUTENTICACIÓN
        // ----------------------------------------------------------------

        public enum ResultadoAutenticacion
        {
            Exitoso,
            TarjetaNoEncontrada,
            PinIncorrecto,
            CuentaBloqueada
        }

        /// <summary>
        /// Autentica al usuario con tarjeta + PIN.
        /// Gestiona los intentos fallidos y el bloqueo automático.
        /// </summary>
        /// <param name="usuarioAutenticado">Usuario si el login fue exitoso.</param>
        public ResultadoAutenticacion Autenticar(
            string numeroTarjeta,
            string pin,
            out Usuario? usuarioAutenticado)
        {
            usuarioAutenticado = null;
            var usuario = ObtenerPorTarjeta(numeroTarjeta);

            if (usuario == null)
                return ResultadoAutenticacion.TarjetaNoEncontrada;

            if (usuario.Bloqueado)
                return ResultadoAutenticacion.CuentaBloqueada;

            if (usuario.Pin != HashearPin(pin))
            {
                IncrementarIntentosFallidos(usuario.IdUsuario);

                // Recargar para ver si el trigger ya lo bloqueó
                var actualizado = ObtenerPorId(usuario.IdUsuario);
                if (actualizado?.Bloqueado == true)
                    return ResultadoAutenticacion.CuentaBloqueada;

                return ResultadoAutenticacion.PinIncorrecto;
            }

            // Login correcto → limpiar intentos
            ReiniciarIntentos(usuario.IdUsuario);
            usuarioAutenticado = ObtenerPorId(usuario.IdUsuario); // datos frescos
            return ResultadoAutenticacion.Exitoso;
        }

        /// <summary>Devuelve los intentos restantes antes del bloqueo.</summary>
        public int IntentosRestantes(int idUsuario)
        {
            var u = ObtenerPorId(idUsuario);
            return u == null ? 0 : Math.Max(0, 3 - u.IntentosFallidos);
        }
    }
}