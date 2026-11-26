// -----------------------------------------------------------------------
// <copyright file="SerilogSettings.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger.Serilog
{
    using System.Collections.Generic;
    using Kitpymes.Core.Logger.Abstractions;

    /*
       Clase de configuración SerilogSettings
       Contiene las propiedades para configurar los proveedores de error de Serilog
    */

    /// <summary>
    /// Clase de configuración <c>SerilogSettings</c>.
    /// Contiene las propiedades para configurar los proveedores de error de Serilog.
    /// </summary>
    /// <remarks>
    /// <para>En esta clase se pueden agregar todos los proveedores de errores de Serilog.</para>
    /// </remarks>
    public class SerilogSettings
    {
        /// <summary>
        /// Nivel mínimo de error habilidato por defecto.
        /// </summary>
        public const LoggerLevel DefaultMinimumLevel = LoggerLevel.Info;

        private string minimumLevel = DefaultMinimumLevel.ToString();

        /// <summary>
        /// Obtiene o establece el nivel mínimo de error.
        /// </summary>
        public string? MinimumLevel
        {
            get => this.minimumLevel;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    this.minimumLevel = value;
                }
            }
        }

        /// <summary>
        /// Obtiene o establece el nivel mínimo por categoría (prefijo del nombre), p. ej. <c>"Microsoft.AspNetCore": "Warning"</c>.
        /// </summary>
        /// <remarks>
        /// <para>Actúa sobre la categoría de <c>ILogger&lt;T&gt;</c>, que es el nombre completo del tipo; el <c>ILoggerService</c> antiguo no tiene categoría.</para>
        /// <para>Si no se indican, <c>Microsoft</c> y <c>System</c> quedan en <c>Warning</c> (<see cref="KitpymesSerilog.DefaultMinimumLevelOverride"/>).</para>
        /// </remarks>
#pragma warning disable CA2227 // Se rellena desde la configuración (appsettings).
        public Dictionary<string, string>? MinimumLevelOverride { get; set; }
#pragma warning restore CA2227

        /// <summary>
        /// Obtiene o establece la configuración del logeo de errores de consola.
        /// </summary>
        public SerilogConsoleSettings Console { get; set; } = new SerilogConsoleSettings();

        /// <summary>
        /// Obtiene o establece la configuración del logeo de errores de archivos.
        /// </summary>
        public SerilogFileSettings File { get; set; } = new SerilogFileSettings();

        /// <summary>
        /// Obtiene o establece la configuración del logeo de errores de email.
        /// </summary>
        public SerilogEmailSettings Email { get; set; } = new SerilogEmailSettings();

        /// <summary>
        /// Obtiene o establece la configuración del logeo de errores de SqlServer.
        /// </summary>
        public SerilogSqlServerSettings SqlServer { get; set; } = new SerilogSqlServerSettings();
    }
}
