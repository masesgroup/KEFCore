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

public partial class KafkaShapedQueryCompilingExpressionVisitor : ShapedQueryCompilingExpressionVisitor
{
    private readonly Type _contextType;
    private readonly bool _threadSafetyChecksEnabled;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public KafkaShapedQueryCompilingExpressionVisitor(
        ShapedQueryCompilingExpressionVisitorDependencies dependencies,
        QueryCompilationContext queryCompilationContext)
        : base(dependencies, queryCompilationContext)
    {
        _contextType = queryCompilationContext.ContextType;
        _threadSafetyChecksEnabled = dependencies.CoreSingletonOptions.AreThreadSafetyChecksEnabled;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override Expression VisitExtension(Expression extensionExpression)
    {
        switch (extensionExpression)
        {
            case KafkaTableExpression kafkaTableExpression:
                return Call(
                    TableMethodInfo,
                    QueryCompilationContext.QueryContextParameter,
                    Constant(kafkaTableExpression.EntityType));
        }

        return base.VisitExtension(extensionExpression);
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
    {
        var kafkaQueryExpression = (KafkaQueryExpression)shapedQueryExpression.QueryExpression;
        kafkaQueryExpression.ApplyProjection();

        var shaperExpression = new ShaperExpressionProcessingExpressionVisitor(
                this, kafkaQueryExpression, QueryCompilationContext.QueryTrackingBehavior == QueryTrackingBehavior.TrackAll)
            .ProcessShaper(shapedQueryExpression.ShaperExpression);
        var innerEnumerable = Visit(kafkaQueryExpression.ServerQueryExpression);

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
        = typeof(KafkaShapedQueryCompilingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(Table))!;

    private static IEnumerable<ValueBuffer> Table(
        QueryContext queryContext,
        IEntityType entityType)
        => ((KafkaQueryContext)queryContext).GetValueBuffers(entityType);
}
