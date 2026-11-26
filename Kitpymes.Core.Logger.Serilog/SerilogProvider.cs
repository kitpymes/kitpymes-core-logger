// -----------------------------------------------------------------------
// <copyright file="SerilogProvider.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger.Serilog;

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using global::Serilog;

/*
  Clase del servicio SerilogProvider
  Contiene la implemetación del servicio del logeo de errores de Serilog
*/

/// <summary>
/// Clase del servicio <c>ILoggerService</c>.
/// Contiene la implemetación del servicio del logeo de errores de Serilog.
/// </summary>
/// <remarks>
/// <para>Cada instancia creada con el constructor tiene un único logger de Serilog, que se crea con el primer
/// <see cref="CreateLogger(string)"/> y se libera con <see cref="Dispose()"/> o al terminar el proceso.</para>
/// <para>Cada <see cref="CreateLogger(string)"/> devuelve un logger nuevo con su título, que escribe en ese
/// logger de Serilog; no se libera por separado.</para>
/// </remarks>
public class SerilogProvider : Abstractions.ILogger, Abstractions.ILoggerService, IDisposable
{
    private readonly SerilogSettings settings;

    private readonly Lazy<global::Serilog.Core.Logger> root;

    private readonly SerilogProvider owner;

    private readonly ILogger? logger;

    private int disposed;

    /// <summary>
    /// Inicializa una nueva instancia de la clase <see cref="SerilogProvider"/>.
    /// </summary>
    /// <param name="settings">Configuración de Serilog.</param>
    /// <exception cref="ArgumentNullException">Si <paramref name="settings"/> es nulo.</exception>
    public SerilogProvider(SerilogSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.root = new Lazy<global::Serilog.Core.Logger>(this.CreateRoot, LazyThreadSafetyMode.ExecutionAndPublication);
        this.owner = this;
        AppDomain.CurrentDomain.ProcessExit += this.OnProcessExit;
    }

    private SerilogProvider(SerilogProvider owner, string title)
    {
        this.settings = owner.settings;
        this.root = owner.root;
        this.owner = owner;
        this.logger = owner.root.Value.ForContext(
            TitleEnricher.TitlePropertyName,
            string.IsNullOrWhiteSpace(title) ? Process.GetCurrentProcess().ProcessName : title);
    }

    /// <summary>
    /// Crea un log nuevo.
    /// </summary>
    /// <param name="title">Título del log.</param>
    /// <returns>Un logger nuevo con ese título.</returns>
    public Abstractions.ILogger CreateLogger(string title) => new SerilogProvider(this.owner, title);

    /// <summary>
    /// Crea un log nuevo.
    /// </summary>
    /// <typeparam name="TSourceContext">Tipo cuyo nombre se usa como título.</typeparam>
    /// <returns>Un logger nuevo con ese título.</returns>
    public Abstractions.ILogger CreateLogger<TSourceContext>() => this.CreateLogger(typeof(TSourceContext).Name);

    /// <summary>
    /// Registra un mensaje de nivel Trace.
    /// </summary>
    /// <param name="message">Mensaje.</param>
    /// <returns>Este logger.</returns>
    public Abstractions.ILogger Trace(string message)
    {
        this.logger?.Verbose(message);
        return this;
    }

    /// <summary>
    /// Registra un mensaje de nivel Trace con datos.
    /// </summary>
    /// <param name="message">Mensaje.</param>
    /// <param name="data">Datos.</param>
    /// <returns>Este logger.</returns>
    public Abstractions.ILogger Trace(string message, object data)
    {
        this.logger?.Verbose(message + " => {Data}", data);
        return this;
    }

    /// <summary>
    /// Registra un mensaje de nivel Debug.
    /// </summary>
    /// <param name="message">Mensaje.</param>
    /// <returns>Este logger.</returns>
    public Abstractions.ILogger Debug(string message)
    {
        this.logger?.Debug(message);
        return this;
    }

    /// <summary>
    /// Registra un mensaje de nivel Debug con datos.
    /// </summary>
    /// <param name="message">Mensaje.</param>
    /// <param name="data">Datos.</param>
    /// <returns>Este logger.</returns>
    public Abstractions.ILogger Debug(string message, object data)
    {
        this.logger?.Debug(message + " => {Data}", data);
        return this;
    }

    /// <summary>
    /// Registra un mensaje de nivel Info.
    /// </summary>
    /// <param name="message">Mensaje.</param>
    /// <returns>Este logger.</returns>
    public Abstractions.ILogger Info(string message)
    {
        this.logger?.Information(message);
        return this;
    }

    /// <summary>
    /// Registra un mensaje de nivel Info con datos.
    /// </summary>
    /// <param name="message">Mensaje.</param>
    /// <param name="data">Datos.</param>
    /// <returns>Este logger.</returns>
    public Abstractions.ILogger Info(string message, object data)
    {
        this.logger?.Information(message + " => {Data}", data);
        return this;
    }

    /// <summary>
    /// Registra un evento de nivel Info con una plantilla.
    /// </summary>
    /// <param name="eventName">Nombre del evento.</param>
    /// <param name="templateMessage">Plantilla del mensaje.</param>
    /// <param name="propertyValues">Valores de la plantilla.</param>
    /// <returns>Este logger.</returns>
    public Abstractions.ILogger Info(string eventName, string templateMessage, params object[] propertyValues)
    {
        this.logger?.Information("{EventName} => " + templateMessage, new object[] { eventName }.Concat(propertyValues).ToArray());
        return this;
    }

    /// <summary>
    /// Registra un mensaje de nivel Warning.
    /// </summary>
    /// <param name="message">Mensaje.</param>
    /// <returns>Este logger.</returns>
    public Abstractions.ILogger Warning(string message)
    {
        this.logger?.Warning(message);
        return this;
    }

    /// <summary>
    /// Registra un mensaje de nivel Warning con datos.
    /// </summary>
    /// <param name="message">Mensaje.</param>
    /// <param name="data">Datos.</param>
    /// <returns>Este logger.</returns>
    public Abstractions.ILogger Warning(string message, object data)
    {
        this.logger?.Warning(message + " => {Data}", data);
        return this;
    }

    /// <summary>
    /// Registra un mensaje de nivel Error.
    /// </summary>
    /// <param name="message">Mensaje.</param>
    /// <returns>Este logger.</returns>
    public Abstractions.ILogger Error(string message)
    {
        this.logger?.Error(message);
        return this;
    }

    /// <summary>
    /// Registra un mensaje de nivel Error con datos.
    /// </summary>
    /// <param name="message">Mensaje.</param>
    /// <param name="data">Datos.</param>
    /// <returns>Este logger.</returns>
    public Abstractions.ILogger Error(string message, object data)
    {
        this.logger?.Error(message + " => {Data}", data);
        return this;
    }

    /// <summary>
    /// Registra un evento de nivel Error con una plantilla.
    /// </summary>
    /// <param name="eventName">Nombre del evento.</param>
    /// <param name="templateMessage">Plantilla del mensaje.</param>
    /// <param name="propertyValues">Valores de la plantilla.</param>
    /// <returns>Este logger.</returns>
    public Abstractions.ILogger Error(string eventName, string templateMessage, params object[] propertyValues)
    {
        this.logger?.Error("{EventName} => " + templateMessage, new object[] { eventName }.Concat(propertyValues).ToArray());
        return this;
    }

    /// <summary>
    /// Registra una excepción de nivel Error.
    /// </summary>
    /// <param name="exception">Excepción, que se guarda como excepción del evento.</param>
    /// <returns>Este logger.</returns>
    public Abstractions.ILogger Error(Exception exception)
    {
        this.logger?.Error(exception, "Exception => {ExceptionMessage}", exception?.Message);
        return this;
    }

    /// <summary>
    /// Registra un mensaje y una excepción de nivel Error.
    /// </summary>
    /// <param name="message">Mensaje.</param>
    /// <param name="exception">Excepción, que se guarda como excepción del evento.</param>
    /// <returns>Este logger.</returns>
    public Abstractions.ILogger Error(string message, Exception exception)
    {
        this.logger?.Error(exception, message);
        return this;
    }

    /// <summary>
    /// Libera el logger de Serilog de esta instancia y vacía los eventos pendientes.
    /// </summary>
    /// <remarks>Sólo actúa en la instancia creada con el constructor; en un logger de <see cref="CreateLogger(string)"/> no hace nada.</remarks>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Libera el logger de Serilog de esta instancia y vacía los eventos pendientes.
    /// </summary>
    /// <param name="disposing">Si se llama desde <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || !ReferenceEquals(this.owner, this) || Interlocked.Exchange(ref this.disposed, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.ProcessExit -= this.OnProcessExit;

        if (this.root.IsValueCreated)
        {
            this.root.Value.Dispose();
        }
    }

    private global::Serilog.Core.Logger CreateRoot() => new LoggerConfiguration()
        .MinimumLevel.Is(this.settings.MinimumLevel.ToMinimumLevel())
        .AddDefaultSettings(string.Empty)
        .AddConsole(this.settings.Console)
        .AddFile(this.settings.File)
        .AddEmail(this.settings.Email)
        .AddSqlserver(this.settings.SqlServer)
        .CreateLogger();

    private void OnProcessExit(object? sender, EventArgs e) => this.Dispose();
}
