/*
 *  MIT License
 *
 *  Copyright (c) 2022 - 2025 MASES s.r.l.
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in all
 *  copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *  SOFTWARE.
 */

using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace MASES.EntityFrameworkCore.KNet.Test.Model
{
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
