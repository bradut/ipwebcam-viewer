using IpWebCam3.Helpers;
using IpWebCam3.Helpers.Cache;
using IpWebCam3.Helpers.TimeHelpers;
using System;

namespace IpWebCam3.Services
{
    public interface IImageCachingService
    {
        byte[] GetImageAsByteArray();
        byte[] GetImageAsByteArray(int userId, DateTime timeRequested);
        int WaitBeforeGettingNextImage(int userId, DateTime timeRequested);
        void UpdateCachedImage(byte[] imageByteArray, int userId, DateTime timeUpdated);
    }

    /// <summary>
    /// Manage the update and access to the image from cache
    /// </summary>
    public class ImageCachingService : IImageCachingService
    {
        private readonly ImageCache _imageCache;

        // Do not read from cache images older than this duration:
        private readonly int _cacheLifeTimeMilliSec;

        // Do not ask for more than this number of images pe second
        private readonly int _framesPerSecond;

        private readonly MiniLogger _logger;

        private const int MinValueCacheLifeTimeMilliSec = 1;
        private const int MinValueFramesPerSecond = 1;

        public ImageCachingService(ImageCache imageCache,
                                   MiniLogger logger = null,
                                   int cacheLifeTimeMilliSec = 2000,
                                   int framesPerSecond = 5

            )
        {
            _imageCache = imageCache ?? throw new ArgumentNullException(nameof(imageCache));
            if (cacheLifeTimeMilliSec <= MinValueCacheLifeTimeMilliSec) throw new ArgumentException(
                        $"{nameof(cacheLifeTimeMilliSec)} too small: " +
                        $"{cacheLifeTimeMilliSec} < {MinValueCacheLifeTimeMilliSec}");
            if (framesPerSecond <= MinValueFramesPerSecond) throw new ArgumentException(
                        $"{nameof(framesPerSecond)} too small: " +
                        $"{framesPerSecond} < {MinValueFramesPerSecond}");

            _cacheLifeTimeMilliSec = cacheLifeTimeMilliSec;
            _framesPerSecond = framesPerSecond;
            _logger = logger;
        }

        // Read the image from cache regardless of age, etc 
        public byte[] GetImageAsByteArray()
        {
            if (_imageCache != null && _imageCache.HasData)
            {
                return _imageCache.ImageAsByteArray;
            }

            return null;
        }

        public byte[] GetImageAsByteArray(int userId, DateTime timeRequested)
        {
            return CanReturnCachedImage(userId, timeRequested)
                ? _imageCache.ImageAsByteArray
                : null;
        }

        private bool CanReturnCachedImage(int userId, DateTime timeRequested)
        {
            bool canReturnImage = _imageCache != null &&
                                 _imageCache.HasData &&
                                 _imageCache.UserId != userId && // *cache updater* user never reads from cache
                                 IsCachedImageFresh(timeRequested);

            LogCanReturnCachedImage(userId, timeRequested, canReturnImage);

            return canReturnImage;
        }
 
        private bool IsCachedImageFresh(DateTime requestMoment)
        {
            return requestMoment.Subtract(_imageCache.LastUpdate).TotalMilliseconds
                   < _cacheLifeTimeMilliSec;
        }

        // Limit the frequency of cache accesses in order to match camera's FPS rate 
        public int WaitBeforeGettingNextImage(int userId, DateTime timeRequested)
        {
            // A user in the role of *cache updater* never reads from cache
            if (_imageCache.UserId == userId) return 0;

            if (_imageCache.LastUpdate >= timeRequested) return 0;

            int waitTimeMilliSec = TimeToWaitUntilNextImageIsAvailable(timeRequested);
            System.Threading.Thread.Sleep(waitTimeMilliSec);

            return waitTimeMilliSec;
        }

        private int TimeToWaitUntilNextImageIsAvailable(DateTime timeRequested)
        {
            double requestDelay = timeRequested.Subtract(_imageCache.LastUpdate).TotalMilliseconds;
            if (requestDelay <= 0) return 0;

            var timeToWait = 0;

            // grant immediate access to requests occuring whiles reading the first frame
            if (FpsTimeBetweenTwoImagesMilliSec >= requestDelay)
            {
                return timeToWait;
            }

            const int safetyFactor = 100; // expect to read 100 times more frames from a stale cache
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
            if (_imageCache == null) return;

            if (_imageCache.LastUpdate >= timeUpdated) return;

            string logMessage = PrepareUpdateCacheLogMessage(userId);

            _imageCache.UpdateImage(imageByteArray, userId, timeUpdated);

            LogCacheHasBeenUpdated(logMessage);
        }

        public DateTime CacheLastUpdate => _imageCache.LastUpdate;

        private string PrepareUpdateCacheLogMessage(int newUserId)
        {
            var statMessage = "UPDATE cache . ";
            string action = _imageCache.UserId == newUserId ? "Updated" : "Replaced";
            statMessage += $"{action}= userId: " + _imageCache.UserId + ", LastUpdate: "
                           + DateTimeFormatter.ConvertTimeToCompactString(_imageCache.LastUpdate, true) + " ";
            return statMessage;
        }

        private void LogCacheHasBeenUpdated(string statMessage)
        {
            statMessage += " With = userId: " + _imageCache.UserId + ", LastUpdate: "
                           + DateTimeFormatter.ConvertTimeToCompactString(_imageCache.LastUpdate, true) + " ";
            _logger?.LogCacheStat(statMessage);
        }

        private void LogCanReturnCachedImage(int userId, DateTime timeRequested, bool canReturnImage)
        {
            string statusMessage = canReturnImage
                ? CreateMessageWhenCanReturnImage()
                : CreateMessageWhenCanNotReturnImage(userId, timeRequested);

            statusMessage += ". Requested by userId " + userId + ", at " +
                             DateTimeFormatter.ConvertTimeToCompactString(timeRequested, true);
            _logger?.LogCacheStat(statusMessage);
        }

        private string CreateMessageWhenCanNotReturnImage(int userId, DateTime timeRequested)
        {
            string reason = string.Empty;
            if (_imageCache == null) reason += " cache is null ";
            else
            {
                if (!_imageCache.HasData) reason += "cache has no valid data. ";
                if (_imageCache.UserId == userId) reason += " same user. ";
                if (!IsCachedImageFresh(timeRequested)) reason += "image too old. ";
            }

            string statusMessage = "From SOURCE.   Reason = " + reason.Trim();
            return statusMessage;
        }

        private string CreateMessageWhenCanReturnImage()
        {
            return "From cache  . " +
                   " Current = userId: " + _imageCache.UserId
                   + ", LastUpdate: " +
                   DateTimeFormatter.ConvertTimeToCompactString(_imageCache.LastUpdate, true);
        }
    }
}