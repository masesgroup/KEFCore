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

using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace MASES.EntityFrameworkCore.KNet.Test.Common.Model.Base
{
    [PrimaryKey("BlogId")]
    [Table("Blog", Schema = "Simple")]
    public class Blog
    {
        public int BlogId { get; set; }
        public string Url { get; set; }
        public int Rating { get; set; }

        public List<Post> Posts { get; set; }

        public override string ToString()
        {
            return $"BlogId: {BlogId} Url: {Url} Rating: {Rating}";
        }
    }

    [PrimaryKey("PostId")]
    [Table("Post", Schema = "Simple")]
    public class Post
    {
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }

        public int BlogId { get; set; }
        public Blog Blog { get; set; }

        public override string ToString()
        {
            return $"PostId: {PostId} Title: {Title} Content: {Content} BlogId: {BlogId}";
        }
    }
}

namespace MASES.EntityFrameworkCore.KNet.Test.Common.Model.Evolved
{
    [PrimaryKey("BlogId")]
    [Table("Blog", Schema = "Simple")]
    public class Blog
    {
        public int BlogId { get; set; }
        public int Rating { get; set; }
        public DateTime? Date { get; set; }

        public List<Post> Posts { get; set; }

        public override string ToString()
        {
            return $"BlogId: {BlogId} Rating: {Rating} Date: {Date}";
        }
    }

    [PrimaryKey("PostId")]
    [Table("Post", Schema = "Simple")]
    public class Post
    {
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }

        public int BlogId { get; set; }
        public Blog Blog { get; set; }

        public override string ToString()
        {
            return $"PostId: {PostId} Title: {Title} Content: {Content} BlogId: {BlogId}";
        }
    }
}

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
