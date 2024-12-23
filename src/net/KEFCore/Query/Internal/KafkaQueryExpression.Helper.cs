// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*
*  Copyright 2024 MASES s.r.l.
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

public partial class KafkaQueryExpression
{
    private sealed class ResultEnumerable : IEnumerable<ValueBuffer>
    {
        private readonly Func<ValueBuffer> _getElement;

        public ResultEnumerable(Func<ValueBuffer> getElement)
        {
            _getElement = getElement;
        }

        public IEnumerator<ValueBuffer> GetEnumerator()
            => new ResultEnumerator(_getElement());

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

    private sealed class ProjectionMemberRemappingExpressionVisitor : ExpressionVisitor
    {
        private readonly Expression _queryExpression;
        private readonly Dictionary<ProjectionMember, ProjectionMember> _projectionMemberMappings;

        public ProjectionMemberRemappingExpressionVisitor(
            Expression queryExpression,
            Dictionary<ProjectionMember, ProjectionMember> projectionMemberMappings)
        {
            _queryExpression = queryExpression;
            _projectionMemberMappings = projectionMemberMappings;
        }

        [return: NotNullIfNotNull("expression")]
        public override Expression? Visit(Expression? expression)
        {
            if (expression is ProjectionBindingExpression projectionBindingExpression)
            {
                Check.DebugAssert(
                    projectionBindingExpression.ProjectionMember != null,
                    "ProjectionBindingExpression must have projection member.");

                return new ProjectionBindingExpression(
                    _queryExpression,
                    _projectionMemberMappings[projectionBindingExpression.ProjectionMember],
                    projectionBindingExpression.Type);
            }

            return base.Visit(expression);
        }
    }

    private sealed class ProjectionMemberToIndexConvertingExpressionVisitor : ExpressionVisitor
    {
        private readonly Expression _queryExpression;
        private readonly Dictionary<ProjectionMember, int> _projectionMemberMappings;

        public ProjectionMemberToIndexConvertingExpressionVisitor(
            Expression queryExpression,
            Dictionary<ProjectionMember, int> projectionMemberMappings)
        {
            _queryExpression = queryExpression;
            _projectionMemberMappings = projectionMemberMappings;
        }

        [return: NotNullIfNotNull("expression")]
        public override Expression? Visit(Expression? expression)
        {
            if (expression is ProjectionBindingExpression projectionBindingExpression)
            {
                Check.DebugAssert(
                    projectionBindingExpression.ProjectionMember != null,
                    "ProjectionBindingExpression must have projection member.");

                return new ProjectionBindingExpression(
                    _queryExpression,
                    _projectionMemberMappings[projectionBindingExpression.ProjectionMember],
                    projectionBindingExpression.Type);
            }

            return base.Visit(expression);
        }
    }

    private sealed class ProjectionIndexRemappingExpressionVisitor : ExpressionVisitor
    {
        private readonly Expression _oldExpression;
        private readonly Expression _newExpression;
        private readonly int[] _indexMap;

        public ProjectionIndexRemappingExpressionVisitor(
            Expression oldExpression,
            Expression newExpression,
            int[] indexMap)
        {
            _oldExpression = oldExpression;
            _newExpression = newExpression;
            _indexMap = indexMap;
        }

        [return: NotNullIfNotNull("expression")]
        public override Expression? Visit(Expression? expression)
        {
            if (expression is ProjectionBindingExpression projectionBindingExpression
                && ReferenceEquals(projectionBindingExpression.QueryExpression, _oldExpression))
            {
                Check.DebugAssert(
                    projectionBindingExpression.Index != null,
                    "ProjectionBindingExpression must have index.");

                return new ProjectionBindingExpression(
                    _newExpression,
                    _indexMap[projectionBindingExpression.Index.Value],
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

    private sealed class QueryExpressionReplacingExpressionVisitor : ExpressionVisitor
    {
        private readonly Expression _oldQuery;
        private readonly Expression _newQuery;

        public QueryExpressionReplacingExpressionVisitor(Expression oldQuery, Expression newQuery)
        {
            _oldQuery = oldQuery;
            _newQuery = newQuery;
        }

        [return: NotNullIfNotNull("expression")]
        public override Expression? Visit(Expression? expression)
            => expression is ProjectionBindingExpression projectionBindingExpression
                && ReferenceEquals(projectionBindingExpression.QueryExpression, _oldQuery)
                    ? projectionBindingExpression.ProjectionMember != null
                        ? new ProjectionBindingExpression(
                            _newQuery, projectionBindingExpression.ProjectionMember!, projectionBindingExpression.Type)
                        : new ProjectionBindingExpression(
                            _newQuery, projectionBindingExpression.Index!.Value, projectionBindingExpression.Type)
                    : base.Visit(expression);
    }

    private sealed class CloningExpressionVisitor : ExpressionVisitor
    {
        [return: NotNullIfNotNull("expression")]
        public override Expression? Visit(Expression? expression)
        {
            if (expression is KafkaQueryExpression kafkaQueryExpression)
            {
                var clonedKafkaQueryExpression = new KafkaQueryExpression(
                    kafkaQueryExpression.ServerQueryExpression, kafkaQueryExpression._valueBufferParameter)
                {
                    _groupingParameter = kafkaQueryExpression._groupingParameter,
                    _singleResultMethodInfo = kafkaQueryExpression._singleResultMethodInfo,
                    _scalarServerQuery = kafkaQueryExpression._scalarServerQuery
                };

                clonedKafkaQueryExpression._clientProjections.AddRange(
                    kafkaQueryExpression._clientProjections.Select(e => Visit(e)));
                clonedKafkaQueryExpression._projectionMappingExpressions.AddRange(
                    kafkaQueryExpression._projectionMappingExpressions);
                foreach (var (projectionMember, value) in kafkaQueryExpression._projectionMapping)
                {
                    clonedKafkaQueryExpression._projectionMapping[projectionMember] = Visit(value);
                }

                return clonedKafkaQueryExpression;
            }

            if (expression is EntityProjectionExpression entityProjectionExpression)
            {
                return entityProjectionExpression.Clone();
            }

            return base.Visit(expression);
        }
    }
}
