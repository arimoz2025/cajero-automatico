// ============================================================
//  UI/menu.cs
//  Interfaz principal de consola del cajero automático.
//  Conecta todos los módulos: Autenticacion, Operaciones,
//  Historial y CambiarPIN en un flujo completo de sesión.
// ============================================================

using System;
using System.IO;
using System.Threading;
using CajeroAutomatico.Logica;
using CajeroAutomatico.Modelos;
using CajeroAutomatico.Base_de_Datos;

namespace CajeroAutomatico.UI
{
    public static class Menu
    {
        // ── Tiempo máximo de inactividad antes de cerrar sesión (segundos) ──
        private const int SEGUNDOS_INACTIVIDAD = 60;

        // ── Timestamp de la última acción del usuario ──
        private static DateTime _ultimaActividad;

        // ── Flag para detectar si el timer ya cerró la sesión ──
        private static bool _sesionExpirada = false;

        // ── Timer de inactividad ──
        private static Timer? _timerInactividad;

        // ════════════════════════════════════════════════════════
        //   PUNTO DE ENTRADA PRINCIPAL
        //   Program.cs llama a este método al iniciar.
        // ════════════════════════════════════════════════════════
        public static void Iniciar()
        {
            while (true)
            {
                // Pantalla de bienvenida del cajero
                MostrarPantallaBienvenida();

                // Esperar que el usuario presione ENTER para comenzar
                Console.Write("  Presione ENTER para iniciar...");
                Console.ReadLine();

                // Intentar autenticar al usuario
                Usuario? usuario = Autenticacion.IniciarSesion();

                // Si la autenticación falló (tarjeta incorrecta, bloqueado, etc.)
                if (usuario == null)
                {
                    MostrarError("No se pudo iniciar sesión. Inténtelo de nuevo.");
                    Pausa();
                    continue; // Volver a mostrar la pantalla de bienvenida
                }

                // Autenticación exitosa → iniciar sesión activa
                EjecutarSesion(usuario);
            }
        }

        // ════════════════════════════════════════════════════════
        //   SESIÓN ACTIVA
        //   Muestra el menú principal y gestiona cada opción.
        //   Incluye timer de inactividad y cierre automático.
        // ════════════════════════════════════════════════════════
        private static void EjecutarSesion(Usuario usuario)
        {
            // Reiniciar estado de sesión
            _sesionExpirada = false;
            _ultimaActividad = DateTime.Now;

            // Iniciar el timer de inactividad (revisa cada segundo)
            _timerInactividad = new Timer(VerificarInactividad, null,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1));

            // Instanciar los módulos de lógica
            var operaciones = new CajeroAutomatico.Operaciones.Operaciones();
            var historial   = new Historial();

            bool sesionActiva = true;

            while (sesionActiva && !_sesionExpirada)
            {
                // Recargar datos frescos del usuario desde la BD
                var dao = new UsuarioDAO();
                Usuario? usuarioActual = dao.ObtenerPorId(usuario.IdUsuario);

                if (usuarioActual == null)
                {
                    MostrarError("Error al cargar la cuenta. Sesión terminada.");
                    break;
                }

                // Mostrar menú y capturar opción
                MostrarMenuPrincipal(usuarioActual);

                // Si el timer expiró mientras el usuario leía el menú
                if (_sesionExpirada) break;

                string opcion = Console.ReadLine()?.Trim() ?? "";
                RegistrarActividad(); // Resetear timer con cada acción

                switch (opcion)
                {
                    case "1":
                        // Consultar saldo — delega a Operaciones
                        operaciones.EjecutarSesion_Consulta(usuarioActual);
                        break;

                    case "2":
                        // Depositar — delega a Operaciones
                        operaciones.EjecutarSesion_Deposito(usuarioActual);
                        break;

                    case "3":
                        // Retirar — delega a Operaciones
                        operaciones.EjecutarSesion_Retiro(usuarioActual);
                        break;

                    case "4":
                        // Historial de transacciones
                        historial.MostrarMenuHistorial(usuarioActual);
                        break;

                    case "5":
                        // Transferir dinero
                        operaciones.EjecutarSesion_Transferencia(usuarioActual);
                        break;

                    case "6":
                        // Cambiar PIN
                        Autenticacion.CambiarPIN(usuarioActual);
                        break;

                    case "7":
                        // Cerrar sesión voluntariamente
                        sesionActiva = false;
                        DetenerTimer();
                        Autenticacion.CerrarSesion(usuarioActual);
                        break;

                    default:
                        MostrarError("Opción no válida. Ingrese un número del 1 al 7.");
                        Pausa();
                        break;
                }

                RegistrarActividad(); // Resetear también después de cada operación
            }

            // Si la sesión expiró por inactividad (no por opción 6)
            if (_sesionExpirada)
            {
                DetenerTimer();
                MostrarSesionExpirada(usuario.NombreTitular);
            }
        }

        // ════════════════════════════════════════════════════════
        //   PANTALLAS DE LA INTERFAZ
        // ════════════════════════════════════════════════════════

        // Pantalla inicial del cajero (antes del login)
        private static void MostrarPantallaBienvenida()
        {
            try
            {
                Console.Clear();
            }
            catch (IOException)
            {
                // Ignorar error de consola no válida
            }
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════════════════╗");
            Console.WriteLine("  ║                                              ║");
            Console.WriteLine("  ║          B A N C O S I M   A T M             ║");
            Console.WriteLine("  ║                                              ║");
            Console.WriteLine("  ║        Universidad Don Bosco                 ║");
            Console.WriteLine("  ║        PAL404 — Programación de Algoritmos   ║");
            Console.WriteLine("  ║                                              ║");
            Console.WriteLine("  ╠══════════════════════════════════════════════╣");
            Console.ResetColor();
            Console.WriteLine("  ║                                              ║");
            Console.WriteLine($" ║ {DateTime.Now:dddd, dd 'de' MMMM 'de' yyyy}  ║");
            Console.WriteLine($" ║   Hora: {DateTime.Now:HH:mm:ss}              ║");
            Console.WriteLine("  ║                                              ║");
            Console.WriteLine("  ║   Por favor inserte su tarjeta               ║");
            Console.WriteLine("  ║   y presione ENTER para continuar.           ║");
            Console.WriteLine("  ║                                              ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════╝");
            Console.WriteLine();
        }

        // Menú principal con las 6 opciones
        private static void MostrarMenuPrincipal(Usuario usuario)
        {
            Console.Clear();
            Console.WriteLine();

            // Encabezado con datos del titular
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  ╔══════════════════════════════════════════════╗");
            Console.WriteLine("  ║           CAJERO AUTOMÁTICO — MENÚ           ║");
            Console.WriteLine("  ╠══════════════════════════════════════════════╣");
            Console.ResetColor();

            // Nombre del titular (recortado si es muy largo)
            string nombre = usuario.NombreTitular.Length > 36
                ? usuario.NombreTitular.Substring(0, 36)
                : usuario.NombreTitular;
            Console.WriteLine($"  ║  Titular : {nombre,-34}║");

            // Mostrar solo los últimos 4 dígitos de la tarjeta
            string tarjetaOculta = $"**** **** **** {usuario.NumeroTarjeta[^4..]}";
            Console.WriteLine($"  ║  Tarjeta : {tarjetaOculta,-34}║");

            // Tiempo restante de sesión
            int segundosRestantes = SEGUNDOS_INACTIVIDAD -
                (int)(DateTime.Now - _ultimaActividad).TotalSeconds;
            Console.Write("  ║  Sesión  : expira en ");
            Console.ForegroundColor = segundosRestantes <= 15
                ? ConsoleColor.Red : ConsoleColor.Yellow;
            Console.Write($"{Math.Max(0, segundosRestantes)}s");
            Console.ResetColor();
            string tiempoStr = $"{Math.Max(0, segundosRestantes)}s";
            Console.WriteLine(new string(' ', 34 - ("expira en ".Length + tiempoStr.Length)) + "║");

            Console.WriteLine("  ╠══════════════════════════════════════════════╣");

            // Opciones del menú
            Console.WriteLine("  ║                                              ║");
            Console.WriteLine("  ║   [1]  Consultar saldo                       ║");
            Console.WriteLine("  ║   [2]  Depositar dinero                      ║");
            Console.WriteLine("  ║   [3]  Retirar efectivo                      ║");
            Console.WriteLine("  ║   [4]  Ver historial de operaciones          ║");
            Console.WriteLine("  ║   [5]  Transferir dinero                     ║");
            Console.WriteLine("  ║   [6]  Cambiar PIN                           ║");
            Console.WriteLine("  ║   [7]  Finalizar sesión                      ║");
            Console.WriteLine("  ║                                              ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.Write("  Seleccione una opción: ");
        }

        // Pantalla de sesión cerrada por inactividad
        private static void MostrarSesionExpirada(string nombre)
        {
            Console.Clear();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  ╔══════════════════════════════════════════════╗");
            Console.WriteLine("  ║          SESIÓN CERRADA POR INACTIVIDAD      ║");
            Console.WriteLine("  ╠══════════════════════════════════════════════╣");
            Console.ResetColor();
            Console.WriteLine($" ║  Su sesión ha sido cerrada por seguridad.    ║");
            Console.WriteLine($" ║                                              ║");
            Console.WriteLine($" ║  Por favor, retire su tarjeta.               ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine($"  Hasta pronto, {nombre}.");
            Console.WriteLine();
            Pausa();
        }

        // ════════════════════════════════════════════════════════
        //   TIMER DE INACTIVIDAD
        // ════════════════════════════════════════════════════════

        // Se llama cada segundo desde el Timer para verificar inactividad
        private static void VerificarInactividad(object? state)
        {
            if (_sesionExpirada) return;

            double segundosSinActividad = (DateTime.Now - _ultimaActividad).TotalSeconds;

            if (segundosSinActividad >= SEGUNDOS_INACTIVIDAD)
            {
                _sesionExpirada = true;
                DetenerTimer();

                // Interrumpir el ReadLine actual en consola simulando ENTER
                // (el bucle principal detectará _sesionExpirada en la próxima iteración)
            }
        }

        // Actualiza el timestamp de la última actividad del usuario
        private static void RegistrarActividad()
        {
            _ultimaActividad = DateTime.Now;
        }

        // Detiene y libera el timer de inactividad
        private static void DetenerTimer()
        {
            _timerInactividad?.Dispose();
            _timerInactividad = null;
        }

        // ════════════════════════════════════════════════════════
        //   UTILIDADES DE PANTALLA
        // ════════════════════════════════════════════════════════

        private static void MostrarError(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n  [✘] {msg}");
            Console.ResetColor();
        }

        private static void Pausa()
        {
            Console.WriteLine("\n  Presione ENTER para continuar...");
            Console.ReadLine();
        }
    }
}