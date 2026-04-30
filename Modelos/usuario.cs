// ============================================================
//  Modelos/usuario.cs
//  Modelo de datos para los usuarios del cajero automático
// ============================================================

using System;

namespace CajeroAutomatico.Modelos
{
    public class Usuario
    {
        public int IdUsuario { get; set; }
        public string NombreTitular { get; set; } = string.Empty;
        public string NumeroTarjeta { get; set; } = string.Empty;
        public string Pin { get; set; } = string.Empty; // Hash SHA-256 del PIN
        public double Saldo { get; set; }
        public int IntentosFallidos { get; set; }
        public bool Bloqueado { get; set; }
        public string FechaCreacion { get; set; } = string.Empty;
        public string FechaActualizacion { get; set; } = string.Empty;
    }
}