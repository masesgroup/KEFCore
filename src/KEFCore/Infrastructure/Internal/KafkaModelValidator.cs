/*
*  Copyright 2022 MASES s.r.l.
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

namespace MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;

public class KafkaModelValidator : ModelValidator
{
    public KafkaModelValidator(ModelValidatorDependencies dependencies)
        : base(dependencies)
    {
    }

    public override void Validate(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        base.Validate(model, logger);

        ValidateDefiningQuery(model, logger);
    }

    /// <summary>
    ///     Validates the configuration of defining queries in the model.
    /// </summary>
    /// <param name="model">The model to validate.</param>
    /// <param name="logger">The logger to use.</param>
    protected virtual void ValidateDefiningQuery(
        IModel model,
        IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            if (entityType.GetKafkaQuery() != null)
            {
                if (entityType.BaseType != null)
                {
                    throw new InvalidOperationException(
                        CoreStrings.DerivedTypeDefiningQuery(entityType.DisplayName(), entityType.BaseType.DisplayName()));
                }
            }
        }
    }
}
