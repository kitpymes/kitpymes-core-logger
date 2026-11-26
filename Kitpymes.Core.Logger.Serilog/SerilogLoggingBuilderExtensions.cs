// -----------------------------------------------------------------------
// <copyright file="SerilogLoggingBuilderExtensions.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger.Serilog;

using System;
using System.Linq;
using global::Serilog.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Registra Serilog como proveedor de Microsoft.Extensions.Logging: el código usa <c>ILogger&lt;T&gt;</c>.
/// </summary>
public static class SerilogLoggingBuilderExtensions
{
    /// <summary>
    /// Registra Serilog, con la configuración de Kitpymes, como proveedor de <c>ILogger&lt;T&gt;</c>.
    /// </summary>
    /// <param name="builder">Configuración de logging (<c>builder.Logging</c>).</param>
    /// <param name="settings">Configuración de Serilog.</param>
    /// <returns>El mismo <paramref name="builder"/>.</returns>
    /// <remarks>
    /// <para>El logger de Serilog se crea al pedir el primer <c>ILogger</c> y lo libera el contenedor (vacía los eventos pendientes).</para>
    /// <para>Serilog decide el nivel (<c>MinimumLevel</c> y <c>MinimumLevelOverride</c>): las reglas <c>Logging:LogLevel</c> no lo filtran.</para>
    /// <para>Se registra una sola vez: una segunda llamada (también desde <c>AddKitpymesLogging</c>) no hace nada.</para>
    /// <para>No quita los demás proveedores: si la aplicación tiene el de consola de Microsoft y aquí también se activa
    /// la consola, cada evento sale dos veces. Para dejar sólo Serilog, <c>builder.ClearProviders()</c> antes.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Si <paramref name="builder"/> o <paramref name="settings"/> son nulos.</exception>
    public static ILoggingBuilder AddKitpymesSerilog(this ILoggingBuilder builder, SerilogSettings settings)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(settings);

        // Una sola vez: llamarlo dos veces (o con AddKitpymesLogging) escribiría cada evento dos veces.
        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(KitpymesSerilogRegistration)))
        {
            return builder;
        }

        builder.Services.AddSingleton<KitpymesSerilogRegistration>();
        builder.Services.AddSingleton<ILoggerProvider>(_ =>
            new SensitiveScopeLoggerProvider(new SerilogLoggerProvider(KitpymesSerilog.CreateLogger(settings), dispose: true)));

        // Como AddSerilog: el nivel lo decide Serilog, no las reglas de Microsoft.Extensions.Logging.
        builder.AddFilter<SensitiveScopeLoggerProvider>(null, LogLevel.Trace);

        return builder;
    }

    /// <summary>
    /// Registra Serilog, con la configuración de Kitpymes, como proveedor de <c>ILogger&lt;T&gt;</c>.
    /// </summary>
    /// <param name="builder">Configuración de logging (<c>builder.Logging</c>).</param>
    /// <param name="options">Opciones de Serilog.</param>
    /// <returns>El mismo <paramref name="builder"/>.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="builder"/> u <paramref name="options"/> son nulos.</exception>
    public static ILoggingBuilder AddKitpymesSerilog(this ILoggingBuilder builder, Action<SerilogOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var serilogOptions = new SerilogOptions();
        options(serilogOptions);

        return builder.AddKitpymesSerilog(serilogOptions.SerilogSettings);
    }

    /// <summary>
    /// Marca de que Serilog ya está registrado en el contenedor.
    /// </summary>
    private sealed class KitpymesSerilogRegistration
    {
    }
}
