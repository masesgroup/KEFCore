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


using MASES.EntityFrameworkCore.KNet.Metadata;
using MASES.EntityFrameworkCore.KNet.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MASES.EntityFrameworkCore.KNet.Test.Common.Model.ReducedComplex
{
    [PrimaryKey("BlogId")]
    [Table("BlogComplex", Schema = "ReducedComplexTest")]
    public class BlogComplex
    {
        public int BlogId { get; set; }
        public string Url { get; set; }
        public int Rating { get; set; }

        public TaxInfo TaxInfo { get; set; }

        [Required]
        public TaxInfoExtended TaxInfoExtended { get; set; }

        public override string ToString()
        {
            return $"BlogId: {BlogId} Url: {Url} Rating: {Rating}";
        }
    }

    [ComplexType]
    [KEFCoreComplexTypeConverter(typeof(TaxInfoExtendedConverter))]
    public class TaxInfoExtended : IEquatable<TaxInfoExtended>
    {
        public TaxInfoExtended()
        {
        }

        public TaxInfoExtended(string str)
        {
            try
            {
                var values = str.Split("_");
                CodeExtended = int.Parse(values[0]);
                PercentageExtended = decimal.Parse(values[1]);
                NestedTaxInfoExtended = new NestedTaxInfoExtended(values[2]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TaxInfoExtendedConverter.ConvertBack failed for input '{str}': {ex}");
            }
        }

        public int CodeExtended { get; set; }
        public decimal PercentageExtended { get; set; }
        [Required]
        public NestedTaxInfoExtended NestedTaxInfoExtended { get; set; }
        public override string ToString()
        {
            return $"{CodeExtended}_{PercentageExtended}_{NestedTaxInfoExtended}";
        }

        public override bool Equals(object obj)
        {
            if (obj is TaxInfoExtended no)
            {
                return Equals(no);
            }
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(CodeExtended, PercentageExtended, NestedTaxInfoExtended);
        }

        public bool Equals(TaxInfoExtended other)
        {
            return other.CodeExtended == CodeExtended
                   && other.PercentageExtended == PercentageExtended
                   && other.NestedTaxInfoExtended.Equals(NestedTaxInfoExtended);
        }
    }

    [ComplexType]
    public class NestedTaxInfoExtended : IEquatable<NestedTaxInfoExtended>
    {
        public NestedTaxInfoExtended()
        {
        }

        public NestedTaxInfoExtended(string str)
        {
            try
            {
                var values = str.Split("$");
                CodeExtended = int.Parse(values[0]);
                PercentageExtended = decimal.Parse(values[1]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TaxInfoExtendedConverter.ConvertBack failed for input '{str}': {ex}");
            }
        }

        public int CodeExtended { get; set; }
        public decimal PercentageExtended { get; set; }
        public override string ToString()
        {
            return $"{CodeExtended}${PercentageExtended}";
        }

        public override bool Equals(object obj)
        {
            if (obj is NestedTaxInfoExtended no)
            {
                return Equals(no);
            }
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(CodeExtended, PercentageExtended);
        }

        public bool Equals(NestedTaxInfoExtended other)
        {
            return other.CodeExtended == CodeExtended
                   && other.PercentageExtended == PercentageExtended;
        }
    }

    [Owned]
    [Table("TaxInfo", Schema = "ReducedComplexTest")]
    public class TaxInfo
    {
        public int TaxInfoId { get; set; }
        public char Code { get; set; }
        public decimal Percentage { get; set; }
    }

    public class TaxInfoExtendedConverter : IComplexTypeConverter
    {
        public IEnumerable<Type> SupportedClrTypes => [typeof(TaxInfoExtended)];

        public IDiagnosticsLogger<DbLoggerCategory.Infrastructure> Logging { get; set; }

        public bool Convert(PreferredConversionType conversionType, ref object input)
        {
            if (input is TaxInfoExtended taxInfoExtended)
            {
                input = taxInfoExtended.ToString();
                return true;
            }
            return false;
        }

        public bool ConvertBack(PreferredConversionType conversionType, ref object input)
        {
            if (input is string str)
            {
                input = new TaxInfoExtended(str);
                return true;
            }
            return false;
        }

        public void Register(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logging)
        {
            Logging = logging;
        }
    }
}
