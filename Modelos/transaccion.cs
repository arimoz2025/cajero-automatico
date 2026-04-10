// ============================================================
//  Modelos/Transaccion.cs
//  Modelo de datos para el historial de operaciones
// ============================================================

namespace CajeroAutomatico.Modelos
{
    public enum TipoOperacion
    {
        RETIRO,
        DEPOSITO,
        CONSULTA
    }

    public class Transaccion
    {
        public int           IdTransaccion  { get; set; }
        public int           IdUsuario      { get; set; }
        public TipoOperacion TipoOperacion  { get; set; }
        public double        Monto          { get; set; }
        public double        SaldoAnterior  { get; set; }
        public double        SaldoPosterior { get; set; }
        public string?       Descripcion    { get; set; }
        public string        Fecha          { get; set; } = string.Empty;
        public bool          Exitosa        { get; set; } = true;

        // Para mostrar en UI
        public string TipoTexto => TipoOperacion.ToString();

        public string MontoFormateado =>
            TipoOperacion == TipoOperacion.RETIRO
                ? $"-${Monto:F2}"
                : TipoOperacion == TipoOperacion.DEPOSITO
                    ? $"+${Monto:F2}"
                    : "Consulta";
    }
}