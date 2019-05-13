using System;
using IpWebCam3.Models;

namespace IpWebCam3.Services.ImageServices
{
    /// <summary>
    /// Manage the image from cache
    /// https://docs.microsoft.com/en-us/aspnet/web-forms/overview/data-access/caching-data/caching-data-at-application-startup-cs
    /// </summary>
    public class CacheUpdateService
    {
        private static readonly ImageInfo CachedImageInfo = new ImageInfo();
        private static readonly object LockCachedData = new object();

        public void UpdateImage(byte[] imgBytes, int userId, DateTime timeUpdated)
        {
            lock (LockCachedData)
            {
                CachedImageInfo.ImageAsByteArray = imgBytes;
                CachedImageInfo.UpdatedByUserId = userId;
                CachedImageInfo.LastUpdated = timeUpdated;
            }
        }

        public byte[] ImageAsByteArray => CachedImageInfo.ImageAsByteArray;

        public int UserId => CachedImageInfo.UpdatedByUserId;

        public DateTime LastUpdate => CachedImageInfo.LastUpdated;

        public bool HasData => !(CachedImageInfo?.ImageAsByteArray == null || 
                                 CachedImageInfo?.UpdatedByUserId == 0 || 
                                 CachedImageInfo?.LastUpdated == DateTime.MinValue);

        public void Clear()
        {
            lock (LockCachedData)
            {
                CachedImageInfo.ImageAsByteArray = null;
                CachedImageInfo.UpdatedByUserId = 0;
                CachedImageInfo.LastUpdated = DateTime.MinValue;
            }
        }
    }
}