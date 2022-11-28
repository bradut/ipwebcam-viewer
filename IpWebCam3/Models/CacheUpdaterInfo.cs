using System;

namespace IpWebCam3.Models
{
    /// <summary>
    /// CacheUpdater contains the ID of the USER IN CHARGE with reading images from the WebCam and updating the cache
    /// Update: 2022-11-28: This is actually an implementation of the Leader Election pattern as I found out 3 years later
    /// https://learn.microsoft.com/en-us/azure/architecture/patterns/leader-election
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