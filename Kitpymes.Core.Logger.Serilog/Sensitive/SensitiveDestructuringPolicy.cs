// -----------------------------------------------------------------------
// <copyright file="SensitiveDestructuringPolicy.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Logger.Serilog;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using global::Serilog.Core;
using global::Serilog.Events;
using Kitpymes.Core.Logger.Abstractions;

/// <summary>
/// Al desestructurar (<c>{@Objeto}</c>) un tipo con algún miembro marcado con <see cref="SensitiveAttribute"/>, escribe esos miembros como <c>***</c>.
/// </summary>
internal sealed class SensitiveDestructuringPolicy : IDestructuringPolicy
{
    private readonly ConcurrentDictionary<Type, PropertyInfo[]?> types = new ConcurrentDictionary<Type, PropertyInfo[]?>();

    /// <summary>
    /// Desestructura el objeto si su tipo tiene miembros sensibles.
    /// </summary>
    /// <param name="value">Objeto.</param>
    /// <param name="propertyValueFactory">Fábrica de valores de Serilog.</param>
    /// <param name="result">Valor desestructurado.</param>
    /// <returns><c>true</c> si el tipo tiene miembros sensibles.</returns>
    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue result)
    {
        var properties = this.types.GetOrAdd(value.GetType(), SensitiveProperties);

        if (properties is null)
        {
            result = null!;
            return false;
        }

        result = new StructureValue(
            properties.Select(p => new LogEventProperty(p.Name, Value(p, value, propertyValueFactory))).ToList(),
            value.GetType().Name);

        return true;
    }

    private static PropertyInfo[]? SensitiveProperties(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();

        var sensitive = properties.Any(p => Attribute.IsDefined(p, typeof(SensitiveAttribute), true))
            || type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Any(f => Attribute.IsDefined(f, typeof(SensitiveAttribute), true));

        return sensitive ? properties : null;
    }

    private static LogEventPropertyValue Value(PropertyInfo property, object value, ILogEventPropertyValueFactory factory)
    {
        if (Attribute.IsDefined(property, typeof(SensitiveAttribute), true) || HasSensitiveBackingField(property))
        {
            return new ScalarValue(SensitiveDataNames.Mask);
        }

#pragma warning disable CA1031 // Un getter que lanza no debe romper el log: se oculta su valor.
        try
        {
            return factory.CreatePropertyValue(property.GetValue(value), destructureObjects: true);
        }
        catch (Exception)
        {
            return new ScalarValue(SensitiveDataNames.Mask);
        }
#pragma warning restore CA1031
    }

    // [field: Sensitive] en una propiedad automática o en un record posicional deja el atributo en el campo de respaldo.
    private static bool HasSensitiveBackingField(PropertyInfo property) =>
        property.DeclaringType?.GetField($"<{property.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)?.IsDefined(typeof(SensitiveAttribute), true) == true;
}
