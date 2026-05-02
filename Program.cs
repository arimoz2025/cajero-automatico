using System;
using CajeroAutomatico.Base_de_Datos;
using CajeroAutomatico.UI;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.Title = "BancoSim ATM — Universidad Don Bosco";

ConexionDB.InicializarBaseDeDatos();

Menu.Iniciar();