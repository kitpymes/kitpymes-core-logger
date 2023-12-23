// -----------------------------------------------------------------------
// <copyright file="LoggerServiceCollectionExtensions.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger;

using System;
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
public static class LoggerServiceCollectionExtensions
{
    /// <summary>
    /// Carga la configuracion del logeo de errores desde el LoggerSettings del appsettings.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <returns>IServiceCollection.</returns>
    public static IServiceCollection LoadLogger(this IServiceCollection services)
    {
        var configuration = services.ToService<IConfiguration>();

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
