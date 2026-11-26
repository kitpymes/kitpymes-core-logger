// -----------------------------------------------------------------------
// <copyright file="LoggerServiceCollectionExtensions.Obsolete.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger;

using System;
using System.ComponentModel;
using System.Linq;
using Kitpymes.Core.Logger.Serilog;
using Kitpymes.Core.Shared.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/*
    Clase de extensión LoggerServiceCollectionExtensions
    Contiene las extensiones de los servicios del logeo de errores
*/

/// <summary>
/// Clase de extensión <c>LoggerServiceCollectionExtensions</c>.
/// Contiene las extensiones de los servicios del logeo de errores.
/// </summary>
/// <remarks>
/// <para>En esta clase se pueden agregar todas las extensiones de los servicios para el logeo de errores.</para>
/// </remarks>
[Obsolete("Use builder.Logging.AddKitpymesLogging(...) (Kitpymes.Core.Logger) o builder.Logging.AddKitpymesSerilog(...) (Kitpymes.Core.Logger.Serilog), y ILogger<T>. Se elimina en 3.0.0.", DiagnosticId = "KPY0011")]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LoggerServiceCollectionExtensions
{
    /// <summary>
    /// Carga la configuracion del logeo de errores desde el LoggerSettings del appsettings.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <returns>IServiceCollection.</returns>
    public static IServiceCollection LoadLogger(this IServiceCollection services)
    {
        // La IConfiguration que registra el host suele ser una instancia: se toma sin construir un contenedor.
        var configuration = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(IConfiguration))?.ImplementationInstance as IConfiguration
            ?? services.ToService<IConfiguration>();

        var settings = configuration?.GetSection(nameof(LoggerSettings))?.Get<LoggerSettings>();

        services.LoadLogger(settings);

        return services;
    }

    /// <summary>
    /// Carga la configuracion del logeo de errores desde las opciones.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <param name="options">Las opciones personalizadas.</param>
    /// <returns>IServiceCollection.</returns>
    public static IServiceCollection LoadLogger(this IServiceCollection services, Action<LoggerOptions> options)
    {
        var settings = options.ToConfigureOrDefault().LoggerSettings;

        services.LoadLogger(settings);

        return services;
    }

    /// <summary>
    /// Carga la configuracion del logeo de errores desde las opciones.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <param name="settings">Configuracion del logeo de errores.</param>
    /// <returns>IServiceCollection.</returns>
    public static IServiceCollection LoadLogger(this IServiceCollection services, LoggerSettings? settings)
    {
        if (settings?.Enabled == true)
        {
            if (settings.IsSerilogEnabled == true)
            {
                services.LoadSerilog(settings.SerilogSettings);
            }
        }

        return services;
    }
}
