// -----------------------------------------------------------------------
// <copyright file="SensitiveAttribute.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger.Abstractions;

using System;

/// <summary>
/// Marca una propiedad o un campo cuyo valor nunca debe escribirse en el log.
/// </summary>
/// <remarks>
/// <para>El adaptador de Serilog lo respeta al desestructurar un objeto (<c>{@Objeto}</c>): el valor sale como <c>***</c>.</para>
/// <para>Las propiedades con nombre de credencial (ver <see cref="SensitiveDataNames"/>) se ocultan aunque no lleven el atributo.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class SensitiveAttribute : Attribute
{
}
