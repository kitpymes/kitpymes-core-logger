// -----------------------------------------------------------------------
// <copyright file="KitpymesSerilog.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger.Serilog;

using System;
using System.Collections.Generic;
using global::Serilog;
using Microsoft.Extensions.Logging;

/// <summary>
/// Crea el logger de Serilog de Kitpymes: sinks de la configuración, título, enriquecedores y datos sensibles ocultos.
/// </summary>
public static class KitpymesSerilog
{
    /// <summary>
    /// Niveles mínimos por categoría que se aplican si la configuración no los cambia.
    /// </summary>
    /// <remarks><c>Microsoft</c> y <c>System</c> en <c>Warning</c>: sin ellos, cada petición de ASP.NET Core escribe varios Info.</remarks>
    public static readonly IReadOnlyDictionary<string, string> DefaultMinimumLevelOverride = new Dictionary<string, string>
    {
        ["Microsoft"] = "Warning",
        ["System"] = "Warning",
    };

    /// <summary>
    /// Crea un logger de Serilog con la configuración.
    /// </summary>
    /// <param name="settings">Configuración de Serilog.</param>
    /// <returns>El logger; quien lo crea lo libera (<c>Dispose</c>) para vaciar los eventos pendientes.</returns>
    /// <remarks>
    /// <para>Todo evento pasa, antes de llegar a los sinks, por la ocultación de datos sensibles: propiedades y
    /// miembros con nombre de credencial o marcados con <c>[Sensitive]</c>, valores en JSON, <c>clave=valor</c>,
    /// <c>Bearer</c> y contraseñas en URL, también dentro del texto del mensaje. No se ocultan los mensajes de las excepciones.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Si <paramref name="settings"/> es nulo.</exception>
    public static global::Serilog.Core.Logger CreateLogger(SerilogSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var sinks = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .AddConsole(settings.Console)
            .AddFile(settings.File)
            .AddEmail(settings.Email)
            .AddSqlserver(settings.SqlServer)
            .CreateLogger();

        var configuration = new LoggerConfiguration().MinimumLevel.Is(settings.MinimumLevel.ToMinimumLevel());

        foreach (var level in DefaultMinimumLevelOverride)
        {
            if (settings.MinimumLevelOverride?.ContainsKey(level.Key) != true)
            {
                configuration.MinimumLevel.Override(level.Key, level.Value.ToMinimumLevel());
            }
        }

        foreach (var level in settings.MinimumLevelOverride ?? new Dictionary<string, string>())
        {
            configuration.MinimumLevel.Override(level.Key, level.Value.ToMinimumLevel());
        }

        return configuration
            .Destructure.With(new SensitiveDestructuringPolicy())
            .Enrich.With(new SourceContextTitleEnricher())
            .AddDefaultSettings(string.Empty)
            .WriteTo.Sink(new SensitiveDataSink(sinks))
            .CreateLogger();
    }

    /// <summary>
    /// Crea una fábrica de <c>ILogger&lt;T&gt;</c> con Serilog, para el arranque de la aplicación (antes de tener el contenedor).
    /// </summary>
    /// <param name="settings">Configuración de Serilog.</param>
    /// <returns>La fábrica; al liberarla se vacían los eventos pendientes.</returns>
    public static ILoggerFactory CreateLoggerFactory(SerilogSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Con el mismo registro que AddKitpymesSerilog: al liberar la fábrica se libera su contenedor, y con él el logger de Serilog.
        return LoggerFactory.Create(builder => builder.AddKitpymesSerilog(settings));
    }

    /// <summary>
    /// Crea una fábrica de <c>ILogger&lt;T&gt;</c> con Serilog, para el arranque de la aplicación (antes de tener el contenedor).
    /// </summary>
    /// <param name="options">Opciones de Serilog.</param>
    /// <returns>La fábrica; al liberarla se vacían los eventos pendientes.</returns>
    public static ILoggerFactory CreateLoggerFactory(Action<SerilogOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var serilogOptions = new SerilogOptions();
        options(serilogOptions);

        return CreateLoggerFactory(serilogOptions.SerilogSettings);
    }
}
