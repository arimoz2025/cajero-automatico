// ============================================================
//  Operaciones/Operaciones.cs
//  Módulo de operaciones del cajero automático:
//    - Consulta de saldo
//    - Depósito
//    - Retiro de efectivo
//  Conectado a la base de datos mediante TransaccionDAO y UsuarioDAO
// ============================================================

using System;
using CajeroAutomatico.Base_de_Datos;  // Para usar TransaccionDAO y UsuarioDAO
using CajeroAutomatico.Modelos;        // Para usar Usuario y TipoOperacion

namespace CajeroAutomatico.Operaciones
{
    public class Operaciones
    {
        // ── Constantes del sistema ─────────────────────────────
        // Si la ingeniera pide cambiar algún límite, solo se toca aquí

        private const double LIMITE_RETIRO_DIARIO = 1000.00;  // Máximo que puede retirar en un día
        private const double MONTO_MAXIMO_OPERACION = 10000.00; // Límite por operación individual
        private const double MULTIPLO_RETIRO = 5.00;     // Solo se puede retirar en múltiplos de $5

        // ── DAOs: son los que hablan con la base de datos ──────
        private readonly TransaccionDAO _transaccionDAO; // Para registrar movimientos
        private readonly UsuarioDAO _usuarioDAO;     // Para leer y actualizar el saldo

        // ── Constructor: se crean los DAOs al iniciar ──────────
        public Operaciones()
        {
            _transaccionDAO = new TransaccionDAO();
            _usuarioDAO = new UsuarioDAO();
        }

        // ══════════════════════════════════════════════════════
        //   PUNTO DE ENTRADA
        //   El módulo de autenticación llama a este método
        //   pasando el usuario ya autenticado.
        //   El menú se repite hasta que el usuario elija salir.
        // ══════════════════════════════════════════════════════
        public void EjecutarSesion(Usuario usuario)
        {
            bool sesionActiva = true; // Controla si el menú sigue mostrándose

            while (sesionActiva)
            {
                // Siempre cargar el saldo fresco desde la BD antes de mostrar el menú
                // (por si otro proceso lo cambió)
                Usuario? usuarioActual = _usuarioDAO.ObtenerPorId(usuario.IdUsuario);

                // Si por alguna razón no se encontró el usuario, cerrar sesión
                if (usuarioActual == null)
                {
                    MensajeError("Error al cargar los datos de la cuenta. Sesión terminada.");
                    break;
                }

                // Mostrar el menú con el nombre del titular
                MostrarMenu(usuarioActual.NombreTitular);

                // Leer la opción que escribe el usuario
                string opcion = Console.ReadLine()?.Trim() ?? "";

                // Switch case: ejecutar la operación que eligió
                switch (opcion)
                {
                    case "1":
                        // Opción 1: Ver saldo y datos de la cuenta
                        OperacionConsultarSaldo(usuarioActual);
                        break;

                    case "2":
                        // Opción 2: Ingresar dinero a la cuenta
                        OperacionDepositar(usuarioActual);
                        break;

                    case "3":
                        // Opción 3: Sacar dinero de la cuenta
                        OperacionRetirar(usuarioActual);
                        break;

                    case "4":
                        // Opción 4: Cerrar sesión y salir
                        sesionActiva = false;
                        MostrarSesionFinalizada();
                        break;

                    default:
                        // El usuario escribió algo que no es 1, 2, 3 o 4
                        MensajeError("Opción no válida. Ingrese un número del 1 al 4.");
                        Pausa();
                        break;
                }
            }
        }
        // Métodos públicos para llamar operaciones individuales desde Menu.cs
        public void EjecutarSesion_Consulta(Usuario usuario) => OperacionConsultarSaldo(usuario);
        public void EjecutarSesion_Deposito(Usuario usuario) => OperacionDepositar(usuario);
        public void EjecutarSesion_Retiro(Usuario usuario)   => OperacionRetirar(usuario);
        public void EjecutarSesion_Transferencia(Usuario usuario) => OperacionTransferir(usuario);

        // ══════════════════════════════════════════════════════
        //   OPERACIÓN 1: CONSULTAR SALDO
        //
        //   Muestra los datos de la cuenta en pantalla.
        //   Registra la consulta en la base de datos
        //   usando TransaccionDAO con tipo CONSULTA.
        // ══════════════════════════════════════════════════════
        private void OperacionConsultarSaldo(Usuario usuario)
        {
            LimpiarPantalla();
            DibujarEncabezado("CONSULTA DE SALDO");

            // Mostrar los datos de la cuenta
            Console.WriteLine();
            Console.WriteLine("  Titular   : {0}", usuario.NombreTitular);
            Console.WriteLine("  Tarjeta   : {0}", usuario.NumeroTarjeta);
            Console.WriteLine("  ─────────────────────────────────");
            // F2 = mostrar siempre con 2 decimales (ej: 850.00)
            Console.WriteLine("  Saldo disponible: $ {0:F2}", usuario.Saldo);
            Console.WriteLine("  ─────────────────────────────────");
            Console.WriteLine();

            // Registrar la consulta en la base de datos
            // saldoAnterior y saldoPosterior son iguales porque no se movió dinero
            // monto = 0 porque no hubo movimiento de dinero
            bool guardado = _transaccionDAO.RegistrarTransaccion(
                idUsuario: usuario.IdUsuario,
                tipo: TipoOperacion.CONSULTA,
                monto: 0,
                saldoAnterior: usuario.Saldo,
                saldoPosterior: usuario.Saldo,
                descripcion: "Consulta de saldo",
                exitosa: true
            );

            // Avisar si no se pudo guardar en la BD (no es crítico para consulta)
            if (!guardado)
                MensajeAdvertencia("Advertencia: no se pudo registrar la consulta en el historial.");

            MensajeExito("Información mostrada correctamente.");
            Pausa();
        }

        // ══════════════════════════════════════════════════════
        //   OPERACIÓN 2: DEPOSITAR DINERO
        //
        //   Permite al usuario ingresar dinero a su cuenta.
        //   Flujo: pedir monto → validar → confirmar → guardar en BD → comprobante
        //
        //   La BD se actualiza con TransaccionDAO.RegistrarTransaccion()
        //   que en un solo paso guarda el movimiento Y actualiza el saldo.
        // ══════════════════════════════════════════════════════
        private void OperacionDepositar(Usuario usuario)
        {
            LimpiarPantalla();
            DibujarEncabezado("DEPÓSITO");

            // Mostrar saldo actual antes de pedir el monto
            Console.WriteLine("  Saldo actual: $ {0:F2}", usuario.Saldo);
            Console.WriteLine();

            // ── Paso 1: Pedir el monto ─────────────────────────
            // PedirMonto devuelve -1 si el usuario escribe 0 o C para cancelar
            double monto = PedirMonto("  Ingrese el monto a depositar: $");

            if (monto == -1) // Usuario canceló
            {
                MensajeAdvertencia("Operación cancelada.");
                Pausa();
                return; // Salir y volver al menú
            }

            // ── Paso 2: Validaciones ───────────────────────────

            // El monto debe ser mayor a cero
            if (monto <= 0)
            {
                MensajeError("El monto debe ser mayor a cero.");
                Pausa();
                return;
            }

            // No se permiten montos extremadamente grandes
            if (monto > MONTO_MAXIMO_OPERACION)
            {
                MensajeError($"El monto máximo por operación es $ {MONTO_MAXIMO_OPERACION:F2}.");
                Pausa();
                return;
            }

            // ── Paso 3: Mostrar resumen y pedir confirmación ───
            double nuevoSaldo = usuario.Saldo + monto; // Calcular cómo quedaría el saldo

            Console.WriteLine();
            Console.WriteLine("  ┌─────────────────────────────────┐");
            Console.WriteLine("  │       CONFIRMAR DEPÓSITO        │");
            Console.WriteLine("  │                                 │");
            Console.WriteLine("  │  Monto a depositar: $ {0,-10:F2}│", monto);
            Console.WriteLine("  │  Saldo actual:      $ {0,-10:F2}│", usuario.Saldo);
            Console.WriteLine("  │  Nuevo saldo:       $ {0,-10:F2}│", nuevoSaldo);
            Console.WriteLine("  └─────────────────────────────────┘");
            Console.WriteLine();

            // Preguntar S/N antes de hacer cualquier cambio
            if (!PedirConfirmacion("  ¿Desea confirmar el depósito? (S/N): "))
            {
                MensajeAdvertencia("Operación cancelada por el usuario.");
                Pausa();
                return;
            }

            // ── Paso 4: Guardar en la base de datos ────────────
            // RegistrarTransaccion hace DOS cosas en un solo paso (atómico):
            //   1. Inserta el registro en la tabla Transacciones
            //   2. Actualiza el saldo en la tabla Usuarios
            // Si algo falla, hace rollback y no cambia nada
            bool exito = _transaccionDAO.RegistrarTransaccion(
                idUsuario: usuario.IdUsuario,
                tipo: TipoOperacion.DEPOSITO,
                monto: monto,
                saldoAnterior: usuario.Saldo,   // Saldo antes del depósito
                saldoPosterior: nuevoSaldo,       // Saldo después del depósito
                descripcion: $"Depósito de $ {monto:F2}",
                exitosa: true
            );

            // ── Paso 5: Mostrar resultado ──────────────────────
            if (exito)
            {
                // Actualizar el saldo en memoria para mostrarlo en el comprobante
                usuario.Saldo = nuevoSaldo;
                ImprimirComprobante("DEPÓSITO", usuario, monto);
                MensajeExito("¡Depósito realizado exitosamente!");
            }
            else
            {
                // La BD rechazó la operación (error de conexión, etc.)
                MensajeError("No se pudo completar el depósito. Intente de nuevo.");
            }

            Pausa();
        }

        // ══════════════════════════════════════════════════════
        //   OPERACIÓN 3: RETIRAR EFECTIVO
        //
        //   Permite al usuario sacar dinero de su cuenta.
        //   Tiene más validaciones que el depósito:
        //   saldo suficiente, límite diario, múltiplos de $5.
        //   Flujo: mostrar info → pedir monto → validar → confirmar → guardar en BD → comprobante
        // ══════════════════════════════════════════════════════
        private void OperacionRetirar(Usuario usuario)
        {
            LimpiarPantalla();
            DibujarEncabezado("RETIRO DE EFECTIVO");

            // Calcular cuánto ya retiró hoy consultando la BD
            double retiradoHoy = ObtenerRetiroAcumuladoHoy(usuario.IdUsuario);
            double disponibleHoy = LIMITE_RETIRO_DIARIO - retiradoHoy;

            // Mostrar información importante antes de pedir el monto
            Console.WriteLine("  Saldo disponible        : $ {0:F2}", usuario.Saldo);
            Console.WriteLine("  Límite de retiro diario : $ {0:F2}", LIMITE_RETIRO_DIARIO);
            Console.WriteLine("  Disponible hoy          : $ {0:F2}", disponibleHoy);
            Console.WriteLine("  (Solo múltiplos de $ {0:F0})", MULTIPLO_RETIRO);
            Console.WriteLine();

            // ── Paso 1: Pedir el monto ─────────────────────────
            double monto = PedirMonto("  Ingrese el monto a retirar: $");

            if (monto == -1) // Usuario canceló
            {
                MensajeAdvertencia("Operación cancelada.");
                Pausa();
                return;
            }

            // ── Paso 2: Validaciones ───────────────────────────

            // Validación 1: monto debe ser positivo
            if (monto <= 0)
            {
                MensajeError("El monto debe ser mayor a cero.");
                Pausa();
                return;
            }

            // Validación 2: no superar el máximo por operación
            if (monto > MONTO_MAXIMO_OPERACION)
            {
                MensajeError($"El monto máximo por operación es $ {MONTO_MAXIMO_OPERACION:F2}.");
                Pausa();
                return;
            }

            // Validación 3: debe ser múltiplo de $5
            // El operador % devuelve el residuo: 25 % 5 = 0 (válido), 27 % 5 = 2 (inválido)
            if (monto % MULTIPLO_RETIRO != 0)
            {
                MensajeError($"Solo se permiten retiros en múltiplos de $ {MULTIPLO_RETIRO:F0} (ej: $5, $10, $20...).");
                Pausa();
                return;
            }

            // Validación 4: debe haber suficiente saldo en la cuenta
            if (monto > usuario.Saldo)
            {
                MensajeError("Saldo insuficiente.");
                Pausa();
                return;
            }

            // Validación 5: no superar el límite de retiro diario
            if (monto > disponibleHoy)
            {
                MensajeError($"Límite diario superado. Solo puede retirar $ {disponibleHoy:F2} más hoy.");
                Pausa();
                return;
            }

            // ── Paso 3: Mostrar resumen y pedir confirmación ───
            double nuevoSaldo = usuario.Saldo - monto; // Cómo quedaría el saldo

            Console.WriteLine();
            Console.WriteLine("  ┌─────────────────────────────────┐");
            Console.WriteLine("  │         CONFIRMAR RETIRO        │");
            Console.WriteLine("  │                                 │");
            Console.WriteLine("  │  Monto a retirar:   $ {0,-10:F2}│", monto);
            Console.WriteLine("  │  Saldo actual:      $ {0,-10:F2}│", usuario.Saldo);
            Console.WriteLine("  │  Saldo restante:    $ {0,-10:F2}│", nuevoSaldo);
            Console.WriteLine("  └─────────────────────────────────┘");
            Console.WriteLine();

            if (!PedirConfirmacion("  ¿Desea confirmar el retiro? (S/N): "))
            {
                MensajeAdvertencia("Operación cancelada por el usuario.");
                Pausa();
                return;
            }

            // ── Paso 4: Guardar en la base de datos ────────────
            // Igual que el depósito: atómico, guarda el movimiento y actualiza el saldo
            bool exito = _transaccionDAO.RegistrarTransaccion(
                idUsuario: usuario.IdUsuario,
                tipo: TipoOperacion.RETIRO,
                monto: monto,
                saldoAnterior: usuario.Saldo,  // Saldo antes del retiro
                saldoPosterior: nuevoSaldo,      // Saldo después del retiro
                descripcion: $"Retiro de $ {monto:F2}",
                exitosa: true
            );

            // ── Paso 5: Mostrar resultado ──────────────────────
            if (exito)
            {
                // Actualizar saldo en memoria para el comprobante
                usuario.Saldo = nuevoSaldo;
                ImprimirComprobante("RETIRO", usuario, monto);
                MensajeExito("¡Retiro exitoso! Retire su efectivo.");
            }
            else
            {
                MensajeError("No se pudo completar el retiro. Intente de nuevo.");
            }

            Pausa();
        }

        // ══════════════════════════════════════════════════════
        //   MÉTODO AUXILIAR: ObtenerRetiroAcumuladoHoy
        //
        //   Consulta la BD para saber cuánto ya retiró el usuario
        //   en el día de hoy. Se usa para validar el límite diario.
        // ══════════════════════════════════════════════════════
        private double ObtenerRetiroAcumuladoHoy(int idUsuario)
        {
            // Obtener todos los retiros del usuario
            var retiros = _transaccionDAO.ObtenerPorTipo(idUsuario, TipoOperacion.RETIRO);

            double totalHoy = 0;
            string hoy = DateTime.Now.ToString("yyyy-MM-dd"); // Formato de fecha de la BD

            // Sumar solo los retiros de hoy que fueron exitosos
            foreach (var t in retiros)
            {
                // t.Fecha viene en formato "yyyy-MM-dd HH:mm:ss" desde SQLite
                // StartsWith compara solo la parte de la fecha (los primeros 10 caracteres)
                if (t.Exitosa && t.Fecha.StartsWith(hoy))
                    totalHoy += t.Monto;
            }

            return totalHoy;
        }

        // ══════════════════════════════════════════════════════
        //   COMPROBANTE SIMULADO
        //   Se imprime al final de cada depósito o retiro exitoso.
        // ══════════════════════════════════════════════════════
        private static void ImprimirComprobante(string tipoOp, Usuario usuario, double monto)
        {
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════╗");
            Console.WriteLine("  ║           COMPROBANTE            ║");
            Console.WriteLine("  ╠══════════════════════════════════╣");
            Console.WriteLine("  ║  Cajero Automático               ║");
            // DateTime.Now.ToString formatea la fecha y hora actual
            Console.WriteLine("  ║  Fecha   : {0,-23}║", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            Console.WriteLine("  ║  Tarjeta : {0,-23}║", usuario.NumeroTarjeta);
            // Si el nombre tiene más de 22 letras, cortarlo para que quepa
            Console.WriteLine("  ║  Nombre  : {0,-23}║", usuario.NombreTitular.Length > 22
                ? usuario.NombreTitular.Substring(0, 22)
                : usuario.NombreTitular);
            Console.WriteLine("  ╠══════════════════════════════════╣");
            Console.WriteLine("  ║  Operación : {0,-21}║", tipoOp);
            Console.WriteLine("  ║  Monto     : $ {0,-19:F2}║", monto);
            Console.WriteLine("  ║  Saldo     : $ {0,-19:F2}║", usuario.Saldo); // Ya actualizado
            Console.WriteLine("  ╚══════════════════════════════════╝");
            Console.WriteLine();
        }

        // ══════════════════════════════════════════════════════
        //   MÉTODOS DE APOYO (internos, no los usan los compañeros)
        // ══════════════════════════════════════════════════════

        // Pide un número al usuario. Repite hasta que sea válido.
        // Devuelve -1 si el usuario escribe "0" o "C" para cancelar.
        private static double PedirMonto(string mensaje)
        {
            while (true) // Repetir hasta obtener un número válido
            {
                Console.Write(mensaje);
                string entrada = Console.ReadLine()?.Trim() ?? "";

                // Si el usuario escribe 0 o C, cancelar la operación
                if (entrada == "0" || entrada.ToUpper() == "C")
                    return -1;

                // TryParse intenta convertir el texto a número
                // Si falla (ej: escribió "abc"), devuelve false y pide de nuevo
                if (double.TryParse(entrada, out double monto))
                    return monto;

                MensajeError("Entrada inválida. Ingrese solo números (o '0' para cancelar).");
            }
        }

        // Pregunta S/N al usuario. Repite hasta obtener una respuesta válida.
        // Devuelve true si confirmó (S), false si canceló (N).
        private static bool PedirConfirmacion(string mensaje)
        {
            while (true)
            {
                Console.Write(mensaje);
                // ToUpper() convierte a mayúsculas para aceptar tanto "s" como "S"
                string respuesta = Console.ReadLine()?.Trim().ToUpper() ?? "";

                if (respuesta == "S") return true;
                if (respuesta == "N") return false;

                MensajeError("Ingrese S para confirmar o N para cancelar.");
            }
        }

        // ── Pantalla y mensajes ────────────────────────────────

        private static void LimpiarPantalla() => Console.Clear();

        // Dibuja el marco del menú principal con las 4 opciones
        private static void MostrarMenu(string nombre)
        {
            LimpiarPantalla();
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════╗");
            Console.WriteLine("  ║       CAJERO AUTOMÁTICO          ║");
            Console.WriteLine("  ╠══════════════════════════════════╣");
            Console.WriteLine("  ║  Bienvenido/a,                  ║");
            // Cortar el nombre si es muy largo para que quepa en el marco
            Console.WriteLine("  ║  {0,-32}║", nombre.Length > 30
                ? nombre.Substring(0, 30)
                : nombre);
            Console.WriteLine("  ╠══════════════════════════════════╣");
            Console.WriteLine("  ║  [1]  Consultar saldo            ║");
            Console.WriteLine("  ║  [2]  Depositar dinero           ║");
            Console.WriteLine("  ║  [3]  Retirar efectivo           ║");
            Console.WriteLine("  ║  [4]  Finalizar sesión           ║");
            Console.WriteLine("  ╚══════════════════════════════════╝");
            Console.WriteLine();
            Console.Write("  Seleccione una opción: ");
        }

        // Dibuja el título de cada sección (consulta, depósito, retiro)
        private static void DibujarEncabezado(string titulo)
        {
            Console.WriteLine("  ╔══════════════════════════════════╗");
            Console.WriteLine("  ║  {0,-32}║", titulo);
            Console.WriteLine("  ╚══════════════════════════════════╝");
        }

        // Pantalla de cierre de sesión
        private static void MostrarSesionFinalizada()
        {
            LimpiarPantalla();
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════╗");
            Console.WriteLine("  ║       SESIÓN FINALIZADA          ║");
            Console.WriteLine("  ║                                  ║");
            Console.WriteLine("  ║  Gracias por usar nuestro        ║");
            Console.WriteLine("  ║  cajero automático.              ║");
            Console.WriteLine("  ║                                  ║");
            Console.WriteLine("  ║  Por favor retire su tarjeta.    ║");
            Console.WriteLine("  ╚══════════════════════════════════╝");
            Console.WriteLine();
        }

        // Mensaje verde: operación completada correctamente
        private static void MensajeExito(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ✔ " + msg);
            Console.ResetColor(); // Restaurar color original de la consola
        }

        // Mensaje rojo: algo salió mal o no se puede hacer
        private static void MensajeError(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  ✘ " + msg);
            Console.ResetColor();
        }

        // Mensaje amarillo: advertencia o cancelación voluntaria
        private static void MensajeAdvertencia(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  ⚠ " + msg);
            Console.ResetColor();
        }

        // ══════════════════════════════════════════════════════
        //   OPERACIÓN 4: TRANSFERIR DINERO
        //
        //   Permite al usuario transferir dinero a otra cuenta.
        //   Flujo: pedir tarjeta destino → validar → pedir monto → validar → confirmar → guardar en BD
        // ══════════════════════════════════════════════════════
        private void OperacionTransferir(Usuario usuario)
        {
            LimpiarPantalla();
            DibujarEncabezado("TRANSFERENCIA DE DINERO");

            // Mostrar saldo actual antes de pedir datos
            Console.WriteLine("  Saldo disponible: $ {0:F2}", usuario.Saldo);
            Console.WriteLine();

            // ── Paso 1: Pedir número de tarjeta destino ─────────────────────────
            string tarjetaDestino = PedirTarjetaDestino();

            if (string.IsNullOrEmpty(tarjetaDestino))
            {
                MensajeAdvertencia("Operación cancelada.");
                Pausa();
                return;
            }

            // Verificar que la tarjeta destino exista y no sea la misma que la de origen
            if (tarjetaDestino == usuario.NumeroTarjeta)
            {
                MensajeError("No puede transferir dinero a su propia cuenta.");
                Pausa();
                return;
            }

            Usuario? usuarioDestino = _usuarioDAO.ObtenerPorTarjeta(tarjetaDestino);
            if (usuarioDestino == null)
            {
                MensajeError("La tarjeta destino no existe en el sistema.");
                Pausa();
                return;
            }

            if (usuarioDestino.Bloqueado)
            {
                MensajeError("La cuenta destino está bloqueada.");
                Pausa();
                return;
            }

            // ── Paso 2: Pedir el monto ─────────────────────────
            double monto = PedirMonto("  Ingrese el monto a transferir: $");

            if (monto == -1) // Usuario canceló
            {
                MensajeAdvertencia("Operación cancelada.");
                Pausa();
                return;
            }

            // ── Paso 3: Validaciones ───────────────────────────

            // El monto debe ser mayor a cero
            if (monto <= 0)
            {
                MensajeError("El monto debe ser mayor a cero.");
                Pausa();
                return;
            }

            // No se permiten montos extremadamente grandes
            if (monto > MONTO_MAXIMO_OPERACION)
            {
                MensajeError($"El monto máximo por operación es $ {MONTO_MAXIMO_OPERACION:F2}.");
                Pausa();
                return;
            }

            // Debe haber suficiente saldo en la cuenta
            if (monto > usuario.Saldo)
            {
                MensajeError("Saldo insuficiente.");
                Pausa();
                return;
            }

            // ── Paso 4: Mostrar resumen y pedir confirmación ───
            double nuevoSaldoOrigen = usuario.Saldo - monto;
            double nuevoSaldoDestino = usuarioDestino.Saldo + monto;

            Console.WriteLine();
            Console.WriteLine("  ┌─────────────────────────────────┐");
            Console.WriteLine("  │    CONFIRMAR TRANSFERENCIA      │");
            Console.WriteLine("  │                                 │");
            Console.WriteLine("  │  Monto a transferir: $ {0,-10:F2}│", monto);
            Console.WriteLine("  │  Cuenta origen:      {0,-16}│", usuario.NumeroTarjeta);
            Console.WriteLine("  │  Saldo actual:       $ {0,-10:F2}│", usuario.Saldo);
            Console.WriteLine("  │  Saldo restante:     $ {0,-10:F2}│", nuevoSaldoOrigen);
            Console.WriteLine("  │                                 │");
            Console.WriteLine("  │  Cuenta destino:     {0,-16}│", usuarioDestino.NumeroTarjeta);
            Console.WriteLine("  │  Titular:            {0,-16}│", usuarioDestino.NombreTitular.Length > 16 ? usuarioDestino.NombreTitular.Substring(0, 16) : usuarioDestino.NombreTitular);
            Console.WriteLine("  │  Saldo destino:      $ {0,-10:F2}│", usuarioDestino.Saldo);
            Console.WriteLine("  │  Nuevo saldo destino:$ {0,-10:F2}│", nuevoSaldoDestino);
            Console.WriteLine("  └─────────────────────────────────┘");
            Console.WriteLine();

            // Preguntar S/N antes de hacer cualquier cambio
            if (!PedirConfirmacion("  ¿Desea confirmar la transferencia? (S/N): "))
            {
                MensajeAdvertencia("Operación cancelada por el usuario.");
                Pausa();
                return;
            }

            // ── Paso 5: Guardar en la base de datos ────────────
            var transferenciaDAO = new TransferenciaDAO();
            bool exito = transferenciaDAO.RegistrarTransferencia(
                idUsuarioOrigen: usuario.IdUsuario,
                idUsuarioDestino: usuarioDestino.IdUsuario,
                monto: monto,
                saldoOrigenAntes: usuario.Saldo,
                saldoOrigenDespues: nuevoSaldoOrigen,
                saldoDestinoAntes: usuarioDestino.Saldo,
                saldoDestinoDespues: nuevoSaldoDestino,
                descripcion: $"Transferencia a {usuarioDestino.NumeroTarjeta}"
            );

            // ── Paso 6: Mostrar resultado ──────────────────────
            if (exito)
            {
                // Actualizar el saldo en memoria para mostrarlo en el comprobante
                usuario.Saldo = nuevoSaldoOrigen;
                ImprimirComprobanteTransferencia(usuario, usuarioDestino, monto);
                MensajeExito("¡Transferencia realizada exitosamente!");
            }
            else
            {
                // La BD rechazó la operación (error de conexión, etc.)
                MensajeError("No se pudo completar la transferencia. Intente de nuevo.");
            }

            Pausa();
        }

        // ══════════════════════════════════════════════════════
        //   MÉTODO AUXILIAR: PedirTarjetaDestino
        //   Pide el número de tarjeta destino al usuario
        // ══════════════════════════════════════════════════════
        private static string PedirTarjetaDestino()
        {
            while (true)
            {
                Console.Write("  Ingrese el número de tarjeta destino (o '0' para cancelar): ");
                string entrada = Console.ReadLine()?.Trim() ?? "";

                // Si el usuario escribe 0, cancelar la operación
                if (entrada == "0")
                    return string.Empty;

                // Validar que solo contenga dígitos
                bool soloDigitos = true;
                foreach (char c in entrada)
                    if (!char.IsDigit(c)) { soloDigitos = false; break; }

                if (!soloDigitos)
                {
                    MensajeError("El número de tarjeta solo debe contener dígitos.");
                    continue;
                }

                // Validar longitud (16 dígitos típicamente)
                if (entrada.Length != 16)
                {
                    MensajeError("El número de tarjeta debe tener 16 dígitos.");
                    continue;
                }

                return entrada;
            }
        }

        // ══════════════════════════════════════════════════════
        //   COMPROBANTE DE TRANSFERENCIA
        //   Se imprime al final de cada transferencia exitosa.
        // ══════════════════════════════════════════════════════
        private static void ImprimirComprobanteTransferencia(Usuario usuarioOrigen, Usuario usuarioDestino, double monto)
        {
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════╗");
            Console.WriteLine("  ║         COMPROBANTE              ║");
            Console.WriteLine("  ╠══════════════════════════════════╣");
            Console.WriteLine("  ║  Cajero Automático               ║");
            Console.WriteLine("  ║  Fecha   : {0,-23}║", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            Console.WriteLine("  ║  Tarjeta : {0,-23}║", usuarioOrigen.NumeroTarjeta);
            Console.WriteLine("  ╠══════════════════════════════════╣");
            Console.WriteLine("  ║  Operación : TRANSFERENCIA       ║");
            Console.WriteLine("  ║  Monto     : $ {0,-19:F2}║", monto);
            Console.WriteLine("  ║  Saldo     : $ {0,-19:F2}║", usuarioOrigen.Saldo);
            Console.WriteLine("  ║                                 ║");
            Console.WriteLine("  ║  Destino   : {0,-23}║", usuarioDestino.NumeroTarjeta);
            Console.WriteLine("  ║  Titular   : {0,-23}║", usuarioDestino.NombreTitular.Length > 23 ? usuarioDestino.NombreTitular.Substring(0, 23) : usuarioDestino.NombreTitular);
            Console.WriteLine("  ╚══════════════════════════════════╝");
            Console.WriteLine();
        }

        // ══════════════════════════════════════════════════════
        //   MÉTODO DE APOYO: Pausa
        //   Pausa el programa hasta que el usuario presione ENTER
        // ══════════════════════════════════════════════════════
        private static void Pausa()
        {
            Console.WriteLine();
            Console.WriteLine("  Presione ENTER para continuar...");
            Console.ReadLine();
        }
    }
}
