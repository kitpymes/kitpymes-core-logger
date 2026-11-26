// -----------------------------------------------------------------------
// <copyright file="ProcessEnricher.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger.Serilog;

using global::Serilog.Core;
using global::Serilog.Events;

/*
    Clase para enriquecer el logeo de errores ProcessEnricher
    Contiene el id y el nombre del proceso para mostrar en el logeo de errores
*/

/// <summary>
/// Clase para enriquecer el logeo de errores <c>ProcessEnricher</c>.
/// Contiene el id y el nombre del proceso para mostrar en el logeo de errores.
/// </summary>
public class ProcessEnricher : ILogEventEnricher
{
    /// <summary>
    /// Nombre de la propiedad donde se va a indexar el id y nombre del proceso.
    /// </summary>
    public const string ProcessPropertyName = "Process";

    private readonly LogEventProperty property = new LogEventProperty(ProcessPropertyName, new ScalarValue(CurrentProcess()));

    /// <summary>
    /// Enriquecer el evento de registro.
    /// </summary>
    /// <param name="logEvent">El evento de registro para enriquecer.</param>
    /// <param name="propertyFactory">Fábrica para crear nuevas propiedades para agregar al evento.</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        => logEvent?.AddPropertyIfAbsent(this.property);

    private static string CurrentProcess()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();

        return $"{process.Id.ToString(System.Globalization.CultureInfo.CurrentCulture)} - {process.ProcessName}";
    }
}