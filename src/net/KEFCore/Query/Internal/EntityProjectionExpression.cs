// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*
*  Copyright 2023 MASES s.r.l.
*
*  Licensed under the Apache License, Version 2.0 (the "License");
*  you may not use this file except in compliance with the License.
*  You may obtain a copy of the License at
*
*  http://www.apache.org/licenses/LICENSE-2.0
*
*  Unless required by applicable law or agreed to in writing, software
*  distributed under the License is distributed on an "AS IS" BASIS,
*  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
*  See the License for the specific language governing permissions and
*  limitations under the License.
*
*  Refer to LICENSE for more information.
*/

using MASES.EntityFrameworkCore.KNet.Internal;

namespace MASES.EntityFrameworkCore.KNet.Query.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class EntityProjectionExpression : Expression, IPrintableExpression
{
    private readonly IReadOnlyDictionary<IProperty, MethodCallExpression> _readExpressionMap;
#if NET8_0_OR_GREATER
    private readonly Dictionary<INavigation, StructuralTypeShaperExpression> _navigationExpressionsCache = new();
#else
    private readonly Dictionary<INavigation, EntityShaperExpression> _navigationExpressionsCache = new();
#endif

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public EntityProjectionExpression(
        IEntityType entityType,
        IReadOnlyDictionary<IProperty, MethodCallExpression> readExpressionMap)
    {
        EntityType = entityType;
        _readExpressionMap = readExpressionMap;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual IEntityType EntityType { get; }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override Type Type
        => EntityType.ClrType;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public sealed override ExpressionType NodeType
        => ExpressionType.Extension;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual EntityProjectionExpression UpdateEntityType(IEntityType derivedType)
    {
        if (!derivedType.GetAllBaseTypes().Contains(EntityType))
        {
            throw new InvalidOperationException(
                KafkaStrings.InvalidDerivedTypeInEntityProjection(
                    derivedType.DisplayName(), EntityType.DisplayName()));
        }

        var readExpressionMap = new Dictionary<IProperty, MethodCallExpression>();
        foreach (var (property, methodCallExpression) in _readExpressionMap)
        {
#if NET8_0_OR_GREATER
            if (derivedType.IsAssignableFrom(property.DeclaringType)
                || property.DeclaringType.IsAssignableFrom(derivedType))
#else
            if (derivedType.IsAssignableFrom(property.DeclaringEntityType)
                || property.DeclaringEntityType.IsAssignableFrom(derivedType))
#endif
            {
                readExpressionMap[property] = methodCallExpression;
            }
        }

        return new EntityProjectionExpression(derivedType, readExpressionMap);
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual MethodCallExpression BindProperty(IProperty property)
    {
        if (property.DeclaringType is not IEntityType entityType)
        {
            if (EntityType != property.DeclaringType)
            {
                throw new InvalidOperationException(
                    KafkaStrings.UnableToBindMemberToEntityProjection("property", property.Name, EntityType.DisplayName()));
            }
        }
        else if (!EntityType.IsAssignableFrom(entityType)
                 && !entityType.IsAssignableFrom(EntityType))
        {
            throw new InvalidOperationException(
                KafkaStrings.UnableToBindMemberToEntityProjection("property", property.Name, EntityType.DisplayName()));
        }

        return _readExpressionMap[property];
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
#if NET8_0_OR_GREATER
    public virtual void AddNavigationBinding(INavigation navigation, StructuralTypeShaperExpression shaper)
#else
    public virtual void AddNavigationBinding(INavigation navigation, EntityShaperExpression shaper)
#endif
    {
        if (!EntityType.IsAssignableFrom(navigation.DeclaringEntityType)
            && !navigation.DeclaringEntityType.IsAssignableFrom(EntityType))
        {
            throw new InvalidOperationException(
                KafkaStrings.UnableToBindMemberToEntityProjection("navigation", navigation.Name, EntityType.DisplayName()));
        }

        _navigationExpressionsCache[navigation] = shaper;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
#if NET8_0_OR_GREATER
    public virtual StructuralTypeShaperExpression? BindNavigation(INavigation navigation)
#else
    public virtual EntityShaperExpression? BindNavigation(INavigation navigation)
#endif
    {
        if (!EntityType.IsAssignableFrom(navigation.DeclaringEntityType)
            && !navigation.DeclaringEntityType.IsAssignableFrom(EntityType))
        {
            throw new InvalidOperationException(
                KafkaStrings.UnableToBindMemberToEntityProjection("navigation", navigation.Name, EntityType.DisplayName()));
        }

        return _navigationExpressionsCache.TryGetValue(navigation, out var expression)
            ? expression
            : null;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual EntityProjectionExpression Clone()
    {
        var readExpressionMap = new Dictionary<IProperty, MethodCallExpression>(_readExpressionMap);
        var entityProjectionExpression = new EntityProjectionExpression(EntityType, readExpressionMap);
        foreach (var (navigation, entityShaperExpression) in _navigationExpressionsCache)
        {
#if NET8_0_OR_GREATER
            entityProjectionExpression._navigationExpressionsCache[navigation] = new StructuralTypeShaperExpression(
                entityShaperExpression.StructuralType,
                ((EntityProjectionExpression)entityShaperExpression.ValueBufferExpression).Clone(),
                entityShaperExpression.IsNullable);
#else
            entityProjectionExpression._navigationExpressionsCache[navigation] = new EntityShaperExpression(
                entityShaperExpression.EntityType,
                ((EntityProjectionExpression)entityShaperExpression.ValueBufferExpression).Clone(),
                entityShaperExpression.IsNullable);
#endif
        }

        return entityProjectionExpression;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    void IPrintableExpression.Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.AppendLine(nameof(EntityProjectionExpression) + ":");
        using (expressionPrinter.Indent())
        {
            foreach (var (property, methodCallExpression) in _readExpressionMap)
            {
                expressionPrinter.Append(property + " -> ");
                expressionPrinter.Visit(methodCallExpression);
                expressionPrinter.AppendLine();
            }
        }
    }
}
