// ============================================================
//  Logica/historial.cs
//  Módulo de historial de transacciones del cajero automático:
//    - Ver todas las transacciones
//    - Ver las últimas N transacciones
//    - Filtrar por tipo (retiro, depósito, consulta)
//  Usa los métodos de TransaccionDAO para obtener los datos
// ============================================================

using System;
using System.Collections.Generic;
using CajeroAutomatico.Base_de_Datos;
using CajeroAutomatico.Modelos;

namespace CajeroAutomatico.Logica
{
    public class Historial
    {
        // DAO para consultar las transacciones en la base de datos
        private TransaccionDAO _transaccionDAO;

        // Constructor: se crea el DAO al instanciar la clase
        public Historial()
        {
            _transaccionDAO = new TransaccionDAO();
        }

        // ══════════════════════════════════════════════════════
        //   MENÚ PRINCIPAL DEL HISTORIAL
        //   El menú de tu compañero llama a este método
        //   pasando el usuario ya autenticado.
        // ══════════════════════════════════════════════════════
        public void MostrarMenuHistorial(Usuario usuario)
        {
            bool enHistorial = true;

            while (enHistorial)
            {
                Console.Clear();
                Console.WriteLine();
                Console.WriteLine("  ╔══════════════════════════════════╗");
                Console.WriteLine("  ║     HISTORIAL DE OPERACIONES     ║");
                Console.WriteLine("  ╠══════════════════════════════════╣");
                Console.WriteLine("  ║  [1]  Ver últimos movimientos    ║");
                Console.WriteLine("  ║  [2]  Ver historial completo     ║");
                Console.WriteLine("  ║  [3]  Filtrar por tipo           ║");
                Console.WriteLine("  ║  [4]  Volver al menú principal   ║");
                Console.WriteLine("  ╚══════════════════════════════════╝");
                Console.WriteLine();
                Console.Write("  Seleccione una opción: ");

                string opcion = Console.ReadLine()?.Trim() ?? "";

                switch (opcion)
                {
                    case "1":
                        MostrarUltimos(usuario);
                        break;

                    case "2":
                        MostrarTodo(usuario);
                        break;

                    case "3":
                        MostrarFiltrado(usuario);
                        break;

                    case "4":
                        enHistorial = false;
                        break;

                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\n  [!] Opción no válida. Ingrese un número del 1 al 4.");
                        Console.ResetColor();
                        Pausa();
                        break;
                }
            }
        }

        // ══════════════════════════════════════════════════════
        //   OPCIÓN 1: VER ÚLTIMOS MOVIMIENTOS
        //   Muestra las últimas 10 transacciones del usuario
        // ══════════════════════════════════════════════════════
        private void MostrarUltimos(Usuario usuario)
        {
            Console.Clear();
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════╗");
            Console.WriteLine("  ║    ÚLTIMOS 10 MOVIMIENTOS        ║");
            Console.WriteLine("  ╚══════════════════════════════════╝");

            // Pedir las últimas 10 transacciones al DAO
            List<Transaccion> lista = _transaccionDAO.ObtenerUltimas(usuario.IdUsuario, 10);

            if (lista.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n  [!] No hay movimientos registrados.");
                Console.ResetColor();
            }
            else
            {
                ImprimirTabla(lista);
            }

            Pausa();
        }

        // ══════════════════════════════════════════════════════
        //   OPCIÓN 2: VER HISTORIAL COMPLETO
        //   Muestra todas las transacciones del usuario
        // ══════════════════════════════════════════════════════
        private void MostrarTodo(Usuario usuario)
        {
            Console.Clear();
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════╗");
            Console.WriteLine("  ║      HISTORIAL COMPLETO          ║");
            Console.WriteLine("  ╚══════════════════════════════════╝");

            // Pedir todas las transacciones al DAO
            List<Transaccion> lista = _transaccionDAO.ObtenerHistorial(usuario.IdUsuario);

            if (lista.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n  [!] No hay movimientos registrados.");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"\n  Total de movimientos: {lista.Count}");
                ImprimirTabla(lista);
            }

            Pausa();
        }

        // ══════════════════════════════════════════════════════
        //   OPCIÓN 3: FILTRAR POR TIPO
        //   El usuario elige: retiros, depósitos o consultas
        // ══════════════════════════════════════════════════════
        private void MostrarFiltrado(Usuario usuario)
        {
            Console.Clear();
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════╗");
            Console.WriteLine("  ║       FILTRAR POR TIPO           ║");
            Console.WriteLine("  ╠══════════════════════════════════╣");
            Console.WriteLine("  ║  [1]  Retiros                    ║");
            Console.WriteLine("  ║  [2]  Depósitos                  ║");
            Console.WriteLine("  ║  [3]  Consultas de saldo         ║");
            Console.WriteLine("  ╚══════════════════════════════════╝");
            Console.WriteLine();
            Console.Write("  Seleccione el tipo: ");

            string opcion = Console.ReadLine()?.Trim() ?? "";

            // Determinar qué tipo de operación filtrar
            TipoOperacion tipo;
            string nombreTipo;

            switch (opcion)
            {
                case "1":
                    tipo = TipoOperacion.RETIRO;
                    nombreTipo = "RETIROS";
                    break;
                case "2":
                    tipo = TipoOperacion.DEPOSITO;
                    nombreTipo = "DEPÓSITOS";
                    break;
                case "3":
                    tipo = TipoOperacion.CONSULTA;
                    nombreTipo = "CONSULTAS";
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n  [!] Opción no válida.");
                    Console.ResetColor();
                    Pausa();
                    return;
            }

            // Pedir las transacciones filtradas al DAO
            List<Transaccion> lista = _transaccionDAO.ObtenerPorTipo(usuario.IdUsuario, tipo);

            Console.Clear();
            Console.WriteLine();
            Console.WriteLine($"  ── HISTORIAL DE {nombreTipo} ──");

            if (lista.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n  [!] No se encontraron {nombreTipo.ToLower()}.");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"  Se encontraron {lista.Count} registro(s).\n");
                ImprimirTabla(lista);
            }

            Pausa();
        }

        // ══════════════════════════════════════════════════════
        //   MÉTODO AUXILIAR: IMPRIMIR TABLA DE TRANSACCIONES
        //   Recorre la lista y muestra cada transacción
        //   en un formato de tabla legible en la consola
        // ══════════════════════════════════════════════════════
        private void ImprimirTabla(List<Transaccion> lista)
        {
            Console.WriteLine();

            // Encabezado de la tabla
            Console.WriteLine("  ┌──────┬────────────┬──────────────┬──────────────┬──────────────────────┐");
            Console.WriteLine("  │  #   │   Tipo     │    Monto     │    Saldo     │   Fecha              │");
            Console.WriteLine("  ├──────┼────────────┼──────────────┼──────────────┼──────────────────────┤");

            // Recorrer cada transacción con un contador
            int numero = 1;

            foreach (Transaccion t in lista)
            {
                // Elegir color según el tipo de operación
                if (t.TipoOperacion == TipoOperacion.RETIRO)
                    Console.ForegroundColor = ConsoleColor.Red;
                else if (t.TipoOperacion == TipoOperacion.DEPOSITO)
                    Console.ForegroundColor = ConsoleColor.Green;
                else
                    Console.ForegroundColor = ConsoleColor.Cyan;

                // Formatear el tipo para que tenga largo fijo (8 caracteres)
                string tipoTexto = t.TipoOperacion.ToString();

                // Formatear el monto con signo
                string montoTexto = t.MontoFormateado;

                // Imprimir la fila
                Console.WriteLine("  │ {0,-4} │ {1,-10} │ {2,-12} │ $ {3,-10:F2} │ {4,-20} │",
                    numero,
                    tipoTexto,
                    montoTexto,
                    t.SaldoPosterior,
                    t.Fecha);

                Console.ResetColor();
                numero++;
            }

            // Pie de la tabla
            Console.WriteLine("  └──────┴────────────┴──────────────┴──────────────┴──────────────────────┘");
            Console.WriteLine();
        }

        // Pausa para que el usuario lea antes de continuar
        private void Pausa()
        {
            Console.WriteLine("\n  Presione ENTER para continuar...");
            Console.ReadLine();
        }
    }
}
