// -----------------------------------------------------------------------
// <copyright file="SensitiveDataNames.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger.Abstractions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// Decide si un nombre (de propiedad, de clave JSON, de parámetro) es de un dato sensible.
/// </summary>
/// <remarks>
/// <para>Se compara por palabras, no por "contiene": el nombre se parte en palabras (mayúsculas, <c>_</c>, <c>-</c>,
/// espacios, puntos), sin tildes y en minúsculas. Es sensible si alguna palabra es de credencial (password, pwd,
/// contraseña, contrasenya, clave, clau, secret, token, pin, otp, cvv, cvc, iban, authorization, bearer, cookie,
/// apikey, accesstoken, clientsecret, accountkey, sig, credential…), si acaba en <c>pass</c> (salvo <c>ByPass</c>) o si
/// contiene dos seguidas como api key, connection string, credit card, card number, private key, access key,
/// account key o cadena conexión.</para>
/// <para><c>CancellationToken</c> no es sensible. <c>Secretaria</c> tampoco: <c>secretaria</c> no es la palabra <c>secret</c>.</para>
/// <para>Cubre credenciales y secretos; no datos personales (DNI, correo, teléfono).</para>
/// </remarks>
public static class SensitiveDataNames
{
    /// <summary>
    /// Texto que sustituye a un valor sensible.
    /// </summary>
    public const string Mask = "***";

    private static readonly HashSet<string> Words = new HashSet<string>
    {
        "password", "passwd", "passphrase", "pwd", "contrasena", "contrasenya", "clave", "clau",
        "secret", "secrets", "secreto", "token", "tokens", "pin", "otp", "cvv", "cvc", "iban",
        "authorization", "bearer", "cookie", "cookies", "apikey", "connectionstring", "credential", "credentials",
        "accesstoken", "refreshtoken", "idtoken", "clientsecret", "privatekey", "accountkey", "secretkey", "sig",
        "sharedaccesskey", "sharedaccesssignature",
    };

    private static readonly ConcurrentDictionary<string, bool> Cache = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

    private static readonly (string First, string Second)[] Pairs =
    {
        ("api", "key"), ("connection", "string"), ("credit", "card"), ("card", "number"),
        ("private", "key"), ("access", "key"), ("secret", "key"), ("account", "key"), ("cadena", "conexion"),
    };

    /// <summary>
    /// Indica si el nombre es de un dato sensible.
    /// </summary>
    /// <param name="name">Nombre de la propiedad, clave o parámetro.</param>
    /// <returns><c>true</c> si es sensible; <c>false</c> si no lo es o es nulo o vacío.</returns>
    public static bool IsSensitive(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (Cache.TryGetValue(name, out var cached))
        {
            return cached;
        }

        var sensitive = Check(Split(name));

        // Los nombres de propiedades se repiten; el texto libre no se guarda (sólo nombres cortos y hasta un tope).
        if (name.Length <= 100 && Cache.Count < 10000)
        {
            Cache.TryAdd(name, sensitive);
        }

        return sensitive;
    }

    private static bool Check(List<string> words)
    {
        // "pass" sola o al final (UserPass, db_pass), pero no ByPass.
        if (words.Count > 0 && words[words.Count - 1] == "pass" && (words.Count == 1 || words[words.Count - 2] != "by"))
        {
            return true;
        }

        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];

            if (Words.Contains(word) && !(word == "token" && i > 0 && words[i - 1] == "cancellation"))
            {
                return true;
            }

            if (i > 0 && Pairs.Contains((words[i - 1], word)))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> Split(string name)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        var text = RemoveDiacritics(name);

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (!char.IsLetterOrDigit(c))
            {
                Flush(words, current);
                continue;
            }

            var startsWord = current.Length > 0 && char.IsUpper(c)
                && (char.IsLower(text[i - 1]) || (i + 1 < text.Length && char.IsLower(text[i + 1])));

            if (startsWord)
            {
                Flush(words, current);
            }

            current.Append(char.ToLowerInvariant(c));
        }

        Flush(words, current);

        return words;
    }

    private static void Flush(List<string> words, StringBuilder current)
    {
        if (current.Length > 0)
        {
            words.Add(current.ToString());
            current.Clear();
        }
    }

    private static string RemoveDiacritics(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
