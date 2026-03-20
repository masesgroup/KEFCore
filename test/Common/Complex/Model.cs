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

namespace MASES.EntityFrameworkCore.KNet.Test.Common.Model.Complex
{
    [PrimaryKey("BlogId")]
    [Table("BlogComplex", Schema = "ComplexTest")]
    public class BlogComplex
    {
        public int BlogId { get; set; }
        public string Url { get; set; }
        public int Rating { get; set; }

        public bool BooleanValue { get; set; }
        public bool? NullableBooleanValue { get; set; }

        // Nested complex type
        public Pricing PricingInfo { get; set; }

        public List<PostComplex> ComplexPosts { get; set; }

        public override string ToString()
        {
            return $"BlogId: {BlogId} Url: {Url} Rating: {Rating} BooleanValue: {BooleanValue} NullableBooleanValue: {NullableBooleanValue}";
        }
    }

    [PrimaryKey("PricingId")]
    [Table("Pricing", Schema = "ComplexTest")]
    public class Pricing
    {
        public int PricingId { get; set; }
        public decimal BasePrice { get; set; }
        public List<Discount> Discounts { get; set; } // Nested collection!
        public TaxInfo Tax { get; set; } // Nested object!
    }

    [PrimaryKey("DiscountId")]
    [Table("Discount", Schema = "ComplexTest")]
    public class Discount
    {
        public int DiscountId { get; set; }
        public string Code { get; set; }
        public decimal Percentage { get; set; }
        public DateRange Validity { get; set; } // Another nested object!
    }

    [PrimaryKey("DateRangeId")]
    [Table("DateRange", Schema = "ComplexTest")]
    public class DateRange
    {
        public int DateRangeId { get; set; }
        public uint CurrentDiff { get; set; }
        public DateTime Min { get; set; }
        public DateTime Max { get; set; }
    }

    [Owned]
    [Table("TaxInfo", Schema = "ComplexTest")]
    public class TaxInfo
    {
        public int TaxInfoId { get; set; }
        public char Code { get; set; }
        public decimal Percentage { get; set; }
        [Required]
        public TaxInfoExtended TaxInfoExtended { get; set; }
        [Required]
        public TaxInfoExtended TaxInfoExtended2 { get; set; }
        public int ExtraValue { get; set; } // used to check index consistency since the method GetIndex of IComplexProperty is zero-based 
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
            return $"{CodeExtended}_{PercentageExtended}";
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
            return HashCode.Combine(CodeExtended, PercentageExtended);
        }

        public bool Equals(TaxInfoExtended other)
        {
            return other.CodeExtended == CodeExtended
                   && other.PercentageExtended == PercentageExtended;
        }
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

    [PrimaryKey("PostId")]
    [Table("PostComplex", Schema = "ComplexTest")]
    public class PostComplex
    {
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public Guid Identifier { get; set; }
        public DateTimeOffset CreationTime { get; set; }

        public int BlogId { get; set; }
        public BlogComplex Blog { get; set; }

        public override string ToString()
        {
            return $"PostId: {PostId} Title: {Title} Content: {Content} BlogId: {Blog?.BlogId} CreationTime: {CreationTime} Identifier: {Identifier}";
        }
    }
}
