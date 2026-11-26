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
/// <para>Con <c>ISupportExternalScope</c> (lo normal con <c>LoggerFactory</c>), los scopes los guarda
/// Microsoft.Extensions.Logging, junto con los de <c>ActivityTrackingOptions</c> (TraceId, SpanId…): Serilog los recibe
/// a través de un proveedor de scopes envuelto que los entrega ya ocultos.</para>
/// </remarks>
[ProviderAlias("Serilog")]
internal sealed class SensitiveScopeLoggerProvider : ILoggerProvider, ISupportExternalScope
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
    /// Pasa a Serilog el proveedor de scopes de Microsoft.Extensions.Logging, envuelto para ocultar los datos sensibles.
    /// </summary>
    /// <param name="scopeProvider">Proveedor de scopes.</param>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
        (this.inner as ISupportExternalScope)?.SetScopeProvider(new MaskingScopeProvider(scopeProvider));

    private static object? Mask(object? state) =>
        state is IReadOnlyList<KeyValuePair<string, object?>> values ? new MaskedScopeState(values) : state;

    /// <summary>
    /// Proveedor de scopes que entrega cada estado con los datos sensibles ocultos.
    /// </summary>
    private sealed class MaskingScopeProvider : IExternalScopeProvider
    {
        private readonly IExternalScopeProvider inner;

        /// <summary>
        /// Inicializa una nueva instancia de la clase <see cref="MaskingScopeProvider"/>.
        /// </summary>
        /// <param name="inner">Proveedor de scopes de Microsoft.Extensions.Logging.</param>
        public MaskingScopeProvider(IExternalScopeProvider inner)
        {
            this.inner = inner;
        }

        /// <summary>
        /// Recorre los scopes, cada uno ya oculto.
        /// </summary>
        /// <typeparam name="TState">Tipo del estado de la llamada.</typeparam>
        /// <param name="callback">Función para cada scope.</param>
        /// <param name="state">Estado de la llamada.</param>
        public void ForEachScope<TState>(Action<object?, TState> callback, TState state) =>
            this.inner.ForEachScope((scope, s) => callback(Mask(scope), s), state);

        /// <summary>
        /// Abre un scope.
        /// </summary>
        /// <param name="state">Estado del scope.</param>
        /// <returns>El scope.</returns>
        public IDisposable Push(object? state) => this.inner.Push(state);
    }

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
            => this.inner.BeginScope(Mask(state) ?? state);

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

        private static readonly Regex Hole = new Regex(@"(?<!\{)\{(?<name>[^{}:,]+)(?:,(?<alignment>[^{}:]*))?(?::(?<format>[^{}]*))?\}(?!\})", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        private readonly List<KeyValuePair<string, object?>> values;

        private readonly string text;

        /// <summary>
        /// Inicializa una nueva instancia de la clase <see cref="MaskedScopeState"/>.
        /// </summary>
        /// <param name="original">Estado original.</param>
        public MaskedScopeState(IReadOnlyList<KeyValuePair<string, object?>> original)
        {
            var format = original.FirstOrDefault(v => v.Key == OriginalFormat).Value as string;
            var afterKey = format is null ? new HashSet<int>() : HolesAfterSensitiveKey(format);
            var position = 0;

            this.values = original
                .Select(v =>
                {
                    if (v.Key == OriginalFormat)
                    {
                        return v;
                    }

                    var masked = SensitiveDataNames.IsSensitive(v.Key) || afterKey.Contains(position);
                    position++;
                    return masked ? new KeyValuePair<string, object?>(v.Key, SensitiveDataNames.Mask) : v;
                })
                .ToList();

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

        // Huecos (por posición) que van detrás de una clave sensible: "pwd={Valor}".
        private static HashSet<int> HolesAfterSensitiveKey(string format)
        {
            var result = new HashSet<int>();
            var previous = 0;
            var index = 0;

            foreach (Match match in Hole.Matches(format))
            {
                if (SensitiveDataMasker.EndsWithSensitiveKey(format.Substring(previous, match.Index - previous)))
                {
                    result.Add(index);
                }

                previous = match.Index + match.Length;
                index++;
            }

            return result;
        }

        // Como FormattedLogValues: huecos por orden, cultura invariante, alineación y listas unidas con ", ".
        private static string Format(object? value, string? format)
        {
            switch (value)
            {
                case null:
                    return "(null)";
                case string text:
                    return text;
                case IFormattable formattable:
                    return formattable.ToString(format, CultureInfo.InvariantCulture);
                case IEnumerable items:
                    return string.Join(", ", items.Cast<object?>().Select(i => Format(i, format)));
                default:
                    return value.ToString() ?? string.Empty;
            }
        }

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

                var value = Format(holes[index++].Value, match.Groups["format"].Success ? match.Groups["format"].Value : null);

                if (match.Groups["alignment"].Success && int.TryParse(match.Groups["alignment"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var alignment))
                {
                    value = alignment >= 0 ? value.PadLeft(alignment) : value.PadRight(-alignment);
                }

                return value;
            });

            return new StringBuilder(rendered).Replace("{{", "{").Replace("}}", "}").ToString();
        }
    }
}
