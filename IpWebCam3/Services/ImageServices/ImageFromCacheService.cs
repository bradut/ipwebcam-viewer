using IpWebCam3.Helpers.Logging;
using IpWebCam3.Helpers.TimeHelpers;
using System;

namespace IpWebCam3.Services.ImageServices
{
    public interface IImageFromCacheService
    {
        byte[] GetCurrentImageAsByteArray();
        byte[] GetNewImageAsByteArray(int userId, DateTime timeRequested);
        (int waitTimeMilliSec, string reason) WaitBeforeGettingNextImage(int userId, DateTime timeRequested);
        void UpdateCachedImage(byte[] imageByteArray, int userId, DateTime timeUpdated);
        DateTime CacheLastUpdate { get; }
    }

    /// <summary>
    /// Logic for updating and reading the image from cache
    /// </summary>
    public class ImageFromCacheService : IImageFromCacheService
    {
        private CacheUpdateService _cacheUpdateService;

        // Do not read from cache images older than this duration:
        private readonly int _cacheLifeTimeMilliSec;

        // Do not ask for more than this number of images pe second
        private readonly int _framesPerSecond;

        private readonly IMiniLogger _logger;

        private const int MinValueCacheLifeTimeMilliSec = 1;
        private const int MinValueFramesPerSecond = 1;

        public ImageFromCacheService(CacheUpdateService cacheUpdateService,
                                     IMiniLogger logger = null,
                                     int cacheLifeTimeMilliSec = 2001,
                                     int framesPerSecond = 5)
        {
            ValidateInputData(cacheUpdateService, cacheLifeTimeMilliSec, framesPerSecond);

            _cacheLifeTimeMilliSec = cacheLifeTimeMilliSec;
            _framesPerSecond = framesPerSecond;
            _logger = logger;
        }

        private void ValidateInputData(CacheUpdateService cacheUpdateService, int cacheLifeTimeMilliSec, int framesPerSecond)
        {
            _cacheUpdateService = cacheUpdateService ?? throw new ArgumentNullException(nameof(cacheUpdateService));

            if (cacheLifeTimeMilliSec < MinValueCacheLifeTimeMilliSec)
                throw new ArgumentException(
                    $"{nameof(cacheLifeTimeMilliSec)} too small: " +
                    $"{cacheLifeTimeMilliSec} < {MinValueCacheLifeTimeMilliSec}");

            if (framesPerSecond < MinValueFramesPerSecond)
                throw new ArgumentException(
                    $"{nameof(framesPerSecond)} too small: " +
                    $"{framesPerSecond} < {MinValueFramesPerSecond}");
        }


        // Read the image from cache regardless of age, updater user id, etc 
        public byte[] GetCurrentImageAsByteArray()
        {
            if (_cacheUpdateService != null && _cacheUpdateService.HasData)
            {
                return _cacheUpdateService.ImageAsByteArray;
            }

            return null;
        }

        public byte[] GetNewImageAsByteArray(int userId, DateTime timeRequested)
        {
            return CanReturnCachedImage(userId, timeRequested)
                ? _cacheUpdateService.ImageAsByteArray
                : null;
        }

        private bool CanReturnCachedImage(int userId, DateTime timeRequested)
        {
            bool canReturnImage = _cacheUpdateService != null &&
                                  _cacheUpdateService.HasData &&
                                  !IsUserCacheUpdater(userId) && // CacheUpdater user never reads from cache
                                  IsCachedImageFresh(timeRequested);

            LogCanReturnCachedImage(userId, timeRequested, canReturnImage);

            return canReturnImage;
        }

        private bool IsCachedImageFresh(DateTime requestMoment)
        {
            return requestMoment.Subtract(_cacheUpdateService.LastUpdate).TotalMilliseconds
                   < _cacheLifeTimeMilliSec;
        }


        private bool IsUserCacheUpdater(int userId)
        {
            return _cacheUpdateService.UserId == userId;
        }



        // Limit the frequency of cache accesses in order to match camera's FPS rate 
        public (int waitTimeMilliSec, string reason) WaitBeforeGettingNextImage(int userId, DateTime timeRequested)
        {
            DateTime lastCacheUpdate = _cacheUpdateService.LastUpdate;
            int waitTimeMilliSec;
            string reason = string.Empty;


            if (IsUserCacheUpdater(userId))
            {
                // CacheUpdater user never reads from cache
                waitTimeMilliSec = 0;
                reason = "CacheUpdater";
            }
            else if (lastCacheUpdate >= timeRequested)
            {
                waitTimeMilliSec = 0;
                reason = "Old request";
            }
            else
            {
                waitTimeMilliSec = TimeToWaitUntilNextImageIsAvailable(timeRequested, lastCacheUpdate);
            }
            
            LogRequestAccessToCache(userId, timeRequested, lastCacheUpdate, waitTimeMilliSec, reason);

            System.Threading.Thread.Sleep(waitTimeMilliSec);

            return (waitTimeMilliSec, reason);
        }



        private int TimeToWaitUntilNextImageIsAvailable(DateTime timeRequested, DateTime lastCacheUpdate)
        {
            double requestDelay = timeRequested.Subtract(lastCacheUpdate).TotalMilliseconds;
            if (requestDelay <= 0) return 0;

            var timeToWait = 0;

            const int safetyFactor = 100; // expect to read 100 times more frames from a stale cache before returning wait time = 0
            int maxFramesToReadFromSameCache = _framesPerSecond * _cacheLifeTimeMilliSec / 1000 * safetyFactor;

            for (var frameNumber = 1; frameNumber <= maxFramesToReadFromSameCache; frameNumber++)
            {
                timeToWait = FpsTimeBetweenTwoImagesMilliSec * frameNumber - (int)requestDelay;
                if (timeToWait >= 0) break;
            }

            return timeToWait > 0 ? timeToWait : 0;
        }

        private int FpsTimeBetweenTwoImagesMilliSec
        {
            get
            {
                return 1 * 1000 / _framesPerSecond;
            }
        }

        public void UpdateCachedImage(byte[] imageByteArray, int userId, DateTime timeUpdated)
        {
            if (_cacheUpdateService == null)
                return;

            DateTime lastCacheUpdate = _cacheUpdateService.LastUpdate;
            if (lastCacheUpdate >= timeUpdated)
                return;

            string logMessage = PrepareUpdateCacheLogMessage(userId, lastCacheUpdate);
            _cacheUpdateService.UpdateImage(imageByteArray, userId, timeUpdated);

            LogCacheHasBeenUpdated(logMessage);
        }

        public DateTime CacheLastUpdate => _cacheUpdateService.LastUpdate;


        private void LogRequestAccessToCache(int userId, DateTime timeRequested, DateTime lastCacheUpdate,
                                             int waitTimeMilliSec, string reason)
        {
            var requestDelay = (int)timeRequested.Subtract(lastCacheUpdate).TotalMilliseconds;
            string msg = "From cache (1) = Request access and WAIT".PadRight(50) +
                         "LastUpdate: " +
                         DateTimeFormatter.ConvertTimeToCompactString(lastCacheUpdate, true) +
                         $"    Request by userId: {userId.GetFormattedUserId()} " +
                         "  at " +
                         DateTimeFormatter.ConvertTimeToCompactString(timeRequested, true) +
                         $"  Delta T = {requestDelay} ms." +
                         $"  Time to wait = {waitTimeMilliSec} ms  {reason}";
            _logger?.LogCacheStat(msg, userId);
        }

        private string PrepareUpdateCacheLogMessage(int newUserId, DateTime lastCacheUpdate)
        {
            var statMessage = "UPDATE cache . ";
            string action = _cacheUpdateService.UserId == newUserId ? "Updated " : "Replaced";
            statMessage += $"{action}= userId: " + _cacheUpdateService.UserId.GetFormattedUserId();
            statMessage = statMessage.PadRight(50);
            statMessage += "LastUpdate: "
                           + DateTimeFormatter.ConvertTimeToCompactString(lastCacheUpdate, true) + " ";
            return statMessage;
        }

        private void LogCacheHasBeenUpdated(string statMessage)
        {
            statMessage += " With = userId: " + _cacheUpdateService.UserId.GetFormattedUserId() + " , LastUpdate: "
                           + DateTimeFormatter.ConvertTimeToCompactString(_cacheUpdateService.LastUpdate, true) + " ";
            _logger?.LogCacheStat(statMessage, _cacheUpdateService.UserId);
        }

        private void LogCanReturnCachedImage(int userId, DateTime timeRequested, bool canReturnImage)
        {
            string statusMessage = canReturnImage
                ? CreateMessageWhenCanReturnImage()
                : CreateMessageWhenCanNotReturnImage(userId, timeRequested);

            statusMessage += "Request by userId: " + userId.GetFormattedUserId() + " , at " +
                             DateTimeFormatter.ConvertTimeToCompactString(timeRequested, true);
            _logger?.LogCacheStat(statusMessage, userId);
        }

        private string CreateMessageWhenCanNotReturnImage(int userId, DateTime timeRequested)
        {
            string reason = string.Empty;
            if (_cacheUpdateService == null) reason += " cache is null ";
            else
            {
                if (!_cacheUpdateService.HasData) reason += "cache has no valid data. ";
                if (_cacheUpdateService.UserId == userId) reason += " same user. ";
                if (!IsCachedImageFresh(timeRequested)) reason += "image too old. ";
            }
            string statusMessage = "From SOURCE.   Reason = " + reason.Trim();

            return statusMessage.PadRight(85);
        }

        private string CreateMessageWhenCanReturnImage()
        {
            string statusMessage = ("From cache (2)." + // Granted access to cache
                   "Current = userId: " + _cacheUpdateService.UserId.GetFormattedUserId()).PadRight(50)
                   + "LastUpdate: " +
                   DateTimeFormatter.ConvertTimeToCompactString(_cacheUpdateService.LastUpdate, true);

            return statusMessage.PadRight(85);
        }
    }
}