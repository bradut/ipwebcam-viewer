using System;

namespace IpWebCam3.Models
{
    /// <summary>
    /// Additional info for a cached image
    /// </summary>
    public class ImageInfo
    {
        public byte[] ImageAsByteArray { get; set; }
        public int UpdatedByUserId { get; set; }
        public DateTime LastUpdated { get; set; }

        public ImageInfo()
        {
            LastUpdated = DateTime.MinValue;
        }
    }
}