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

using Microsoft.EntityFrameworkCore.Query;
using ExpressionExtensions = Microsoft.EntityFrameworkCore.Infrastructure.ExpressionExtensions;

namespace MASES.EntityFrameworkCore.KNet.Query.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KafkaQueryableMethodTranslatingExpressionVisitor : QueryableMethodTranslatingExpressionVisitor
{
    private readonly KafkaExpressionTranslatingExpressionVisitor _expressionTranslator;
    private readonly SharedTypeEntityExpandingExpressionVisitor _weakEntityExpandingExpressionVisitor;
    private readonly KafkaProjectionBindingExpressionVisitor _projectionBindingExpressionVisitor;
    private readonly IModel _model;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public KafkaQueryableMethodTranslatingExpressionVisitor(
        QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
        QueryCompilationContext queryCompilationContext)
        : base(dependencies, queryCompilationContext, subquery: false)
    {
        _expressionTranslator = new KafkaExpressionTranslatingExpressionVisitor(queryCompilationContext, this);
        _weakEntityExpandingExpressionVisitor = new SharedTypeEntityExpandingExpressionVisitor(_expressionTranslator);
        _projectionBindingExpressionVisitor = new KafkaProjectionBindingExpressionVisitor(this, _expressionTranslator);
        _model = queryCompilationContext.Model;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected KafkaQueryableMethodTranslatingExpressionVisitor(
        KafkaQueryableMethodTranslatingExpressionVisitor parentVisitor)
        : base(parentVisitor.Dependencies, parentVisitor.QueryCompilationContext, subquery: true)
    {
        _expressionTranslator = new KafkaExpressionTranslatingExpressionVisitor(QueryCompilationContext, parentVisitor);
        _weakEntityExpandingExpressionVisitor = new SharedTypeEntityExpandingExpressionVisitor(_expressionTranslator);
        _projectionBindingExpressionVisitor = new KafkaProjectionBindingExpressionVisitor(this, _expressionTranslator);
        _model = parentVisitor._model;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
        => new KafkaQueryableMethodTranslatingExpressionVisitor(this);

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
            case GroupByShaperExpression groupByShaperExpression:
                var groupShapedQueryExpression = groupByShaperExpression.GroupingEnumerable;

                return ((KafkaQueryExpression)groupShapedQueryExpression.QueryExpression)
                    .Clone(groupShapedQueryExpression.ShaperExpression);

            case ShapedQueryExpression shapedQueryExpression:
                return ((KafkaQueryExpression)shapedQueryExpression.QueryExpression)
                    .Clone(shapedQueryExpression.ShaperExpression);

            default:
                return base.VisitExtension(extensionExpression);
        }
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
    {
        if (methodCallExpression.Method.IsGenericMethod
            && methodCallExpression.Arguments.Count == 1
            && methodCallExpression.Arguments[0].Type.TryGetSequenceType() != null
            && (string.Equals(methodCallExpression.Method.Name, "AsSplitQuery", StringComparison.Ordinal)
                || string.Equals(methodCallExpression.Method.Name, "AsSingleQuery", StringComparison.Ordinal)))
        {
            return Visit(methodCallExpression.Arguments[0]);
        }

        return base.VisitMethodCall(methodCallExpression);
    }
    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression CreateShapedQueryExpression(IEntityType entityType)
        => CreateShapedQueryExpressionStatic(entityType);

    private static ShapedQueryExpression CreateShapedQueryExpressionStatic(IEntityType entityType)
    {
        var queryExpression = new KafkaQueryExpression(entityType);

        return new ShapedQueryExpression(
            queryExpression,
            new StructuralTypeShaperExpression(
                entityType,
                new ProjectionBindingExpression(
                    queryExpression,
                    new ProjectionMember(),
                    typeof(ValueBuffer)),
                false));
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateAll(ShapedQueryExpression source, LambdaExpression predicate)
    {
        predicate = Expression.Lambda(Expression.Not(predicate.Body), predicate.Parameters);
        var newSource = TranslateWhere(source, predicate);
        if (newSource == null)
        {
            return null;
        }

        source = newSource;

        var kafkaQueryExpression = (KafkaQueryExpression)source.QueryExpression;

        if (source.ShaperExpression is GroupByShaperExpression)
        {
            kafkaQueryExpression.ReplaceProjection(new Dictionary<ProjectionMember, Expression>());
        }

        kafkaQueryExpression.UpdateServerQueryExpression(
            Expression.Not(
                Expression.Call(
                    EnumerableMethods.AnyWithoutPredicate.MakeGenericMethod(kafkaQueryExpression.CurrentParameter.Type),
                    kafkaQueryExpression.ServerQueryExpression)));

        return source.UpdateShaperExpression(Expression.Convert(kafkaQueryExpression.GetSingleScalarProjection(), typeof(bool)));
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateAny(ShapedQueryExpression source, LambdaExpression? predicate)
    {
        if (predicate != null)
        {
            var newSource = TranslateWhere(source, predicate);
            if (newSource == null)
            {
                return null;
            }

            source = newSource;
        }

        var kafkaQueryExpression = (KafkaQueryExpression)source.QueryExpression;

        if (source.ShaperExpression is GroupByShaperExpression)
        {
            kafkaQueryExpression.ReplaceProjection(new Dictionary<ProjectionMember, Expression>());
        }

        kafkaQueryExpression.UpdateServerQueryExpression(
            Expression.Call(
                EnumerableMethods.AnyWithoutPredicate.MakeGenericMethod(kafkaQueryExpression.CurrentParameter.Type),
                kafkaQueryExpression.ServerQueryExpression));

        return source.UpdateShaperExpression(Expression.Convert(kafkaQueryExpression.GetSingleScalarProjection(), typeof(bool)));
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateAverage(
        ShapedQueryExpression source,
        LambdaExpression? selector,
        Type resultType)
        => TranslateScalarAggregate(source, selector, nameof(Enumerable.Average), resultType);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateCast(ShapedQueryExpression source, Type resultType)
        => source.ShaperExpression.Type != resultType
            ? source.UpdateShaperExpression(Expression.Convert(source.ShaperExpression, resultType))
            : source;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateConcat(ShapedQueryExpression source1, ShapedQueryExpression source2)
        => TranslateSetOperation(EnumerableMethods.Concat, source1, source2);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateContains(ShapedQueryExpression source, Expression item)
    {
        var anyLambdaParameter = Expression.Parameter(item.Type, "p");
        var anyLambda = Expression.Lambda(
            ExpressionExtensions.CreateEqualsExpression(anyLambdaParameter, item),
            anyLambdaParameter);

        return TranslateAny(source, anyLambda);
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateCount(ShapedQueryExpression source, LambdaExpression? predicate)
    {
        if (predicate != null)
        {
            var newSource = TranslateWhere(source, predicate);
            if (newSource == null)
            {
                return null;
            }

            source = newSource;
        }

        var kafkaQueryExpression = (KafkaQueryExpression)source.QueryExpression;

        if (source.ShaperExpression is GroupByShaperExpression)
        {
            kafkaQueryExpression.ReplaceProjection(new Dictionary<ProjectionMember, Expression>());
        }

        kafkaQueryExpression.UpdateServerQueryExpression(
            Expression.Call(
                EnumerableMethods.CountWithoutPredicate.MakeGenericMethod(kafkaQueryExpression.CurrentParameter.Type),
                kafkaQueryExpression.ServerQueryExpression));

        return source.UpdateShaperExpression(Expression.Convert(kafkaQueryExpression.GetSingleScalarProjection(), typeof(int)));
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateDefaultIfEmpty(ShapedQueryExpression source, Expression? defaultValue)
    {
        if (defaultValue == null)
        {
            ((KafkaQueryExpression)source.QueryExpression).ApplyDefaultIfEmpty();
            return source.UpdateShaperExpression(MarkShaperNullable(source.ShaperExpression));
        }

        return null;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateDistinct(ShapedQueryExpression source)
    {
        ((KafkaQueryExpression)source.QueryExpression).ApplyDistinct();

        return source;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateElementAtOrDefault(
        ShapedQueryExpression source,
        Expression index,
        bool returnDefault)
        => null;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateExcept(ShapedQueryExpression source1, ShapedQueryExpression source2)
        => TranslateSetOperation(EnumerableMethods.Except, source1, source2);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateFirstOrDefault(
        ShapedQueryExpression source,
        LambdaExpression? predicate,
        Type returnType,
        bool returnDefault)
        => TranslateSingleResultOperator(
            source,
            predicate,
            returnType,
            returnDefault
                ? EnumerableMethods.FirstOrDefaultWithoutPredicate
                : EnumerableMethods.FirstWithoutPredicate);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateGroupBy(
        ShapedQueryExpression source,
        LambdaExpression keySelector,
        LambdaExpression? elementSelector,
        LambdaExpression? resultSelector)
    {
        var remappedKeySelector = RemapLambdaBody(source, keySelector);

        var translatedKey = TranslateGroupingKey(remappedKeySelector);
        if (translatedKey != null)
        {
            var kafkaQueryExpression = (KafkaQueryExpression)source.QueryExpression;
            var defaultElementSelector = elementSelector == null || elementSelector.Body == elementSelector.Parameters[0];
            if (!defaultElementSelector)
            {
                source = TranslateSelect(source, elementSelector!);
            }

            var groupByShaper = kafkaQueryExpression.ApplyGrouping(translatedKey, source.ShaperExpression, defaultElementSelector);

            if (resultSelector == null)
            {
                return source.UpdateShaperExpression(groupByShaper);
            }

            var original1 = resultSelector.Parameters[0];
            var original2 = resultSelector.Parameters[1];

            var newResultSelectorBody = new ReplacingExpressionVisitor(
                new Expression[] { original1, original2 },
                new[] { groupByShaper.KeySelector, groupByShaper }).Visit(resultSelector.Body);

            newResultSelectorBody = ExpandSharedTypeEntities(kafkaQueryExpression, newResultSelectorBody);
            var newShaper = _projectionBindingExpressionVisitor.Translate(kafkaQueryExpression, newResultSelectorBody);

            return source.UpdateShaperExpression(newShaper);
        }

        return null;
    }

    private Expression? TranslateGroupingKey(Expression expression)
    {
        switch (expression)
        {
            case NewExpression newExpression:
                if (newExpression.Arguments.Count == 0)
                {
                    return newExpression;
                }

                var newArguments = new Expression[newExpression.Arguments.Count];
                for (var i = 0; i < newArguments.Length; i++)
                {
                    var key = TranslateGroupingKey(newExpression.Arguments[i]);
                    if (key == null)
                    {
                        return null;
                    }

                    newArguments[i] = key;
                }

                return newExpression.Update(newArguments);

            case MemberInitExpression memberInitExpression:
                var updatedNewExpression = (NewExpression?)TranslateGroupingKey(memberInitExpression.NewExpression);
                if (updatedNewExpression == null)
                {
                    return null;
                }

                var newBindings = new MemberAssignment[memberInitExpression.Bindings.Count];
                for (var i = 0; i < newBindings.Length; i++)
                {
                    var memberAssignment = (MemberAssignment)memberInitExpression.Bindings[i];
                    var visitedExpression = TranslateGroupingKey(memberAssignment.Expression);
                    if (visitedExpression == null)
                    {
                        return null;
                    }

                    newBindings[i] = memberAssignment.Update(visitedExpression);
                }

                return memberInitExpression.Update(updatedNewExpression, newBindings);

            case StructuralTypeShaperExpression { ValueBufferExpression: ProjectionBindingExpression } shaper:
                return shaper;

            default:
                var translation = TranslateExpression(expression);
                if (translation == null)
                {
                    return null;
                }

                return translation.Type == expression.Type
                    ? translation
                    : Expression.Convert(translation, expression.Type);
        }
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateGroupJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
        => null;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateIntersect(ShapedQueryExpression source1, ShapedQueryExpression source2)
        => TranslateSetOperation(EnumerableMethods.Intersect, source1, source2);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
    {
        var (newOuterKeySelector, newInnerKeySelector) = ProcessJoinKeySelector(outer, inner, outerKeySelector, innerKeySelector);

        if (newOuterKeySelector == null
            || newInnerKeySelector == null)
        {
            return null;
        }

        (outerKeySelector, innerKeySelector) = (newOuterKeySelector, newInnerKeySelector);

        var outerShaperExpression = ((KafkaQueryExpression)outer.QueryExpression).AddInnerJoin(
            (KafkaQueryExpression)inner.QueryExpression,
            outerKeySelector,
            innerKeySelector,
            outer.ShaperExpression,
            inner.ShaperExpression);

        outer = outer.UpdateShaperExpression(outerShaperExpression);

        return TranslateTwoParameterSelector(outer, resultSelector);
    }

    private (LambdaExpression? OuterKeySelector, LambdaExpression? InnerKeySelector) ProcessJoinKeySelector(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector)
    {
        var left = RemapLambdaBody(outer, outerKeySelector);
        var right = RemapLambdaBody(inner, innerKeySelector);

        var joinCondition = TranslateExpression(ExpressionExtensions.CreateEqualsExpression(left, right));

        var (outerKeyBody, innerKeyBody) = DecomposeJoinCondition(joinCondition);

        if (outerKeyBody == null
            || innerKeyBody == null)
        {
            return (null, null);
        }

        outerKeySelector = Expression.Lambda(outerKeyBody, ((KafkaQueryExpression)outer.QueryExpression).CurrentParameter);
        innerKeySelector = Expression.Lambda(innerKeyBody, ((KafkaQueryExpression)inner.QueryExpression).CurrentParameter);

        return AlignKeySelectorTypes(outerKeySelector, innerKeySelector);
    }

    private static (Expression?, Expression?) DecomposeJoinCondition(Expression? joinCondition)
    {
        var leftExpressions = new List<Expression>();
        var rightExpressions = new List<Expression>();

        return ProcessJoinCondition(joinCondition, leftExpressions, rightExpressions)
            ? leftExpressions.Count == 1
                ? (leftExpressions[0], rightExpressions[0])
                : (CreateAnonymousObject(leftExpressions), CreateAnonymousObject(rightExpressions))
            : (null, null);

        // Kafka joins need to use AnonymousObject to perform correct key comparison for server side joins
        static Expression CreateAnonymousObject(List<Expression> expressions)
            => Expression.New(
                AnonymousObject.AnonymousObjectCtor,
                Expression.NewArrayInit(
                    typeof(object),
                    expressions.Select(e => Expression.Convert(e, typeof(object)))));
    }

    private static bool ProcessJoinCondition(
        Expression? joinCondition,
        List<Expression> leftExpressions,
        List<Expression> rightExpressions)
    {
        if (joinCondition is BinaryExpression binaryExpression)
        {
            if (binaryExpression.NodeType == ExpressionType.Equal)
            {
                leftExpressions.Add(binaryExpression.Left);
                rightExpressions.Add(binaryExpression.Right);

                return true;
            }

            if (binaryExpression.NodeType == ExpressionType.AndAlso)
            {
                return ProcessJoinCondition(binaryExpression.Left, leftExpressions, rightExpressions)
                    && ProcessJoinCondition(binaryExpression.Right, leftExpressions, rightExpressions);
            }
        }

        if (joinCondition is MethodCallExpression { Method.Name: nameof(object.Equals), Arguments.Count: 2 } methodCallExpression
            && ((methodCallExpression.Method.IsStatic
                    && methodCallExpression.Method.DeclaringType == typeof(object))
                || typeof(ValueComparer).IsAssignableFrom(methodCallExpression.Method.DeclaringType)))
        {
            leftExpressions.Add(methodCallExpression.Arguments[0]);
            rightExpressions.Add(methodCallExpression.Arguments[1]);

            return true;
        }

        return false;
    }

    private static (LambdaExpression OuterKeySelector, LambdaExpression InnerKeySelector)
        AlignKeySelectorTypes(LambdaExpression outerKeySelector, LambdaExpression innerKeySelector)
    {
        if (outerKeySelector.Body.Type != innerKeySelector.Body.Type)
        {
            if (IsConvertedToNullable(outerKeySelector.Body, innerKeySelector.Body))
            {
                innerKeySelector = Expression.Lambda(
                    Expression.Convert(innerKeySelector.Body, outerKeySelector.Body.Type), innerKeySelector.Parameters);
            }
            else if (IsConvertedToNullable(innerKeySelector.Body, outerKeySelector.Body))
            {
                outerKeySelector = Expression.Lambda(
                    Expression.Convert(outerKeySelector.Body, innerKeySelector.Body.Type), outerKeySelector.Parameters);
            }
        }

        return (outerKeySelector, innerKeySelector);

        static bool IsConvertedToNullable(Expression outer, Expression inner)
            => outer.Type.IsNullableType()
                && !inner.Type.IsNullableType()
                && outer.Type.UnwrapNullableType() == inner.Type;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateLastOrDefault(
        ShapedQueryExpression source,
        LambdaExpression? predicate,
        Type returnType,
        bool returnDefault)
        => TranslateSingleResultOperator(
            source,
            predicate,
            returnType,
            returnDefault
                ? EnumerableMethods.LastOrDefaultWithoutPredicate
                : EnumerableMethods.LastWithoutPredicate);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateLeftJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
    {
        var (newOuterKeySelector, newInnerKeySelector) = ProcessJoinKeySelector(outer, inner, outerKeySelector, innerKeySelector);

        if (newOuterKeySelector == null
            || newInnerKeySelector == null)
        {
            return null;
        }

        (outerKeySelector, innerKeySelector) = (newOuterKeySelector, newInnerKeySelector);

        var outerShaperExpression = ((KafkaQueryExpression)outer.QueryExpression).AddLeftJoin(
            (KafkaQueryExpression)inner.QueryExpression,
            outerKeySelector,
            innerKeySelector,
            outer.ShaperExpression,
            inner.ShaperExpression);

        outer = outer.UpdateShaperExpression(outerShaperExpression);

        return TranslateTwoParameterSelector(outer, resultSelector);
    }
#if NET10_0
    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateRightJoin(
        ShapedQueryExpression outer, 
        ShapedQueryExpression inner, 
        LambdaExpression outerKeySelector, 
        LambdaExpression innerKeySelector, 
        LambdaExpression resultSelector)
    {
#warning to be rewieved
        return null;
    }
#endif
    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateLongCount(ShapedQueryExpression source, LambdaExpression? predicate)
    {
        if (predicate != null)
        {
            var newSource = TranslateWhere(source, predicate);
            if (newSource == null)
            {
                return null;
            }

            source = newSource;
        }

        var kafkaQueryExpression = (KafkaQueryExpression)source.QueryExpression;

        if (source.ShaperExpression is GroupByShaperExpression)
        {
            kafkaQueryExpression.ReplaceProjection(new Dictionary<ProjectionMember, Expression>());
        }

        kafkaQueryExpression.UpdateServerQueryExpression(
            Expression.Call(
                EnumerableMethods.LongCountWithoutPredicate.MakeGenericMethod(
                    kafkaQueryExpression.CurrentParameter.Type),
                kafkaQueryExpression.ServerQueryExpression));

        return source.UpdateShaperExpression(Expression.Convert(kafkaQueryExpression.GetSingleScalarProjection(), typeof(long)));
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateMax(
        ShapedQueryExpression source,
        LambdaExpression? selector,
        Type resultType)
        => TranslateScalarAggregate(source, selector, nameof(Enumerable.Max), resultType);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateMin(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
        => TranslateScalarAggregate(source, selector, nameof(Enumerable.Min), resultType);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateOfType(ShapedQueryExpression source, Type resultType)
    {
        if (source.ShaperExpression is StructuralTypeShaperExpression { StructuralType: IEntityType entityType } shaper)
        {
            if (entityType.ClrType == resultType)
            {
                return source;
            }

            var parameterExpression = Expression.Parameter(shaper.Type);
            var predicate = Expression.Lambda(Expression.TypeIs(parameterExpression, resultType), parameterExpression);
            var newSource = TranslateWhere(source, predicate);
            if (newSource == null)
            {
                // EntityType is not part of hierarchy
                return null;
            }

            source = newSource;

            var baseType = entityType.GetAllBaseTypes().SingleOrDefault(et => et.ClrType == resultType);
            if (baseType != null)
            {
                return source.UpdateShaperExpression(shaper.WithType(baseType));
            }

            var derivedType = entityType.GetDerivedTypes().Single(et => et.ClrType == resultType);
            var kafkaQueryExpression = (KafkaQueryExpression)source.QueryExpression;

            var projectionBindingExpression = (ProjectionBindingExpression)shaper.ValueBufferExpression;
            var projectionMember = projectionBindingExpression.ProjectionMember;
            Check.DebugAssert(new ProjectionMember().Equals(projectionMember), "Invalid ProjectionMember when processing OfType");

            var entityProjectionExpression =
                (EntityProjectionExpression)kafkaQueryExpression.GetProjection(projectionBindingExpression);
            kafkaQueryExpression.ReplaceProjection(
                new Dictionary<ProjectionMember, Expression>
                {
                    { projectionMember, entityProjectionExpression.UpdateEntityType(derivedType) }
                });

            return source.UpdateShaperExpression(shaper.WithType(derivedType));
        }

        return null;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateOrderBy(
        ShapedQueryExpression source,
        LambdaExpression keySelector,
        bool ascending)
    {
        var kafkaQueryExpression = (KafkaQueryExpression)source.QueryExpression;

        var newKeySelector = TranslateLambdaExpression(source, keySelector);
        if (newKeySelector == null)
        {
            return null;
        }

        keySelector = newKeySelector;

        var orderBy = ascending ? EnumerableMethods.OrderBy : EnumerableMethods.OrderByDescending;
        kafkaQueryExpression.UpdateServerQueryExpression(
            Expression.Call(
                orderBy.MakeGenericMethod(kafkaQueryExpression.CurrentParameter.Type, keySelector.ReturnType),
                kafkaQueryExpression.ServerQueryExpression,
                keySelector));

        return source;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateReverse(ShapedQueryExpression source)
    {
        var kafkaQueryExpression = (KafkaQueryExpression)source.QueryExpression;

        kafkaQueryExpression.UpdateServerQueryExpression(
            Expression.Call(
                EnumerableMethods.Reverse.MakeGenericMethod(kafkaQueryExpression.CurrentParameter.Type),
                kafkaQueryExpression.ServerQueryExpression));

        return source;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression TranslateSelect(ShapedQueryExpression source, LambdaExpression selector)
    {
        if (selector.Body == selector.Parameters[0])
        {
            return source;
        }

        var newSelectorBody = RemapLambdaBody(source, selector);
        var queryExpression = (KafkaQueryExpression)source.QueryExpression;
        var newShaper = _projectionBindingExpressionVisitor.Translate(queryExpression, newSelectorBody);

        return source.UpdateShaperExpression(newShaper);
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateSelectMany(
        ShapedQueryExpression source,
        LambdaExpression collectionSelector,
        LambdaExpression resultSelector)
    {
        var defaultIfEmpty = new DefaultIfEmptyFindingExpressionVisitor().IsOptional(collectionSelector);
        var collectionSelectorBody = RemapLambdaBody(source, collectionSelector);

        if (Visit(collectionSelectorBody) is ShapedQueryExpression inner)
        {
            var outerShaperExpression = ((KafkaQueryExpression)source.QueryExpression).AddSelectMany(
                (KafkaQueryExpression)inner.QueryExpression, source.ShaperExpression, inner.ShaperExpression, defaultIfEmpty);

            source = source.UpdateShaperExpression(outerShaperExpression);

            return TranslateTwoParameterSelector(source, resultSelector);
        }

        return null;
    }

    private sealed class DefaultIfEmptyFindingExpressionVisitor : ExpressionVisitor
    {
        private bool _defaultIfEmpty;

        public bool IsOptional(LambdaExpression lambdaExpression)
        {
            _defaultIfEmpty = false;

            Visit(lambdaExpression.Body);

            return _defaultIfEmpty;
        }

        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            if (methodCallExpression.Method.IsGenericMethod
                && methodCallExpression.Method.GetGenericMethodDefinition() == QueryableMethods.DefaultIfEmptyWithoutArgument)
            {
                _defaultIfEmpty = true;
            }

            return base.VisitMethodCall(methodCallExpression);
        }
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateSelectMany(ShapedQueryExpression source, LambdaExpression selector)
    {
        var innerParameter = Expression.Parameter(selector.ReturnType.GetSequenceType(), "i");
        var resultSelector = Expression.Lambda(
            innerParameter, Expression.Parameter(source.Type.GetSequenceType()), innerParameter);

        return TranslateSelectMany(source, selector, resultSelector);
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateSingleOrDefault(
        ShapedQueryExpression source,
        LambdaExpression? predicate,
        Type returnType,
        bool returnDefault)
        => TranslateSingleResultOperator(
            source,
            predicate,
            returnType,
            returnDefault
                ? EnumerableMethods.SingleOrDefaultWithoutPredicate
                : EnumerableMethods.SingleWithoutPredicate);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateSkip(ShapedQueryExpression source, Expression count)
    {
        var kafkaQueryExpression = (KafkaQueryExpression)source.QueryExpression;
        var newCount = TranslateExpression(count);
        if (newCount == null)
        {
            return null;
        }

        count = newCount;

        kafkaQueryExpression.UpdateServerQueryExpression(
            Expression.Call(
                EnumerableMethods.Skip.MakeGenericMethod(kafkaQueryExpression.CurrentParameter.Type),
                kafkaQueryExpression.ServerQueryExpression,
                count));

        return source;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateSkipWhile(ShapedQueryExpression source, LambdaExpression predicate)
        => null;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateSum(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
        => TranslateScalarAggregate(source, selector, nameof(Enumerable.Sum), resultType);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateTake(ShapedQueryExpression source, Expression count)
    {
        var kafkaQueryExpression = (KafkaQueryExpression)source.QueryExpression;
        var newCount = TranslateExpression(count);
        if (newCount == null)
        {
            return null;
        }

        count = newCount;

        kafkaQueryExpression.UpdateServerQueryExpression(
            Expression.Call(
                EnumerableMethods.Take.MakeGenericMethod(kafkaQueryExpression.CurrentParameter.Type),
                kafkaQueryExpression.ServerQueryExpression,
                count));

        return source;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateTakeWhile(ShapedQueryExpression source, LambdaExpression predicate)
        => null;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateThenBy(
        ShapedQueryExpression source,
        LambdaExpression keySelector,
        bool ascending)
    {
        var kafkaQueryExpression = (KafkaQueryExpression)source.QueryExpression;
        var newKeySelector = TranslateLambdaExpression(source, keySelector);
        if (newKeySelector == null)
        {
            return null;
        }

        keySelector = newKeySelector;

        kafkaQueryExpression.UpdateServerQueryExpression(
            Expression.Call(
                (ascending ? EnumerableMethods.ThenBy : EnumerableMethods.ThenByDescending)
                .MakeGenericMethod(kafkaQueryExpression.CurrentParameter.Type, keySelector.ReturnType),
                kafkaQueryExpression.ServerQueryExpression,
                keySelector));

        return source;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateUnion(ShapedQueryExpression source1, ShapedQueryExpression source2)
        => TranslateSetOperation(EnumerableMethods.Union, source1, source2);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override ShapedQueryExpression? TranslateWhere(ShapedQueryExpression source, LambdaExpression predicate)
    {
        var kafkaQueryExpression = (KafkaQueryExpression)source.QueryExpression;
        var newPredicate = TranslateLambdaExpression(source, predicate, preserveType: true);
        if (newPredicate == null)
        {
            return null;
        }

        predicate = newPredicate;

        kafkaQueryExpression.UpdateServerQueryExpression(
            Expression.Call(
                EnumerableMethods.Where.MakeGenericMethod(kafkaQueryExpression.CurrentParameter.Type),
                kafkaQueryExpression.ServerQueryExpression,
                predicate));

        return source;
    }

    private Expression? TranslateExpression(Expression expression, bool preserveType = false)
    {
        var translation = _expressionTranslator.Translate(expression);
        if (translation == null && _expressionTranslator.TranslationErrorDetails != null)
        {
            AddTranslationErrorDetails(_expressionTranslator.TranslationErrorDetails);
        }

        if (expression != null
            && translation != null
            && preserveType
            && expression.Type != translation.Type)
        {
            translation = expression.Type == typeof(bool)
                ? Expression.Equal(translation, Expression.Constant(true, translation.Type))
                : Expression.Convert(translation, expression.Type);
        }

        return translation;
    }

    private LambdaExpression? TranslateLambdaExpression(
        ShapedQueryExpression shapedQueryExpression,
        LambdaExpression lambdaExpression,
        bool preserveType = false)
    {
        var lambdaBody = TranslateExpression(RemapLambdaBody(shapedQueryExpression, lambdaExpression), preserveType);

        return lambdaBody != null
            ? Expression.Lambda(
                lambdaBody,
                ((KafkaQueryExpression)shapedQueryExpression.QueryExpression).CurrentParameter)
            : null;
    }

    private Expression RemapLambdaBody(ShapedQueryExpression shapedQueryExpression, LambdaExpression lambdaExpression)
    {
        var lambdaBody = ReplacingExpressionVisitor.Replace(
            lambdaExpression.Parameters.Single(), shapedQueryExpression.ShaperExpression, lambdaExpression.Body);

        return ExpandSharedTypeEntities((KafkaQueryExpression)shapedQueryExpression.QueryExpression, lambdaBody);
    }

    private Expression ExpandSharedTypeEntities(KafkaQueryExpression queryExpression, Expression lambdaBody)
        => _weakEntityExpandingExpressionVisitor.Expand(queryExpression, lambdaBody);

    private sealed class SharedTypeEntityExpandingExpressionVisitor : ExpressionVisitor
    {
        private readonly KafkaExpressionTranslatingExpressionVisitor _expressionTranslator;

        private KafkaQueryExpression _queryExpression;

        public SharedTypeEntityExpandingExpressionVisitor(KafkaExpressionTranslatingExpressionVisitor expressionTranslator)
        {
            _expressionTranslator = expressionTranslator;
            _queryExpression = null!;
        }

        public string? TranslationErrorDetails
            => _expressionTranslator.TranslationErrorDetails;

        public Expression Expand(KafkaQueryExpression queryExpression, Expression lambdaBody)
        {
            _queryExpression = queryExpression;

            return Visit(lambdaBody);
        }

        protected override Expression VisitMember(MemberExpression memberExpression)
        {
            // Fold member access into conditional, i.e. transform
            // (test ? expr1 : expr2).Member -> (test ? expr1.Member : expr2.Member)
            if (memberExpression.Expression is ConditionalExpression cond)
            {
                return Visit(
                    Expression.Condition(
                        cond.Test,
                        Expression.MakeMemberAccess(cond.IfTrue, memberExpression.Member),
                        Expression.MakeMemberAccess(cond.IfFalse, memberExpression.Member)
                    ));
            }

            var innerExpression = Visit(memberExpression.Expression);

            return TryExpand(innerExpression, MemberIdentity.Create(memberExpression.Member))
                ?? memberExpression.Update(innerExpression);
        }

        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            if (methodCallExpression.TryGetEFPropertyArguments(out var source, out var navigationName))
            {
                source = Visit(source);

                return TryExpand(source, MemberIdentity.Create(navigationName))
                    ?? methodCallExpression.Update(null!, new[] { source, methodCallExpression.Arguments[1] });
            }

            return base.VisitMethodCall(methodCallExpression);
        }

        protected override Expression VisitExtension(Expression extensionExpression)
            => extensionExpression is StructuralTypeShaperExpression or ShapedQueryExpression or GroupByShaperExpression
                ? extensionExpression
                : base.VisitExtension(extensionExpression);

        private Expression? TryExpand(Expression? source, MemberIdentity member)
        {
            source = source.UnwrapTypeConversion(out var convertedType);
            if (source is not StructuralTypeShaperExpression shaper)
            {
                return null;
            }

            if (shaper.StructuralType is not IEntityType)
            {
                return null;
            }

            var entityType = (IEntityType)shaper.StructuralType;

            if (convertedType != null)
            {
                entityType = entityType.GetRootType().GetDerivedTypesInclusive()
                    .FirstOrDefault(et => et.ClrType == convertedType);

                if (entityType == null)
                {
                    return null;
                }
            }

            var navigation = member.MemberInfo != null
                ? entityType.FindNavigation(member.MemberInfo)
                : entityType.FindNavigation(member.Name!);

            if (navigation == null)
            {
                return null;
            }

            var targetEntityType = navigation.TargetEntityType;
            if (targetEntityType == null
                || !targetEntityType.IsOwned())
            {
                return null;
            }

            var foreignKey = navigation.ForeignKey;
            if (navigation.IsCollection)
            {
                var innerShapedQuery = CreateShapedQueryExpressionStatic(targetEntityType);
                var innerQueryExpression = (KafkaQueryExpression)innerShapedQuery.QueryExpression;

                var makeNullable = foreignKey.PrincipalKey.Properties
                    .Concat(foreignKey.Properties)
                    .Select(p => p.ClrType)
                    .Any(t => t.IsNullableType());

                var outerKey = shaper.CreateKeyValuesExpression(
                    navigation.IsOnDependent
                        ? foreignKey.Properties
                        : foreignKey.PrincipalKey.Properties,
                    makeNullable);
                var innerKey = innerShapedQuery.ShaperExpression.CreateKeyValuesExpression(
                    navigation.IsOnDependent
                        ? foreignKey.PrincipalKey.Properties
                        : foreignKey.Properties,
                    makeNullable);

                var keyComparison = ExpressionExtensions.CreateEqualsExpression(outerKey, innerKey);

                var predicate = makeNullable
                    ? Expression.AndAlso(
                        outerKey is NewArrayExpression newArrayExpression
                            ? newArrayExpression.Expressions
                                .Select(e =>
                                {
                                    var left = (e as UnaryExpression)?.Operand ?? e;

                                    return Expression.NotEqual(left, Expression.Constant(null, left.Type));
                                })
                                .Aggregate((l, r) => Expression.AndAlso(l, r))
                            : Expression.NotEqual(outerKey, Expression.Constant(null, outerKey.Type)),
                        keyComparison)
                    : keyComparison;

                var correlationPredicate = _expressionTranslator.Translate(predicate)!;
                innerQueryExpression.UpdateServerQueryExpression(
                    Expression.Call(
                        EnumerableMethods.Where.MakeGenericMethod(innerQueryExpression.CurrentParameter.Type),
                        innerQueryExpression.ServerQueryExpression,
                        Expression.Lambda(correlationPredicate, innerQueryExpression.CurrentParameter)));

                return innerShapedQuery;
            }

            var entityProjectionExpression =
                shaper.ValueBufferExpression is ProjectionBindingExpression projectionBindingExpression
                    ? (EntityProjectionExpression)_queryExpression.GetProjection(projectionBindingExpression)
                    : (EntityProjectionExpression)shaper.ValueBufferExpression;
            var innerShaper = entityProjectionExpression.BindNavigation(navigation);
            if (innerShaper == null)
            {
                var innerShapedQuery = CreateShapedQueryExpressionStatic(targetEntityType);
                var innerQueryExpression = (KafkaQueryExpression)innerShapedQuery.QueryExpression;

                var makeNullable = foreignKey.PrincipalKey.Properties
                    .Concat(foreignKey.Properties)
                    .Select(p => p.ClrType)
                    .Any(t => t.IsNullableType());

                var outerKey = shaper.CreateKeyValuesExpression(
                    navigation.IsOnDependent
                        ? foreignKey.Properties
                        : foreignKey.PrincipalKey.Properties,
                    makeNullable);
                var innerKey = innerShapedQuery.ShaperExpression.CreateKeyValuesExpression(
                    navigation.IsOnDependent
                        ? foreignKey.PrincipalKey.Properties
                        : foreignKey.Properties,
                    makeNullable);

                if (foreignKey.Properties.Count > 1)
                {
                    outerKey = Expression.New(AnonymousObject.AnonymousObjectCtor, outerKey);
                    innerKey = Expression.New(AnonymousObject.AnonymousObjectCtor, innerKey);
                }

                var outerKeySelector = Expression.Lambda(_expressionTranslator.Translate(outerKey)!, _queryExpression.CurrentParameter);
                var innerKeySelector = Expression.Lambda(
                    _expressionTranslator.Translate(innerKey)!, innerQueryExpression.CurrentParameter);
                (outerKeySelector, innerKeySelector) = AlignKeySelectorTypes(outerKeySelector, innerKeySelector);
                innerShaper = _queryExpression.AddNavigationToWeakEntityType(
                    entityProjectionExpression, navigation, innerQueryExpression, outerKeySelector, innerKeySelector);
            }

            return innerShaper;
        }
    }

    private ShapedQueryExpression TranslateTwoParameterSelector(ShapedQueryExpression source, LambdaExpression resultSelector)
    {
        var transparentIdentifierType = source.ShaperExpression.Type;
        var transparentIdentifierParameter = Expression.Parameter(transparentIdentifierType);

        Expression original1 = resultSelector.Parameters[0];
        var replacement1 = AccessField(transparentIdentifierType, transparentIdentifierParameter, "Outer");
        Expression original2 = resultSelector.Parameters[1];
        var replacement2 = AccessField(transparentIdentifierType, transparentIdentifierParameter, "Inner");
        var newResultSelector = Expression.Lambda(
            new ReplacingExpressionVisitor(
                    new[] { original1, original2 }, new[] { replacement1, replacement2 })
                .Visit(resultSelector.Body),
            transparentIdentifierParameter);

        return TranslateSelect(source, newResultSelector);
    }

    private static Expression AccessField(
        Type transparentIdentifierType,
        Expression targetExpression,
        string fieldName)
        => Expression.Field(targetExpression, transparentIdentifierType.GetTypeInfo().GetDeclaredField(fieldName)!);

    private ShapedQueryExpression? TranslateScalarAggregate(
        ShapedQueryExpression source,
        LambdaExpression? selector,
        string methodName,
        Type returnType)
    {
        var kafkaQueryExpression = (KafkaQueryExpression)source.QueryExpression;

        selector = selector == null
            || selector.Body == selector.Parameters[0]
                ? Expression.Lambda(
                    kafkaQueryExpression.GetProjection(
                        new ProjectionBindingExpression(
                            kafkaQueryExpression, new ProjectionMember(), returnType)),
                    kafkaQueryExpression.CurrentParameter)
                : TranslateLambdaExpression(source, selector, preserveType: true);

        if (selector == null
            || selector.Body is EntityProjectionExpression)
        {
            return null;
        }

        var method = GetMethod();
        method = method.GetGenericArguments().Length == 2
            ? method.MakeGenericMethod(typeof(ValueBuffer), selector.ReturnType)
            : method.MakeGenericMethod(typeof(ValueBuffer));

        kafkaQueryExpression.UpdateServerQueryExpression(
            Expression.Call(method, kafkaQueryExpression.ServerQueryExpression, selector));

        return source.UpdateShaperExpression(Expression.Convert(kafkaQueryExpression.GetSingleScalarProjection(), returnType));

        MethodInfo GetMethod()
            => methodName switch
            {
                nameof(Enumerable.Average) => EnumerableMethods.GetAverageWithSelector(selector.ReturnType),
                nameof(Enumerable.Max) => EnumerableMethods.GetMaxWithSelector(selector.ReturnType),
                nameof(Enumerable.Min) => EnumerableMethods.GetMinWithSelector(selector.ReturnType),
                nameof(Enumerable.Sum) => EnumerableMethods.GetSumWithSelector(selector.ReturnType),
                _ => throw new InvalidOperationException(CoreStrings.UnknownEntity("Aggregate Operator"))
            };
    }

    private ShapedQueryExpression? TranslateSingleResultOperator(
        ShapedQueryExpression source,
        LambdaExpression? predicate,
        Type returnType,
        MethodInfo method)
    {
        var kafkaQueryExpression = (KafkaQueryExpression)source.QueryExpression;

        if (predicate != null)
        {
            var newSource = TranslateWhere(source, predicate);
            if (newSource == null)
            {
                return null;
            }

            source = newSource;
        }

        kafkaQueryExpression.ConvertToSingleResult(method);

        return source.ShaperExpression.Type != returnType
            ? source.UpdateShaperExpression(Expression.Convert(source.ShaperExpression, returnType))
            : source;
    }

    private static ShapedQueryExpression TranslateSetOperation(
        MethodInfo setOperationMethodInfo,
        ShapedQueryExpression source1,
        ShapedQueryExpression source2)
    {
        var kafkaQueryExpression1 = (KafkaQueryExpression)source1.QueryExpression;
        var kafkaQueryExpression2 = (KafkaQueryExpression)source2.QueryExpression;

        kafkaQueryExpression1.ApplySetOperation(setOperationMethodInfo, kafkaQueryExpression2);

        if (setOperationMethodInfo.Equals(EnumerableMethods.Except))
        {
            return source1;
        }

        var makeNullable = setOperationMethodInfo != EnumerableMethods.Intersect;

        return source1.UpdateShaperExpression(
            MatchShaperNullabilityForSetOperation(
                source1.ShaperExpression, source2.ShaperExpression, makeNullable));
    }

    private static Expression MatchShaperNullabilityForSetOperation(Expression shaper1, Expression shaper2, bool makeNullable)
    {
        switch (shaper1)
        {
            case StructuralTypeShaperExpression entityShaperExpression1
                when shaper2 is StructuralTypeShaperExpression entityShaperExpression2:
                return entityShaperExpression1.IsNullable != entityShaperExpression2.IsNullable
                    ? entityShaperExpression1.MakeNullable(makeNullable)
                    : entityShaperExpression1;

            case NewExpression newExpression1
                when shaper2 is NewExpression newExpression2:
                var newArguments = new Expression[newExpression1.Arguments.Count];
                for (var i = 0; i < newArguments.Length; i++)
                {
                    newArguments[i] = MatchShaperNullabilityForSetOperation(
                        newExpression1.Arguments[i], newExpression2.Arguments[i], makeNullable);
                }

                return newExpression1.Update(newArguments);

            case MemberInitExpression memberInitExpression1
                when shaper2 is MemberInitExpression memberInitExpression2:
                var newExpression = (NewExpression)MatchShaperNullabilityForSetOperation(
                    memberInitExpression1.NewExpression, memberInitExpression2.NewExpression, makeNullable);

                var memberBindings = new MemberBinding[memberInitExpression1.Bindings.Count];
                for (var i = 0; i < memberBindings.Length; i++)
                {
                    var memberAssignment = memberInitExpression1.Bindings[i] as MemberAssignment;
                    Check.DebugAssert(memberAssignment != null, "Only member assignment bindings are supported");

                    memberBindings[i] = memberAssignment.Update(
                        MatchShaperNullabilityForSetOperation(
                            memberAssignment.Expression, ((MemberAssignment)memberInitExpression2.Bindings[i]).Expression,
                            makeNullable));
                }

                return memberInitExpression1.Update(newExpression, memberBindings);

            default:
                return shaper1;
        }
    }
}
