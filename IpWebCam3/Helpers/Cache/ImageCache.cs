using System;
using IpWebCam3.Models;

namespace IpWebCam3.Helpers.Cache
{
    /// <summary>
    /// Manage the image from cache
    /// https://docs.microsoft.com/en-us/aspnet/web-forms/overview/data-access/caching-data/caching-data-at-application-startup-cs
    /// </summary>
    public class ImageCache
    {
        private static readonly ImageInfo CachedImage = new ImageInfo();
        private static readonly object LockCachedData = new object();

        public void UpdateImage(byte[] imgBytes, int userId, DateTime timeUpdated)
        {
            lock (LockCachedData)
            {
                CachedImage.ImageAsByteArray = imgBytes;
                CachedImage.UpdatedByUserId = userId;
                CachedImage.LastUpdated = timeUpdated;
            }
        }

        public byte[] ImageAsByteArray => CachedImage.ImageAsByteArray;

        public int UserId => CachedImage.UpdatedByUserId;

        public DateTime LastUpdate => CachedImage.LastUpdated;

        public bool HasData => !(CachedImage?.ImageAsByteArray == null || 
                                 CachedImage.UpdatedByUserId == 0 || 
                                 CachedImage.LastUpdated == DateTime.MinValue);

        public void Clear()
        {
            lock (LockCachedData)
            {
                CachedImage.ImageAsByteArray = null;
                CachedImage.UpdatedByUserId = 0;
                CachedImage.LastUpdated = DateTime.MinValue;
            }
        }
    }
}