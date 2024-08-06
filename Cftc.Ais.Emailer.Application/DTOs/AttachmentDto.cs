using System;

namespace Cftc.Ais.Emailer.Application.DTOs
{
    public class AttachmentDto
    {
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long Size { get; set; }
        public Guid AttachmentId { get; set; }
        public byte[] Content { get; set; }
    }
}