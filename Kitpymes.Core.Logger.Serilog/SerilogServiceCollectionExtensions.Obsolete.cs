// -----------------------------------------------------------------------
// <copyright file="SerilogServiceCollectionExtensions.Obsolete.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger.Serilog;

using System;
using System.ComponentModel;
using System.Linq;
using Kitpymes.Core.Logger.Abstractions;
using Kitpymes.Core.Shared.Extensions;
using Microsoft.Extensions.DependencyInjection;

/*
   Clase de extensión SerilogServiceCollectionExtensions
   Contiene las extensiones de los servicios del logeo de errores para Serilog
*/

/// <summary>
/// Clase de extensión <c>SerilogServiceCollectionExtensions</c>.
/// Contiene las extensiones de los servicios del logeo de errores para Serilogs.
/// </summary>
/// <remarks>
/// <para>En esta clase se pueden agregar todas las extensiones de los servicios para el logeo de errores de Serilog.</para>
/// </remarks>
[Obsolete("Use builder.Logging.AddKitpymesLogging(...) (Kitpymes.Core.Logger) o builder.Logging.AddKitpymesSerilog(...) (Kitpymes.Core.Logger.Serilog), y ILogger<T>. Se elimina en 3.0.0.", DiagnosticId = "KPY0011")]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SerilogServiceCollectionExtensions
{
    /// <summary>
    /// Carga el servicio de logeo de errores de Serilog.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <param name="settings">Configuración de Serilog.</param>
    /// <returns>La interface ILoggerService registrada.</returns>
    /// <remarks>
    /// <para>Registra una única instancia (singleton) y la devuelve. Si ya había un <see cref="ILoggerService"/>
    /// registrado, no registra otro y devuelve el existente.</para>
    /// <para>El contenedor libera la instancia, y vacía los eventos pendientes, al liberarse él; si no se resuelve
    /// nunca del contenedor, se vacía al terminar el proceso o al llamar a <see cref="SerilogProvider.Dispose()"/>.</para>
    /// </remarks>
    public static ILoggerService? LoadSerilog(this IServiceCollection services, SerilogSettings? settings)
    {
        var existing = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(ILoggerService));

        if (existing is not null)
        {
            return existing.ImplementationInstance as ILoggerService
                ?? (existing.ImplementationFactory?.Target as Registration)?.Provider
                ?? services.ToService<ILoggerService>();
        }

        var registration = new Registration(new SerilogProvider(settings.ToThrowIfNullOrEmpty()));

        // Con fábrica y no con la instancia: así el contenedor la libera (y vacía los eventos) al liberarse.
        services.AddSingleton<ILoggerService>(registration.Get);

        return registration.Provider;
    }

    /// <summary>
    /// Carga el servicio de logeo de errores de Serilog.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <param name="options">Configuración de Serilog.</param>
    /// <returns>La interface ILoggerService.</returns>
    public static ILoggerService? LoadSerilog(this IServiceCollection services, Action<SerilogOptions> options)
        => services.LoadSerilog(options.ToConfigureOrDefault().SerilogSettings);

    /// <summary>
    /// Fábrica del registro: permite saber qué instancia se registró sin construir un contenedor.
    /// </summary>
    /// <param name="provider">Instancia registrada.</param>
    private sealed class Registration(SerilogProvider provider)
    {
        /// <summary>
        /// Obtiene la instancia registrada.
        /// </summary>
        public SerilogProvider Provider { get; } = provider;

        /// <summary>
        /// Devuelve la instancia registrada.
        /// </summary>
        /// <param name="services">Contenedor (no se usa).</param>
        /// <returns>La instancia registrada.</returns>
        public ILoggerService Get(IServiceProvider services) => this.Provider;
    }
}
