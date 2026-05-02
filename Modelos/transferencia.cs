// ============================================================
//  Modelos/transferencia.cs
//  Modelo de datos para las transferencias entre usuarios
// ============================================================

namespace CajeroAutomatico.Modelos
{
    public class Transferencia
    {
        public int IdTransferencia { get; set; }
        public int IdUsuarioOrigen { get; set; }
        public int IdUsuarioDestino { get; set; }
        public double Monto { get; set; }
        public double SaldoOrigenAntes { get; set; }
        public double SaldoOrigenDespues { get; set; }
        public double SaldoDestinoAntes { get; set; }
        public double SaldoDestinoDespues { get; set; }
        public string? Descripcion { get; set; }
        public string Fecha { get; set; } = string.Empty;
    }
}