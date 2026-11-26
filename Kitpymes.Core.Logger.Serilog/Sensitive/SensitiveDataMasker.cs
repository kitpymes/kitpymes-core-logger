// -----------------------------------------------------------------------
// <copyright file="SensitiveDataMasker.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger.Serilog;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using global::Serilog.Events;
using global::Serilog.Parsing;
using Kitpymes.Core.Logger.Abstractions;

/// <summary>
/// Oculta los datos sensibles de un evento: propiedades, objetos, diccionarios, listas y texto (también el de la plantilla).
/// </summary>
internal static class SensitiveDataMasker
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    private static readonly MessageTemplateParser Parser = new MessageTemplateParser();

    private static readonly ScalarValue MaskValue = new ScalarValue(SensitiveDataNames.Mask);

    // "clave": valor (JSON, y también con comillas simples).
    private static readonly Regex QuotedPair = new Regex(
        @"(?<q>[""'])(?<key>[^""'\r\n]{1,100})\k<q>(?<sep>\s*:\s*)(?<value>""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'|[^,}\]\r\n]+)",
        RegexOptions.CultureInvariant,
        Timeout);

    // clave = valor y clave: valor (ToString de un record, cadenas de conexión, parámetros de una URL). El valor acaba
    // en , ; & } ] o fin de línea, o donde empieza otra clave (usuario=a clave=b); "://" no es un separador.
    private static readonly Regex Assignment = new Regex(
        @"(?<key>[\p{L}\p{N}_\-]{1,100})(?<sep>\s*(?:=|:(?!//))\s*)(?<value>[^,;&}\]\s\r\n](?:(?!\s+[\p{L}\p{N}_\-]{1,100}\s*(?:=|:(?!//)))[^,;&}\]\r\n])*)",
        RegexOptions.CultureInvariant,
        Timeout);

    private static readonly Regex Bearer = new Regex(
        @"(?<=\bbearer\s+)[A-Za-z0-9\-._~+/]+=*",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        Timeout);

    private static readonly Regex UrlPassword = new Regex(
        @"(?<=://[^/\s:@]+:)[^/\s@]+(?=@)",
        RegexOptions.CultureInvariant,
        Timeout);

    /// <summary>
    /// Devuelve el evento con los datos sensibles ocultos, o el mismo si no tiene ninguno.
    /// </summary>
    /// <param name="logEvent">Evento.</param>
    /// <returns>El evento sin datos sensibles.</returns>
    internal static LogEvent Mask(LogEvent logEvent)
    {
        var changed = false;

        var text = logEvent.MessageTemplate.Text;
        var maskedText = MaskText(text);
        var template = logEvent.MessageTemplate;

        if (!string.Equals(text, maskedText, StringComparison.Ordinal))
        {
            template = Parser.Parse(maskedText);
            changed = true;
        }

        var properties = new List<LogEventProperty>(logEvent.Properties.Count);

        foreach (var property in logEvent.Properties)
        {
            var value = SensitiveDataNames.IsSensitive(property.Key) ? MaskValue : Mask(property.Value);

            changed |= !ReferenceEquals(value, property.Value);

            properties.Add(new LogEventProperty(property.Key, value));
        }

        return changed
            ? new LogEvent(logEvent.Timestamp, logEvent.Level, logEvent.Exception, template, properties, logEvent.TraceId ?? default, logEvent.SpanId ?? default)
            : logEvent;
    }

    /// <summary>
    /// Oculta los valores sensibles de un texto.
    /// </summary>
    /// <param name="text">Texto.</param>
    /// <returns>El texto con los valores sensibles sustituidos por <c>***</c>.</returns>
    internal static string MaskText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var masked = QuotedPair.Replace(text, match => SensitiveDataNames.IsSensitive(match.Groups["key"].Value)
            ? match.Groups["q"].Value + match.Groups["key"].Value + match.Groups["q"].Value + match.Groups["sep"].Value + "\"" + SensitiveDataNames.Mask + "\""
            : match.Value);

        masked = Assignment.Replace(masked, match => SensitiveDataNames.IsSensitive(match.Groups["key"].Value)
            ? match.Groups["key"].Value + match.Groups["sep"].Value + SensitiveDataNames.Mask + TrailingSpaces(match.Groups["value"].Value)
            : match.Value);

        masked = Bearer.Replace(masked, SensitiveDataNames.Mask);

        return UrlPassword.Replace(masked, SensitiveDataNames.Mask);
    }

    private static string TrailingSpaces(string value) => value.Substring(value.TrimEnd().Length);

    private static LogEventPropertyValue Mask(LogEventPropertyValue value)
    {
        switch (value)
        {
            case ScalarValue { Value: string text }:
                var masked = MaskText(text);
                return string.Equals(masked, text, StringComparison.Ordinal) ? value : new ScalarValue(masked);

            case StructureValue structure:
                var members = structure.Properties
                    .Select(p => new LogEventProperty(p.Name, SensitiveDataNames.IsSensitive(p.Name) ? MaskValue : Mask(p.Value)))
                    .ToList();
                return members.Zip(structure.Properties).All(p => ReferenceEquals(p.First.Value, p.Second.Value))
                    ? value
                    : new StructureValue(members, structure.TypeTag);

            case SequenceValue sequence:
                var elements = sequence.Elements.Select(Mask).ToList();
                return elements.Zip(sequence.Elements).All(p => ReferenceEquals(p.First, p.Second))
                    ? value
                    : new SequenceValue(elements);

            case DictionaryValue dictionary:
                var entries = dictionary.Elements
                    .Select(e => new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                        e.Key,
                        e.Key.Value is string key && SensitiveDataNames.IsSensitive(key) ? MaskValue : Mask(e.Value)))
                    .ToList();
                return entries.Zip(dictionary.Elements).All(p => ReferenceEquals(p.First.Value, p.Second.Value))
                    ? value
                    : new DictionaryValue(entries);

            default:
                return value;
        }
    }
}
