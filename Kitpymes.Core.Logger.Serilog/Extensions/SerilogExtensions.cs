// -----------------------------------------------------------------------
// <copyright file="SerilogExtensions.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger.Serilog;

using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Net;
using global::Serilog;
using global::Serilog.Configuration;
using global::Serilog.Formatting.Display;
using global::Serilog.Sinks.MSSqlServer;
using MailKit.Security;
using Seri = global::Serilog;

/*
    Clase de extensión SerilogExtensions
    Contiene las extensiones del logeo de errores
*/

/// <summary>
/// Clase de extensión <c>SerilogExtensions</c>.
/// Contiene las extensiones del logeo de errores.
/// </summary>
/// <remarks>
/// <para>En esta clase se pueden agregar todas las extensiones para el logeo de errores.</para>
/// </remarks>
public static class SerilogExtensions
{
    /// <summary>
    /// Agrega las propiedades por defecto del log.
    /// </summary>
    /// <param name="loggerConfiguration">Objeto de configuración para crear instancias Serilog.ILogger.</param>
    /// <param name="title">El título del log.</param>
    /// <returns>La clase LoggerConfiguration.</returns>
    public static LoggerConfiguration AddDefaultSettings(this LoggerConfiguration loggerConfiguration, string title)
    {
        return Guard.NotNull(loggerConfiguration)
            .Enrich.WithTitle(title)
            .Enrich.WithMachineName()
            .Enrich.WithProcess()
            .Enrich.WithThread()
            .Enrich.FromLogContext();
    }

    /// <summary>
    /// Habilita el log de consola.
    /// </summary>
    /// <param name="loggerConfiguration">Objeto de configuración para crear instancias Serilog.ILogger.</param>
    /// <param name="settings">Configuración del log de consola.</param>
    /// <returns>La clase LoggerConfiguration.</returns>
    public static LoggerConfiguration AddConsole(this LoggerConfiguration loggerConfiguration, SerilogConsoleSettings? settings)
    {
        if (settings?.Enabled == true)
        {
            Guard.NotNull(loggerConfiguration).WriteTo.Console(
                theme: Seri.Sinks.SystemConsole.Themes.SystemConsoleTheme.Colored,
                outputTemplate: Guard.NotNullOrWhiteSpace(settings.OutputTemplate),
                restrictedToMinimumLevel: settings.MinimumLevel.ToMinimumLevel());
        }

        return loggerConfiguration;
    }

    /// <summary>
    /// Habilita el log de archivos.
    /// </summary>
    /// <param name="loggerConfiguration">Objeto de configuración para crear instancias Serilog.ILogger.</param>
    /// <param name="settings">Configuración del log de archivos.</param>
    /// <returns>La clase LoggerConfiguration.</returns>
    public static LoggerConfiguration AddFile(this LoggerConfiguration loggerConfiguration, SerilogFileSettings? settings)
    {
        if (settings?.Enabled == true)
        {
            Guard.NotNull(loggerConfiguration).WriteTo.Async(x => x.File(
                formatter: new Seri.Formatting.Compact.CompactJsonFormatter(),
                path: Guard.NotNullOrWhiteSpace(settings.FilePath),
                restrictedToMinimumLevel: settings.MinimumLevel.ToMinimumLevel(),
                rollingInterval: settings.Interval.ToRollingInterval(),
                shared: true));
        }

        return loggerConfiguration;
    }

    /// <summary>
    /// Habilita el log de envio de email.
    /// </summary>
    /// <param name="loggerConfiguration">Objeto de configuración para crear instancias Serilog.ILogger.</param>
    /// <param name="settings">Configuración del log de envio de email.</param>
    /// <returns>La clase LoggerConfiguration.</returns>
    public static LoggerConfiguration AddEmail(this LoggerConfiguration loggerConfiguration, SerilogEmailSettings? settings)
    {
        if (settings?.Enabled == true)
        {
            Guard.NotNullOrWhiteSpace(settings.UserName);
            Guard.NotNullOrWhiteSpace(settings.Password);
            Guard.NotNullOrWhiteSpace(settings.Server);
            Guard.NotNullOrWhiteSpace(settings.From);
            Guard.NotNullOrWhiteSpace(settings.To);

            // Serilog.Sinks.Email 4.x: mismo comportamiento que la 2.4.0 que se usaba hasta Logger 2.0.0.
            // EnableSsl equivale al useSsl de MailKit (true: SSL al conectar; false: STARTTLS si el servidor lo admite),
            // y los correos se agrupan igual: hasta 100 eventos o 30 segundos, sin enviar el primero al momento.
            var options = new Seri.Sinks.Email.EmailSinkOptions
            {
                From = settings.From,
                To = settings.To.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                Host = settings.Server,
                Port = settings.Port!.Value,
                ConnectionSecurity = settings.EnableSsl!.Value ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable,
                Credentials = new NetworkCredential(settings.UserName, settings.Password),
                Body = new MessageTemplateTextFormatter(settings.OutputTemplate ?? SerilogEmailSettings.DefaultOutputTemplate),
            };

            if (!string.IsNullOrEmpty(settings.Subject))
            {
                options.Subject = new MessageTemplateTextFormatter(settings.Subject);
            }

            Guard.NotNull(loggerConfiguration).WriteTo.Email(
                options,
                new BatchingOptions
                {
                    BatchSizeLimit = 100,
                    BufferingTimeLimit = TimeSpan.FromSeconds(30),
                    EagerlyEmitFirstEvent = false,
                    QueueLimit = 10000,
                },
                restrictedToMinimumLevel: settings.MinimumLevel.ToMinimumLevel());
        }

        return loggerConfiguration;
    }

    /// <summary>
    /// Habilita el log de archivos.
    /// </summary>
    /// <param name="loggerConfiguration">Objeto de configuración para crear instancias Serilog.ILogger.</param>
    /// <param name="settings">Configuración del log de archivos.</param>
    /// <returns>La clase LoggerConfiguration.</returns>
    public static LoggerConfiguration AddSqlserver(this LoggerConfiguration loggerConfiguration, SerilogSqlServerSettings? settings)
    {
        if (settings?.Enabled == true)
        {
            Guard.NotNullOrWhiteSpace(settings.ConnectionString);

            var columnOptions = new ColumnOptions
            {
                AdditionalColumns = new Collection<SqlColumn>
                {
                    new ()
                    {
                        ColumnName = "MachineName",
                        DataType = SqlDbType.VarChar,
                        DataLength = 100,
                    },
                },
            };

            columnOptions.Store.Remove(StandardColumn.Properties);
            columnOptions.Store.Remove(StandardColumn.MessageTemplate);
            columnOptions.Store.Add(StandardColumn.LogEvent);
            columnOptions.LogEvent.DataLength = 2048;
            columnOptions.PrimaryKey = columnOptions.TimeStamp;
            columnOptions.TimeStamp.NonClusteredIndex = true;
            columnOptions.TimeStamp.AllowNull = true;

            Guard.NotNull(loggerConfiguration).WriteTo.MSSqlServer(
                sinkOptions: new MSSqlServerSinkOptions
                {
                    SchemaName = settings.SchemaName,
                    TableName = settings.TableName,
                    AutoCreateSqlTable = settings.AutoCreateDB == true,
                    BatchPostingLimit = 100,
                    BatchPeriod = new TimeSpan(0, 0, 10),
                },
                connectionString: settings.ConnectionString,
                columnOptions: columnOptions,
                restrictedToMinimumLevel: settings.MinimumLevel.ToMinimumLevel());
        }

        return loggerConfiguration;
    }

    /// <summary>
    /// Convierte el nivel del log génerico al nivel de log de Serilog.
    /// </summary>
    /// <param name="loggerLevel">El nivel mínimo habilitado para el log de errores.</param>
    /// <returns>El nivel mínimo de serilog LogEventLevel.</returns>
    public static Seri.Events.LogEventLevel ToMinimumLevel(this Abstractions.LoggerLevel loggerLevel)
    => loggerLevel.ToString().ToMinimumLevel();

    /// <summary>
    /// Convierte el nivel del log génerico al nivel de log de Serilog.
    /// </summary>
    /// <param name="loggerLevel">El nivel mínimo habilitado para el log de errores.</param>
    /// <param name="defaulLevel">Nivel por defecto a devolver.</param>
    /// <returns>El nivel mínimo de serilog LogEventLevel.</returns>
    /// <remarks>Sin distinguir mayúsculas: Trace o Verbose, Debug, Info o Information, Warn o Warning, Error y Fatal.</remarks>
    public static Seri.Events.LogEventLevel ToMinimumLevel(this string? loggerLevel, Seri.Events.LogEventLevel defaulLevel = Seri.Events.LogEventLevel.Information)
    => loggerLevel?.Trim().ToUpperInvariant() switch
    {
        "TRACE" or "VERBOSE" => Seri.Events.LogEventLevel.Verbose,
        "DEBUG" => Seri.Events.LogEventLevel.Debug,
        "INFO" or "INFORMATION" => Seri.Events.LogEventLevel.Information,
        "WARN" or "WARNING" => Seri.Events.LogEventLevel.Warning,
        "ERROR" => Seri.Events.LogEventLevel.Error,
        "FATAL" => Seri.Events.LogEventLevel.Fatal,
        _ => defaulLevel,
    };

    /// <summary>
    /// Convierte el intervalo génerico para la creación de archivos a el intervalo génerico de Serilog.
    /// </summary>
    /// <param name="loggerInterval">El intervalo génerico para la creación de archivos.</param>
    /// <returns>El intervalo de Serilog RollingInterval.</returns>
    public static RollingInterval ToRollingInterval(this Abstractions.LoggerFileInterval loggerInterval)
    => loggerInterval.ToString().ToRollingInterval();

    /// <summary>
    /// Convierte el intervalo génerico para la creación de archivos a el intervalo génerico de Serilog.
    /// </summary>
    /// <param name="loggerInterval">El intervalo génerico para la creación de archivos.</param>
    /// <param name="defaulInterval">Intervalo por defecto a devolver.</param>
    /// <returns>El intervalo de Serilog RollingInterval.</returns>
    public static RollingInterval ToRollingInterval(this string? loggerInterval, RollingInterval defaulInterval = RollingInterval.Day)
    => loggerInterval switch
    {
        "Infinite" => RollingInterval.Infinite,
        "Year" => RollingInterval.Year,
        "Month" => RollingInterval.Month,
        "Hour" => RollingInterval.Hour,
        "Minute" => RollingInterval.Minute,
        _ => defaulInterval,
    };
}
