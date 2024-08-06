using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Cftc.Ais.Emailer.Application.DTOs
{
    public class EmailDto
    {
        [Required]
        public string FromName { get; set; }

        [Required]
        [EmailAddress]
        public string FromEmail { get; set; }

        [Required]
        public string ToName { get; set; }

        [Required]
        [EmailAddress]
        public string ToEmail { get; set; }

        [Required]
        public string Subject { get; set; }

        [Required]
        public string Body { get; set; }

        public bool IsHtml { get; set; }

        public List<AttachmentDto> Attachments { get; set; }

        public List<string> Cc { get; set; }

        public List<string> Bcc { get; set; }

        public EmailPriority Priority { get; set; }

        public DateTime? SendAt { get; set; }
    }

    public enum EmailPriority
    {
        Low,
        Normal,
        High
    }
}