using System;

namespace IpWebCam3.Models
{
    /// <summary>
    /// CacheUpdater contains the ID of the user in charge with reading images from the WebCam and updating the cache
    /// </summary>
    public class CacheUpdaterInfo
    {
        private static int _userId;
        public int UserId => _userId;

        private static DateTime _lastUpdate = DateTime.MinValue;
        public DateTime LastUpdate => _lastUpdate;

        private static readonly object LockUpdate = new object();

        public void Update(int userId, DateTime lastUpdate)
        {
            lock (LockUpdate)
            {
                _userId = userId;
                _lastUpdate = lastUpdate;
            }
        }
    }
}