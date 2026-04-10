// ============================================================
//  Base_de_Datos/conexionDB.cs
//  Gestión de la conexión a SQLite y creación de la BD
// ============================================================

using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace CajeroAutomatico.Base_de_Datos
{
    public static class ConexionDB
    {
        // Ruta al archivo de base de datos (en la raíz del proyecto)
        private static readonly string _rutaBD = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "cajero.db");

        // Cadena de conexión reutilizable
        public static string CadenaConexion =>
            $"Data Source={_rutaBD};";

        /// <summary>
        /// Devuelve una conexión abierta lista para usar.
        /// El llamador es responsable de cerrarla (using).
        /// </summary>
        public static SqliteConnection ObtenerConexion()
        {
            var conexion = new SqliteConnection(CadenaConexion);
            conexion.Open();

            // Activar claves foráneas en cada conexión (SQLite lo requiere)
            using var cmd = conexion.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys = ON;";
            cmd.ExecuteNonQuery();

            return conexion;
        }

        /// <summary>
        /// Crea las tablas, índices y triggers si no existen,
        /// ejecutando el script init.sql.
        /// Llamar una sola vez al iniciar la aplicación.
        /// </summary>
        public static void InicializarBaseDeDatos()
        {
            // Buscar init.sql relativo al ejecutable
            string rutaScript = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Scripts", "init.sql");

            if (!File.Exists(rutaScript))
                throw new FileNotFoundException(
                    $"No se encontró el script de inicialización: {rutaScript}");

            string sql = File.ReadAllText(rutaScript);

            using var conexion = ObtenerConexion();
            using var cmd = conexion.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();

            Console.WriteLine("[DB] Base de datos inicializada correctamente.");
        }

        /// <summary>
        /// Verifica que la conexión funcione correctamente.
        /// </summary>
        public static bool ProbarConexion()
        {
            try
            {
                using var conexion = ObtenerConexion();
                return conexion.State == System.Data.ConnectionState.Open;
            }
            catch
            {
                return false;
            }
        }
    }
}