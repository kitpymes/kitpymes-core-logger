// -----------------------------------------------------------------------
// <copyright file="LoggerSettings.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger;

using Kitpymes.Core.Logger.Serilog;

/// <summary>
/// Clase de configuración <c>LoggerSettings</c>.
/// Contiene las propiedades para configurar los proveedores de errores.
/// </summary>
/// <remarks>
/// <para>En esta clase se pueden agregar todos los proveedores de errores.</para>
/// </remarks>
public class LoggerSettings
{
    private bool enabled;
    private bool isSerilogEnabled;

    /// <summary>
    /// Obtiene o establece si el logeo de errores esta habiliato.
    /// </summary>
    public bool? Enabled
    {
        get => this.enabled;
        set
        {
            if (value.HasValue)
            {
                this.enabled = value.Value;
            }
        }
    }

    /// <summary>
    /// Obtiene o establece si el logeo de errores de Serilog esta habiliato.
    /// </summary>
    public bool? IsSerilogEnabled
    {
        get
        {
            if (this.isSerilogEnabled)
            {
                SerilogSettings ??= new ();
            }

            return this.isSerilogEnabled;
        }

        set
        {
            if (value.HasValue)
            {
                this.isSerilogEnabled = value.Value;
            }
        }
    }

    /// <summary>
    /// Obtiene o establece la configuración del logeo de errores de Serilog.
    /// </summary>
    public SerilogSettings? SerilogSettings { get; set; }
}
