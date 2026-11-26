// -----------------------------------------------------------------------
// <copyright file="SourceContextTitleEnricher.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger.Serilog;

using global::Serilog.Core;
using global::Serilog.Events;

/// <summary>
/// Si el evento no tiene <c>Title</c>, lo toma del nombre corto de la categoría (<c>SourceContext</c>) de Microsoft.Extensions.Logging.
/// </summary>
/// <remarks>Así las plantillas con <c>{Title}</c> siguen funcionando con <c>ILogger&lt;T&gt;</c>.</remarks>
internal sealed class SourceContextTitleEnricher : ILogEventEnricher
{
    /// <summary>
    /// Añade el título a partir de la categoría.
    /// </summary>
    /// <param name="logEvent">Evento.</param>
    /// <param name="propertyFactory">Fábrica de propiedades (no se usa).</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!logEvent.Properties.ContainsKey(TitleEnricher.TitlePropertyName)
            && logEvent.Properties.TryGetValue("SourceContext", out var value)
            && value is ScalarValue { Value: string context })
        {
            logEvent.AddPropertyIfAbsent(new LogEventProperty(TitleEnricher.TitlePropertyName, new ScalarValue(ShortName(context))));
        }
    }

    /// <summary>
    /// Nombre corto de una categoría: sin espacio de nombres ni argumentos genéricos.
    /// </summary>
    /// <param name="context">Categoría.</param>
    /// <returns>El nombre corto.</returns>
    internal static string ShortName(string context)
    {
        var name = context;

        foreach (var end in new[] { '<', '`', '[' })
        {
            var index = name.IndexOf(end, System.StringComparison.Ordinal);

            if (index > 0)
            {
                name = name.Substring(0, index);
            }
        }

        var dot = name.LastIndexOf('.');

        return dot >= 0 && dot < name.Length - 1 ? name.Substring(dot + 1) : name;
    }
}
