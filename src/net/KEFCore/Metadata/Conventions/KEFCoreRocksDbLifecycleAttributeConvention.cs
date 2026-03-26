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

#nullable enable

using MASES.EntityFrameworkCore.KNet.Extensions;

namespace MASES.EntityFrameworkCore.KNet.Metadata.Conventions;

/// <summary>
/// Reads <see cref="KEFCoreRocksDbLifecycleAttribute"/> from CLR entity types and writes
/// the corresponding KEFCore RocksDB lifecycle metadata into the EF Core model.
/// </summary>
/// <remarks>
/// Fluent API is expected to win over data annotations according to standard EF Core precedence rules.
/// </remarks>
public sealed class KEFCoreRocksDbLifecycleAttributeConvention : IModelFinalizingConvention
{
    /// <inheritdoc />
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (clrType is null)
            {
                continue;
            }

            var attribute = clrType
                .GetCustomAttributes(typeof(KEFCoreRocksDbLifecycleAttribute), true)
                .OfType<KEFCoreRocksDbLifecycleAttribute>()
                .FirstOrDefault();

            if (attribute is null)
            {
                continue;
            }

            if (entityType.GetRocksDbLifecycleHandler() is not null)
            {
                continue;
            }

            if (entityType.GetRocksDbLifecycleHandlerType() is not null)
            {
                continue;
            }

            entityType.SetRocksDbLifecycleHandlerType(attribute.HandlerType, fromDataAnnotation: true);
        }
    }
}