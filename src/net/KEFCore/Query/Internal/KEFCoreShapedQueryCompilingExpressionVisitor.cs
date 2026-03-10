// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*
*  Copyright (c) 2022-2026 MASES s.r.l.
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

namespace MASES.EntityFrameworkCore.KNet.Query.Internal;

using static Expression;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public partial class KEFCoreShapedQueryCompilingExpressionVisitor(
    ShapedQueryCompilingExpressionVisitorDependencies dependencies,
    QueryCompilationContext queryCompilationContext) : ShapedQueryCompilingExpressionVisitor(dependencies, queryCompilationContext)
{
    private readonly Type _contextType = queryCompilationContext.ContextType;
    private readonly bool _threadSafetyChecksEnabled = dependencies.CoreSingletonOptions.AreThreadSafetyChecksEnabled;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override Expression VisitExtension(Expression extensionExpression)
    {
        return extensionExpression switch
        {
            KEFCoreTableExpression kefcoreTableExpression => Call(
                                TableMethodInfo,
                                QueryCompilationContext.QueryContextParameter,
                                Constant(kefcoreTableExpression.EntityType)),
            KEFCoreSingleKeyTableExpression singleKey => Call(
                                SingleKeyTableMethodInfo,
                                QueryCompilationContext.QueryContextParameter,
                                Constant(singleKey.EntityType),
                                NewArrayInit(
                                    typeof(object),
                                    singleKey.KeyExpressions.Select(e =>
                                        e.Type.IsValueType ? Convert(e, typeof(object)) : e))),
            KEFCoreRangeTableExpression range => Call(
                                RangeTableMethodInfo,
                                QueryCompilationContext.QueryContextParameter,
                                Constant(range.EntityType),
                                BuildNullableObjectArray(range.RangeStart),
                                BuildNullableObjectArray(range.RangeEnd)),
            KEFCoreReverseTableExpression reverse => Call(
                                ReverseTableMethodInfo,
                                QueryCompilationContext.QueryContextParameter,
                                Constant(reverse.EntityType)),
            KEFCoreReverseRangeTableExpression reverseRange => Call(
                                ReverseRangeTableMethodInfo,
                                QueryCompilationContext.QueryContextParameter,
                                Constant(reverseRange.EntityType),
                                BuildNullableObjectArray(reverseRange.RangeStart),
                                BuildNullableObjectArray(reverseRange.RangeEnd)),
            _ => base.VisitExtension(extensionExpression),
        };
    }

    private static Expression BuildNullableObjectArray(IReadOnlyList<Expression>? exprs)
    => exprs == null
        ? Constant(null, typeof(object[]))
        : NewArrayInit(
            typeof(object),
            exprs.Select(e => e.Type.IsValueType ? Convert(e, typeof(object)) : e));

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
    {
        var kefcoreQueryExpression = (KEFCoreQueryExpression)shapedQueryExpression.QueryExpression;
        kefcoreQueryExpression.ApplyProjection();

        var shaperExpression = new ShaperExpressionProcessingExpressionVisitor(
                this, kefcoreQueryExpression, QueryCompilationContext.QueryTrackingBehavior == QueryTrackingBehavior.TrackAll)
            .ProcessShaper(shapedQueryExpression.ShaperExpression);
        var innerEnumerable = Visit(kefcoreQueryExpression.ServerQueryExpression);

        return New(
            typeof(QueryingEnumerable<>).MakeGenericType(shaperExpression.ReturnType).GetConstructors()[0],
            QueryCompilationContext.QueryContextParameter,
            innerEnumerable,
            Constant(shaperExpression.Compile()),
            Constant(_contextType),
            Constant(
                QueryCompilationContext.QueryTrackingBehavior == QueryTrackingBehavior.NoTrackingWithIdentityResolution),
            Constant(_threadSafetyChecksEnabled));
    }

    private static readonly MethodInfo TableMethodInfo
        = typeof(KEFCoreShapedQueryCompilingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(Table))!;

    private static readonly MethodInfo SingleKeyTableMethodInfo
        = typeof(KEFCoreShapedQueryCompilingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(SingleKeyTable))!;

    private static readonly MethodInfo RangeTableMethodInfo
        = typeof(KEFCoreShapedQueryCompilingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(RangeTable))!;

    private static readonly MethodInfo ReverseTableMethodInfo
        = typeof(KEFCoreShapedQueryCompilingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(ReverseTable))!;

    private static readonly MethodInfo ReverseRangeTableMethodInfo
        = typeof(KEFCoreShapedQueryCompilingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(ReverseRangeTable))!;

    private static IEnumerable<ValueBuffer> Table(
        QueryContext queryContext,
        IEntityType entityType)
        => ((KEFCoreQueryContext)queryContext).GetValueBuffers(entityType);

    private static IEnumerable<ValueBuffer> SingleKeyTable(
        QueryContext queryContext,
        IEntityType entityType,
        object?[] keyValues)
        => ((KEFCoreQueryContext)queryContext).GetValueBuffer(entityType, keyValues);

    private static IEnumerable<ValueBuffer> RangeTable(
        QueryContext queryContext,
        IEntityType entityType,
        object?[]? rangeStart,
        object?[]? rangeEnd)
        => ((KEFCoreQueryContext)queryContext).GetValueBuffersRange(entityType, rangeStart, rangeEnd);

    private static IEnumerable<ValueBuffer> ReverseTable(
        QueryContext queryContext,
        IEntityType entityType)
        => ((KEFCoreQueryContext)queryContext).GetValueBuffersReverse(entityType);

    private static IEnumerable<ValueBuffer> ReverseRangeTable(
        QueryContext queryContext,
        IEntityType entityType,
        object?[]? rangeStart,
        object?[]? rangeEnd)
        => ((KEFCoreQueryContext)queryContext).GetValueBuffersReverseRange(entityType, rangeStart, rangeEnd);
}
