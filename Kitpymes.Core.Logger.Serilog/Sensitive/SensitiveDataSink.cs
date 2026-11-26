// -----------------------------------------------------------------------
// <copyright file="SensitiveDataSink.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger.Serilog;

using System;
using global::Serilog.Core;
using global::Serilog.Events;
using global::Serilog.Parsing;

/// <summary>
/// Sink que va delante de todos los demás: les pasa cada evento con los datos sensibles ocultos.
/// </summary>
/// <remarks>
/// Falla cerrado: si ocultar los datos lanza, no se escribe el evento original, sino uno que dice que se descartó.
/// </remarks>
/// <param name="inner">Sinks reales (un logger de Serilog con los sinks configurados).</param>
/// <param name="mask">Función que oculta los datos; por defecto <see cref="SensitiveDataMasker.Mask(LogEvent)"/> (los tests la sustituyen para probar el fallo).</param>
internal sealed class SensitiveDataSink(ILogEventSink inner, Func<LogEvent, LogEvent>? mask = null) : ILogEventSink, IDisposable
{
    private static readonly MessageTemplate Discarded = new MessageTemplateParser()
        .Parse("Evento descartado: no se pudieron ocultar los datos sensibles ({MaskingError}).");

    /// <summary>
    /// Escribe el evento sin datos sensibles.
    /// </summary>
    /// <param name="logEvent">Evento.</param>
    public void Emit(LogEvent logEvent)
    {
        LogEvent masked;

#pragma warning disable CA1031 // Falla cerrado: cualquier error al ocultar descarta el evento original.
        try
        {
            masked = mask is null ? SensitiveDataMasker.Mask(logEvent) : mask(logEvent);
        }
        catch (Exception exception)
        {
            masked = new LogEvent(
                logEvent.Timestamp,
                logEvent.Level,
                null,
                Discarded,
                new[] { new LogEventProperty("MaskingError", new ScalarValue(exception.GetType().Name)) });
        }
#pragma warning restore CA1031

        inner.Emit(masked);
    }

    /// <summary>
    /// Libera los sinks reales.
    /// </summary>
    public void Dispose() => (inner as IDisposable)?.Dispose();
}
