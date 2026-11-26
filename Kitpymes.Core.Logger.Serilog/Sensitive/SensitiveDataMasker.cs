// -----------------------------------------------------------------------
// <copyright file="SensitiveDataMasker.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger.Serilog;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using global::Serilog.Events;
using global::Serilog.Parsing;
using Kitpymes.Core.Logger.Abstractions;

/// <summary>
/// Oculta los datos sensibles de un evento: propiedades, objetos, diccionarios, listas y texto (también el de la plantilla).
/// </summary>
/// <remarks>
/// Las expresiones sólo buscan dónde empieza una clave; el valor se recorre a mano, en tiempo lineal. Si aun así una
/// expresión agota su tiempo, se oculta el texto entero (nunca se escribe sin revisar).
/// </remarks>
internal static class SensitiveDataMasker
{
    private const int MaxCachedTemplates = 1000;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    private static readonly MessageTemplateParser Parser = new MessageTemplateParser();

    private static readonly ScalarValue MaskValue = new ScalarValue(SensitiveDataNames.Mask);

    private static readonly ConcurrentDictionary<string, MaskedTemplate> Templates = new ConcurrentDictionary<string, MaskedTemplate>(StringComparer.Ordinal);

    // "clave": (JSON, y también con comillas simples). El valor lo recorre ScanQuotedValue.
    private static readonly Regex QuotedKey = new Regex(
        @"(?<q>[""'])(?<key>[^""'\r\n]{1,100})\k<q>\s*:\s*",
        RegexOptions.CultureInvariant,
        Timeout);

    // clave= o clave: al principio de una palabra (ToString de un record, cadenas de conexión, URL); "://" no cuenta.
    private static readonly Regex AssignmentKey = new Regex(
        @"(?<![\p{L}\p{N}_\-])(?<key>[\p{L}\p{N}_\-]{1,100})\s*(?:=|:(?!//))[ \t]*",
        RegexOptions.CultureInvariant,
        Timeout);

    // <clave>valor</clave>
    private static readonly Regex XmlKey = new Regex(
        @"<(?<key>[\p{L}\p{N}_\-]{1,100})>",
        RegexOptions.CultureInvariant,
        Timeout);

    private static readonly Regex Bearer = new Regex(
        @"(?<![\p{L}\p{N}])bearer\s+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        Timeout);

    // Clave justo antes de un hueco de la plantilla: "pwd={Valor}" oculta Valor aunque su nombre no sea sensible.
    private static readonly Regex KeyAtEnd = new Regex(
        @"(?<![\p{L}\p{N}_\-])(?<key>[\p{L}\p{N}_\-]{1,100})\s*(?:=|:)\s*$",
        RegexOptions.CultureInvariant,
        Timeout);

    /// <summary>
    /// Devuelve el evento con los datos sensibles ocultos, o el mismo si no tiene ninguno.
    /// </summary>
    /// <param name="logEvent">Evento.</param>
    /// <returns>El evento sin datos sensibles.</returns>
    internal static LogEvent Mask(LogEvent logEvent)
    {
        var template = MaskTemplate(logEvent.MessageTemplate);
        var changed = !ReferenceEquals(template.Template, logEvent.MessageTemplate);
        var properties = new List<LogEventProperty>(logEvent.Properties.Count);

        foreach (var property in logEvent.Properties)
        {
            var value = SensitiveDataNames.IsSensitive(property.Key) || template.MaskedProperties.Contains(property.Key)
                ? MaskValue
                : Mask(property.Value);

            changed |= !ReferenceEquals(value, property.Value);

            properties.Add(new LogEventProperty(property.Key, value));
        }

        return changed
            ? new LogEvent(logEvent.Timestamp, logEvent.Level, logEvent.Exception, template.Template, properties, logEvent.TraceId ?? default, logEvent.SpanId ?? default)
            : logEvent;
    }

    /// <summary>
    /// Oculta los valores sensibles de un texto.
    /// </summary>
    /// <param name="text">Texto.</param>
    /// <returns>El texto con los valores sensibles sustituidos por <c>***</c>; <c>***</c> entero si no se pudo revisar a tiempo.</returns>
    internal static string MaskText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        try
        {
            var masked = MaskQuoted(text);
            masked = MaskAssignments(masked);
            masked = MaskXml(masked);
            masked = MaskBearer(masked);
            return MaskUrlPasswords(masked);
        }
        catch (RegexMatchTimeoutException)
        {
            return SensitiveDataNames.Mask;
        }
    }

    private static MaskedTemplate MaskTemplate(MessageTemplate template)
    {
        if (Templates.TryGetValue(template.Text, out var cached))
        {
            return cached;
        }

        var masked = BuildMaskedTemplate(template);

        if (Templates.Count < MaxCachedTemplates)
        {
            Templates.TryAdd(template.Text, masked);
        }

        return masked;
    }

    // Sólo se tocan los trozos de texto: los huecos se quedan igual, para que la plantilla siga agrupando los eventos.
    private static MaskedTemplate BuildMaskedTemplate(MessageTemplate template)
    {
        var text = new StringBuilder(template.Text.Length);
        var maskedProperties = new HashSet<string>(StringComparer.Ordinal);
        var changed = false;
        var keyBeforeHole = false;

        foreach (var token in template.Tokens)
        {
            if (token is TextToken textToken)
            {
                var masked = MaskText(textToken.Text);
                changed |= !string.Equals(masked, textToken.Text, StringComparison.Ordinal);
                keyBeforeHole = EndsWithSensitiveKey(masked);

                // Las llaves del texto se escribieron dobladas ({{ }}) en la plantilla original.
                text.Append(masked.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal));
            }
            else
            {
                if (keyBeforeHole && token is PropertyToken propertyToken)
                {
                    maskedProperties.Add(propertyToken.PropertyName);
                }

                keyBeforeHole = false;
                text.Append(token.ToString());
            }
        }

        return new MaskedTemplate(changed ? Parser.Parse(text.ToString()) : template, maskedProperties);
    }

    /// <summary>
    /// Indica si el texto acaba en una clave sensible seguida de = o : (lo que venga detrás es un secreto).
    /// </summary>
    /// <param name="text">Texto.</param>
    /// <returns><c>true</c> si acaba en una clave sensible (también si no se pudo revisar a tiempo).</returns>
    internal static bool EndsWithSensitiveKey(string text)
    {
        try
        {
            var match = KeyAtEnd.Match(text);
            return match.Success && SensitiveDataNames.IsSensitive(match.Groups["key"].Value);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }

    private static string MaskQuoted(string text)
    {
        var result = new StringBuilder(text.Length);
        var position = 0;

        foreach (Match match in QuotedKey.Matches(text))
        {
            if (match.Index < position || !SensitiveDataNames.IsSensitive(match.Groups["key"].Value))
            {
                continue;
            }

            var start = match.Index + match.Length;
            var end = ScanQuotedValue(text, start);
            result.Append(text, position, start - position).Append('"').Append(SensitiveDataNames.Mask).Append('"');
            position = end;
        }

        return position == 0 ? text : result.Append(text, position, text.Length - position).ToString();
    }

    private static string MaskAssignments(string text)
    {
        var result = new StringBuilder(text.Length);
        var position = 0;

        foreach (Match match in AssignmentKey.Matches(text))
        {
            if (match.Index < position || !SensitiveDataNames.IsSensitive(match.Groups["key"].Value))
            {
                continue;
            }

            var start = match.Index + match.Length;
            var end = ScanAssignedValue(text, start);

            if (end == start)
            {
                continue;
            }

            result.Append(text, position, start - position).Append(SensitiveDataNames.Mask);
            position = end;
        }

        return position == 0 ? text : result.Append(text, position, text.Length - position).ToString();
    }

    private static string MaskXml(string text)
    {
        var result = new StringBuilder(text.Length);
        var position = 0;
        var unclosed = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in XmlKey.Matches(text))
        {
            var key = match.Groups["key"].Value;

            if (match.Index < position || unclosed.Contains(key) || !SensitiveDataNames.IsSensitive(key))
            {
                continue;
            }

            var start = match.Index + match.Length;
            var end = text.IndexOf("</" + key + ">", start, StringComparison.Ordinal);

            // Sin cierre más adelante, tampoco lo habrá para las siguientes aperturas: no se vuelve a buscar.
            if (end < 0)
            {
                unclosed.Add(key);
                continue;
            }

            result.Append(text, position, start - position).Append(SensitiveDataNames.Mask);
            position = end;
        }

        return position == 0 ? text : result.Append(text, position, text.Length - position).ToString();
    }

    private static string MaskBearer(string text)
    {
        var result = new StringBuilder(text.Length);
        var position = 0;

        foreach (Match match in Bearer.Matches(text))
        {
            var start = match.Index + match.Length;
            var end = start;

            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || "-._~+/=".Contains(text[end], StringComparison.Ordinal)))
            {
                end++;
            }

            if (match.Index < position || end == start)
            {
                continue;
            }

            result.Append(text, position, start - position).Append(SensitiveDataNames.Mask);
            position = end;
        }

        return position == 0 ? text : result.Append(text, position, text.Length - position).ToString();
    }

    // esquema://usuario:contraseña@servidor
    private static string MaskUrlPasswords(string text)
    {
        var result = new StringBuilder(text.Length);
        var position = 0;
        var scheme = text.IndexOf("://", StringComparison.Ordinal);

        while (scheme >= 0)
        {
            var start = scheme + 3;
            var end = start;

            while (end < text.Length && text[end] != '@' && text[end] != '/' && !char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            var colon = text.IndexOf(':', start, end - start);

            if (end < text.Length && text[end] == '@' && colon > start)
            {
                result.Append(text, position, colon + 1 - position).Append(SensitiveDataNames.Mask);
                position = end;
            }

            scheme = text.IndexOf("://", end, StringComparison.Ordinal);
        }

        return position == 0 ? text : result.Append(text, position, text.Length - position).ToString();
    }

    // Valor JSON: una cadena entre comillas (con \ de escape) o lo que haya hasta , } ] o fin de línea.
    private static int ScanQuotedValue(string text, int start)
    {
        if (start < text.Length && (text[start] == '"' || text[start] == '\''))
        {
            var quote = text[start];
            var i = start + 1;

            while (i < text.Length && text[i] != quote)
            {
                i += text[i] == '\\' ? 2 : 1;
            }

            return Math.Min(i + 1, text.Length);
        }

        var end = start;

        while (end < text.Length && ",}]\r\n".IndexOf(text[end], StringComparison.Ordinal) < 0)
        {
            end++;
        }

        return end;
    }

    // Valor de clave=valor: entre comillas (con '' o "" doblados) o hasta ; & } ] fin de línea, ", " (como en un
    // record) o el espacio que precede a otra clave (usuario=a clave=b). Los espacios del final no se cuentan.
    private static int ScanAssignedValue(string text, int start)
    {
        if (start < text.Length && (text[start] == '"' || text[start] == '\''))
        {
            var quote = text[start];
            var i = start + 1;

            while (i < text.Length)
            {
                if (text[i] == quote)
                {
                    if (i + 1 < text.Length && text[i + 1] == quote)
                    {
                        i += 2;
                        continue;
                    }

                    return i + 1;
                }

                i++;
            }

            return text.Length;
        }

        var end = start;

        while (end < text.Length)
        {
            var c = text[end];

            if (";&}]\r\n".IndexOf(c, StringComparison.Ordinal) >= 0
                || (c == ',' && end + 1 < text.Length && char.IsWhiteSpace(text[end + 1])))
            {
                break;
            }

            if (char.IsWhiteSpace(c))
            {
                // Los espacios seguidos se saltan de una vez: cada carácter se mira un número fijo de veces.
                var next = end;

                while (next < text.Length && char.IsWhiteSpace(text[next]) && text[next] != '\r' && text[next] != '\n')
                {
                    next++;
                }

                if (StartsKey(text, next))
                {
                    break;
                }

                end = next;
                continue;
            }

            end++;
        }

        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        return end;
    }

    // ¿Empieza aquí otra clave (palabra seguida de = o :)?
    private static bool StartsKey(string text, int index)
    {
        var i = index;
        var wordStart = i;

        while (i < text.Length && i - wordStart <= 100 && (char.IsLetterOrDigit(text[i]) || text[i] == '_' || text[i] == '-'))
        {
            i++;
        }

        if (i == wordStart)
        {
            return false;
        }

        while (i < text.Length && (text[i] == ' ' || text[i] == '\t'))
        {
            i++;
        }

        return i < text.Length && (text[i] == '=' || (text[i] == ':' && !(i + 2 < text.Length && text[i + 1] == '/' && text[i + 2] == '/')));
    }

    private static LogEventPropertyValue Mask(LogEventPropertyValue value)
    {
        switch (value)
        {
            case ScalarValue { Value: string text }:
                var masked = MaskText(text);
                return string.Equals(masked, text, StringComparison.Ordinal) ? value : new ScalarValue(masked);

            // Uri y otros escalares que no son de valor: se revisa su texto (una Uri con contraseña o ?token=).
            case ScalarValue { Value: { } other } when other is not ValueType:
                var otherText = other.ToString() ?? string.Empty;
                var maskedOther = MaskText(otherText);
                return string.Equals(maskedOther, otherText, StringComparison.Ordinal) ? value : new ScalarValue(maskedOther);

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

    /// <summary>
    /// Plantilla con el texto revisado y los huecos que van detrás de una clave sensible.
    /// </summary>
    /// <param name="Template">Plantilla (la misma si no ha cambiado).</param>
    /// <param name="MaskedProperties">Propiedades que se ocultan por ir detrás de una clave sensible.</param>
    private sealed record MaskedTemplate(MessageTemplate Template, HashSet<string> MaskedProperties);
}
