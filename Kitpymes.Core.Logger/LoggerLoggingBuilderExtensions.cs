// -----------------------------------------------------------------------
// <copyright file="LoggerLoggingBuilderExtensions.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger;

using System;
using Kitpymes.Core.Logger.Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Registra el logging de Kitpymes a partir de <see cref="LoggerSettings"/>: el código usa <c>ILogger&lt;T&gt;</c>.
/// </summary>
public static class LoggerLoggingBuilderExtensions
{
    /// <summary>
    /// Registra el logging con la sección <c>LoggerSettings</c> de la configuración.
    /// </summary>
    /// <param name="builder">Configuración de logging (<c>builder.Logging</c>).</param>
    /// <param name="configuration">Configuración de la aplicación (la raíz, no la sección).</param>
    /// <returns>El mismo <paramref name="builder"/>.</returns>
    /// <remarks>Si <c>Enabled</c> o <c>IsSerilogEnabled</c> no están a <c>true</c>, no registra nada.</remarks>
    /// <exception cref="ArgumentNullException">Si <paramref name="builder"/> o <paramref name="configuration"/> son nulos.</exception>
    public static ILoggingBuilder AddKitpymesLogging(this ILoggingBuilder builder, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return builder.AddKitpymesLogging(configuration.GetSection(nameof(LoggerSettings)).Get<LoggerSettings>());
    }

    /// <summary>
    /// Registra el logging con opciones.
    /// </summary>
    /// <param name="builder">Configuración de logging (<c>builder.Logging</c>).</param>
    /// <param name="options">Opciones; <c>UseSerilog</c> lo activa.</param>
    /// <returns>El mismo <paramref name="builder"/>.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="builder"/> u <paramref name="options"/> son nulos.</exception>
    public static ILoggingBuilder AddKitpymesLogging(this ILoggingBuilder builder, Action<LoggerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var loggerOptions = new LoggerOptions();
        options(loggerOptions);

        return builder.AddKitpymesLogging(loggerOptions.LoggerSettings);
    }

    /// <summary>
    /// Registra el logging con la configuración dada.
    /// </summary>
    /// <param name="builder">Configuración de logging (<c>builder.Logging</c>).</param>
    /// <param name="settings">Configuración; si es nula o no está activada, no registra nada.</param>
    /// <returns>El mismo <paramref name="builder"/>.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="builder"/> es nulo.</exception>
    public static ILoggingBuilder AddKitpymesLogging(this ILoggingBuilder builder, LoggerSettings? settings)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (settings?.Enabled == true && settings.IsSerilogEnabled == true)
        {
            builder.AddKitpymesSerilog(settings.SerilogSettings ?? new SerilogSettings());
        }

        return builder;
    }
}
