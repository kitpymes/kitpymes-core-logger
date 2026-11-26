// -----------------------------------------------------------------------
// <copyright file="SensitiveScopeLoggerProvider.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger.Serilog;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Kitpymes.Core.Logger.Abstractions;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

/// <summary>
/// Envuelve el proveedor de Serilog para ocultar los datos sensibles de los scopes (<c>BeginScope</c>).
/// </summary>
/// <remarks>
/// <para>Serilog.Extensions.Logging guarda en la propiedad <c>Scope</c> el texto ya formateado del scope
/// (<c>BeginScope("Usuario {Pwd}", pwd)</c> → <c>"Usuario s3cr3t"</c>): sin clave, el sink no lo puede ocultar.
/// Aquí se cambia el estado del scope antes de que llegue a Serilog.</para>
/// <para>No implementa <c>ISupportExternalScope</c> a propósito: así Microsoft.Extensions.Logging llama a este
/// <c>BeginScope</c> en vez de guardar el scope por su cuenta.</para>
/// </remarks>
[ProviderAlias("Serilog")]
internal sealed class SensitiveScopeLoggerProvider : ILoggerProvider
{
    private readonly ILoggerProvider inner;

    /// <summary>
    /// Inicializa una nueva instancia de la clase <see cref="SensitiveScopeLoggerProvider"/>.
    /// </summary>
    /// <param name="inner">Proveedor de Serilog.</param>
    public SensitiveScopeLoggerProvider(ILoggerProvider inner)
    {
        this.inner = inner;
    }

    /// <summary>
    /// Crea un logger de la categoría.
    /// </summary>
    /// <param name="categoryName">Categoría.</param>
    /// <returns>El logger.</returns>
    public ILogger CreateLogger(string categoryName) => new SensitiveScopeLogger(this.inner.CreateLogger(categoryName));

    /// <summary>
    /// Libera el proveedor de Serilog (y con él el logger de Serilog).
    /// </summary>
    public void Dispose() => this.inner.Dispose();

    /// <summary>
    /// Logger que pasa todo a Serilog salvo el estado de los scopes, que va con los datos sensibles ocultos.
    /// </summary>
    private sealed class SensitiveScopeLogger : ILogger
    {
        private readonly ILogger inner;

        /// <summary>
        /// Inicializa una nueva instancia de la clase <see cref="SensitiveScopeLogger"/>.
        /// </summary>
        /// <param name="inner">Logger de Serilog.</param>
        public SensitiveScopeLogger(ILogger inner)
        {
            this.inner = inner;
        }

        /// <summary>
        /// Abre un scope con el estado sin datos sensibles.
        /// </summary>
        /// <typeparam name="TState">Tipo del estado.</typeparam>
        /// <param name="state">Estado.</param>
        /// <returns>El scope.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => state is IReadOnlyList<KeyValuePair<string, object?>> values
                ? this.inner.BeginScope(new MaskedScopeState(values))
                : this.inner.BeginScope(state);

        /// <summary>
        /// Indica si el nivel está activo.
        /// </summary>
        /// <param name="logLevel">Nivel.</param>
        /// <returns><c>true</c> si está activo.</returns>
        public bool IsEnabled(LogLevel logLevel) => this.inner.IsEnabled(logLevel);

        /// <summary>
        /// Escribe el evento (el sink oculta sus datos sensibles).
        /// </summary>
        /// <typeparam name="TState">Tipo del estado.</typeparam>
        /// <param name="logLevel">Nivel.</param>
        /// <param name="eventId">Evento.</param>
        /// <param name="state">Estado.</param>
        /// <param name="exception">Excepción.</param>
        /// <param name="formatter">Formateador.</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => this.inner.Log(logLevel, eventId, state, exception, formatter);
    }

    /// <summary>
    /// Estado de un scope con los valores sensibles cambiados por <c>***</c>, también en su texto.
    /// </summary>
    private sealed class MaskedScopeState : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private const string OriginalFormat = "{OriginalFormat}";

        private static readonly Regex Hole = new Regex(@"(?<!\{)\{(?<name>[^{}:,]+)(?:,[^{}:]*)?(?::(?<format>[^{}]*))?\}(?!\})", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        private readonly List<KeyValuePair<string, object?>> values;

        private readonly string text;

        /// <summary>
        /// Inicializa una nueva instancia de la clase <see cref="MaskedScopeState"/>.
        /// </summary>
        /// <param name="original">Estado original.</param>
        public MaskedScopeState(IReadOnlyList<KeyValuePair<string, object?>> original)
        {
            this.values = original
                .Select(v => v.Key != OriginalFormat && SensitiveDataNames.IsSensitive(v.Key)
                    ? new KeyValuePair<string, object?>(v.Key, SensitiveDataNames.Mask)
                    : v)
                .ToList();

            var format = this.values.FirstOrDefault(v => v.Key == OriginalFormat).Value as string;

            this.text = format is null ? original.ToString() ?? string.Empty : SensitiveDataMasker.MaskText(this.Render(format));
        }

        /// <summary>
        /// Obtiene el número de valores.
        /// </summary>
        public int Count => this.values.Count;

        /// <summary>
        /// Obtiene un valor.
        /// </summary>
        /// <param name="index">Posición.</param>
        /// <returns>El valor.</returns>
        public KeyValuePair<string, object?> this[int index] => this.values[index];

        /// <summary>
        /// Recorre los valores.
        /// </summary>
        /// <returns>El enumerador.</returns>
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => this.values.GetEnumerator();

        /// <summary>
        /// Texto del scope, con los valores sensibles ocultos.
        /// </summary>
        /// <returns>El texto.</returns>
        public override string ToString() => this.text;

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        // Como FormattedLogValues: los huecos se rellenan por orden con los valores.
        private string Render(string format)
        {
            var index = 0;
            var holes = this.values.Where(v => v.Key != OriginalFormat).ToList();

            var rendered = Hole.Replace(format, match =>
            {
                if (index >= holes.Count)
                {
                    return match.Value;
                }

                var value = holes[index++].Value;
                var valueFormat = match.Groups["format"].Success ? match.Groups["format"].Value : null;

                return value is IFormattable formattable
                    ? formattable.ToString(valueFormat, CultureInfo.CurrentCulture)
                    : value?.ToString() ?? "(null)";
            });

            return new StringBuilder(rendered).Replace("{{", "{").Replace("}}", "}").ToString();
        }
    }
}
