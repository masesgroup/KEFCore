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

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace MASES.EntityFrameworkCore.KNet.Query.Internal;

public partial class KEFCoreQueryExpression
{
    private sealed class ResultEnumerable(Func<ValueBuffer> getElement) : IEnumerable<ValueBuffer>
    {
        public IEnumerator<ValueBuffer> GetEnumerator()
            => new ResultEnumerator(getElement());

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        private sealed class ResultEnumerator : IEnumerator<ValueBuffer>
        {
            private readonly ValueBuffer _value;
            private bool _moved;

            public ResultEnumerator(ValueBuffer value)
            {
                _value = value;
                _moved = _value.IsEmpty;
            }

            public bool MoveNext()
            {
                if (!_moved)
                {
                    _moved = true;

                    return _moved;
                }

                return false;
            }

            public void Reset()
                => _moved = false;

            object IEnumerator.Current
                => Current;

            public ValueBuffer Current
                => !_moved ? ValueBuffer.Empty : _value;

            void IDisposable.Dispose()
            {
            }
        }
    }

    private sealed class ProjectionMemberRemappingExpressionVisitor(
        Expression queryExpression,
        Dictionary<ProjectionMember, ProjectionMember> projectionMemberMappings)
        : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression expression)
        {
            if (expression is ProjectionBindingExpression projectionBindingExpression)
            {
                Check.DebugAssert(
                    projectionBindingExpression.ProjectionMember is not null,
                    "ProjectionBindingExpression must have projection member.");

                return new ProjectionBindingExpression(
                    queryExpression,
                    projectionMemberMappings[projectionBindingExpression.ProjectionMember],
                    projectionBindingExpression.Type);
            }

            return base.VisitExtension(expression);
        }
    }

    private sealed class ProjectionMemberToIndexConvertingExpressionVisitor(
        Expression queryExpression,
        Dictionary<ProjectionMember, int> projectionMemberMappings)
        : ExpressionVisitor
    {
        [return: NotNullIfNotNull(nameof(expression))]
        public override Expression? Visit(Expression? expression)
        {
            if (expression is ProjectionBindingExpression projectionBindingExpression)
            {
                Check.DebugAssert(
                    projectionBindingExpression.ProjectionMember != null,
                    "ProjectionBindingExpression must have projection member.");

                return new ProjectionBindingExpression(
                    queryExpression,
                    projectionMemberMappings[projectionBindingExpression.ProjectionMember],
                    projectionBindingExpression.Type);
            }

            return base.Visit(expression);
        }
    }

    private sealed class ProjectionIndexRemappingExpressionVisitor(
        Expression oldExpression,
        Expression newExpression,
        int[] indexMap)
        : ExpressionVisitor
    {
        [return: NotNullIfNotNull(nameof(expression))]
        public override Expression? Visit(Expression? expression)
        {
            if (expression is ProjectionBindingExpression projectionBindingExpression
                && ReferenceEquals(projectionBindingExpression.QueryExpression, oldExpression))
            {
                Check.DebugAssert(
                    projectionBindingExpression.Index != null,
                    "ProjectionBindingExpression must have index.");

                return new ProjectionBindingExpression(
                    newExpression,
                    indexMap[projectionBindingExpression.Index.Value],
                    projectionBindingExpression.Type);
            }

            return base.Visit(expression);
        }
    }

    private sealed class EntityShaperNullableMarkingExpressionVisitor : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression extensionExpression)
            => extensionExpression is StructuralTypeShaperExpression shaper
                ? shaper.MakeNullable()
                : base.VisitExtension(extensionExpression);
    }

    private sealed class QueryExpressionReplacingExpressionVisitor(Expression oldQuery, Expression newQuery) : ExpressionVisitor
    {
        [return: NotNullIfNotNull(nameof(expression))]
        public override Expression? Visit(Expression? expression)
            => expression is ProjectionBindingExpression projectionBindingExpression
                && ReferenceEquals(projectionBindingExpression.QueryExpression, oldQuery)
                    ? projectionBindingExpression.ProjectionMember != null
                        ? new ProjectionBindingExpression(
                            newQuery, projectionBindingExpression.ProjectionMember!, projectionBindingExpression.Type)
                        : new ProjectionBindingExpression(
                            newQuery, projectionBindingExpression.Index!.Value, projectionBindingExpression.Type)
                    : base.Visit(expression);
    }

    private sealed class CloningExpressionVisitor : ExpressionVisitor
    {
        [return: NotNullIfNotNull(nameof(expression))]
        public override Expression? Visit(Expression? expression)
        {
            if (expression is KEFCoreQueryExpression kefcoreQueryExpression)
            {
                var clonedKEFCoreQueryExpression = new KEFCoreQueryExpression(
                    kefcoreQueryExpression.ServerQueryExpression, kefcoreQueryExpression._valueBufferParameter)
                {
                    _groupingParameter = kefcoreQueryExpression._groupingParameter,
                    _singleResultMethodInfo = kefcoreQueryExpression._singleResultMethodInfo,
                    _scalarServerQuery = kefcoreQueryExpression._scalarServerQuery
                };

                clonedKEFCoreQueryExpression._clientProjections.AddRange(
                    kefcoreQueryExpression._clientProjections.Select(e => Visit(e)));
                clonedKEFCoreQueryExpression._projectionMappingExpressions.AddRange(
                    kefcoreQueryExpression._projectionMappingExpressions);
                foreach (var (projectionMember, value) in kefcoreQueryExpression._projectionMapping)
                {
                    clonedKEFCoreQueryExpression._projectionMapping[projectionMember] = Visit(value);
                }

                return clonedKEFCoreQueryExpression;
            }

            if (expression is EntityProjectionExpression entityProjectionExpression)
            {
                return entityProjectionExpression.Clone();
            }

            return base.Visit(expression);
        }
    }
}
