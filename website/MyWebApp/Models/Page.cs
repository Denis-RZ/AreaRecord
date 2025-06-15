using System;
using System.ComponentModel.DataAnnotations;

namespace MyWebApp.Models
{
    public class Page
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(128)]
        public string Slug { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string Title { get; set; } = string.Empty;

        public string? HeaderHtml { get; set; }

        public string BodyHtml { get; set; } = string.Empty;

        public string? FooterHtml { get; set; }

        [MaxLength(300)]
        public string? MetaDescription { get; set; }

        [MaxLength(256)]
        public string? MetaKeywords { get; set; }

        [MaxLength(256)]
        public string? OgTitle { get; set; }

        [MaxLength(300)]
        public string? OgDescription { get; set; }

        public bool IsPublished { get; set; }

        public DateTime? PublishDate { get; set; }

        [MaxLength(128)]
        public string? Category { get; set; }

        [MaxLength(256)]
        public string? Tags { get; set; }

        [MaxLength(256)]
        public string? FeaturedImage { get; set; }
    }
}
