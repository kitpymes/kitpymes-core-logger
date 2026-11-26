// -----------------------------------------------------------------------
// <copyright file="Guard.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger.Serilog;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

/// <summary>
/// Comprobaciones de argumentos (sustituyen a las de Kitpymes.Core.Shared desde la 3.0.0).
/// </summary>
internal static class Guard
{
    /// <summary>
    /// Lanza <see cref="ArgumentNullException"/> si el valor es nulo.
    /// </summary>
    /// <typeparam name="T">Tipo del valor.</typeparam>
    /// <param name="value">Valor.</param>
    /// <param name="name">Nombre del argumento.</param>
    /// <returns>El valor.</returns>
    internal static T NotNull<T>([NotNull] T? value, [CallerArgumentExpression(nameof(value))] string? name = null)
        where T : class
        => value ?? throw new ArgumentNullException(name);

    /// <summary>
    /// Lanza <see cref="ArgumentNullException"/> si el texto es nulo y <see cref="ArgumentException"/> si está vacío o sólo tiene espacios.
    /// </summary>
    /// <param name="value">Texto.</param>
    /// <param name="name">Nombre del argumento.</param>
    /// <returns>El texto.</returns>
    internal static string NotNullOrWhiteSpace([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value;
    }
}
