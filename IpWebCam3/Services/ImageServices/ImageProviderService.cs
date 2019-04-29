using IpWebCam3.Helpers;
using IpWebCam3.Helpers.ImageHelpers;
using IpWebCam3.Helpers.TimeHelpers;
using IpWebCam3.Models;
using System;
using System.Drawing;

namespace IpWebCam3.Services.ImageServices
{
    // Logic to decide if the image is read from WebCam or from Cache
    public class ImageProviderService
    {
        private readonly IImageCachingService _imageFromCacheService;
        private readonly ImageFromWebCamService _imageFromWebCamService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly MiniLogger _logger;
        private DateTime _lastCacheAccess;
        private readonly int _cacheUpdaterExpirationMilliSec;
        private readonly string _imageErrorLogoUrl;
        private readonly CacheUpdaterInfo _cacheUpdater = new CacheUpdaterInfo();


        public ImageProviderService(
            IImageCachingService imageFromCacheService,
            ImageFromWebCamService imageFromWebCamService,
            IDateTimeProvider dateTimeProvider,
            MiniLogger logger,
            int cacheUpdaterExpirationMilliSec,
            string imageErrorLogoUrl,
            DateTime lastCacheAccess)
        {
            _imageFromCacheService = imageFromCacheService;
            _imageFromWebCamService = imageFromWebCamService;
            _dateTimeProvider = dateTimeProvider;
            _logger = logger;
            _cacheUpdaterExpirationMilliSec = cacheUpdaterExpirationMilliSec;
            _imageErrorLogoUrl = imageErrorLogoUrl;
            _lastCacheAccess = lastCacheAccess;
        }


        // Get image from webCam or from cache
        public byte[] GetImageAsByteArray(int userId, string userUtc)
        {
            (byte[] imageByteArray, DateTime requestTime) = ReadNewImageFromCache(userId);

            if (imageByteArray != null)
            {
                _lastCacheAccess = requestTime;

                return imageByteArray;
            }

            imageByteArray = CanReadImageFromWebCam(userId, requestTime)
                ? ReadImageFromWebCam(userId, userUtc, requestTime)
                : ReadCurrentImageFromCache(userId);

            return imageByteArray;
        }

        private (byte[] imageByteArray, DateTime requestTime) ReadNewImageFromCache(int userId)
        {
            int waitTimeToReadFromCache = _imageFromCacheService.WaitBeforeGettingNextImage(userId: userId, timeRequested: _lastCacheAccess);
            LogRequestAccessToCache(waitTimeToReadFromCache, userId);

            DateTime requestTime = _dateTimeProvider.DateTimeNow;
            byte[] imageByteArray = _imageFromCacheService.GetImageAsByteArray(userId: userId, timeRequested: requestTime);

            return (imageByteArray, requestTime);
        }

        private byte[] ReadCurrentImageFromCache(int userId)
        {
            byte[] imageByteArray;
            {
                imageByteArray = _imageFromCacheService.GetImageAsByteArray();
                LogReadOldImageFromCache(userId);
            }

            return imageByteArray;
        }

        private byte[] ReadImageFromWebCam(int userId, string userUtc, DateTime requestTime)
        {
            LogBeforeReadingFromProvider(requestTime, userId);

            byte[] imageByteArray = GetImageAsByteArrayFromWebCam(userUtc);
            _lastCacheAccess = _dateTimeProvider.DateTimeNow;

            LogAfterReadingFromProvider(userId);

            _imageFromCacheService.UpdateCachedImage(imageByteArray: imageByteArray,
                                                 userId: userId,
                                                 timeUpdated: _lastCacheAccess);
            LogDurationReadingFromProvider(requestTime, userId);
            return imageByteArray;
        }

        private byte[] GetImageAsByteArrayFromWebCam(string userUtc)
        {
            Image image;

            try
            {
                image = _imageFromWebCamService.GetImage(userUtc);
            }
            catch (Exception ex)
            {
                image = Image.FromFile(_imageErrorLogoUrl);

                _logger?.LogError($"{nameof(GetImageAsByteArrayFromWebCam)}: {ex.Message}");
            }

            byte[] imageByteArray = ImageHelper.ConvertImageToByteArray(image);

            return imageByteArray;
        }


        private static readonly object LockCanReadImageFromWebCam = new object();

        // Only one user is in the role of *cache updater* and permitted to connect to the webCam.
        // The others can only read images from cache (Better performance, less traffic)
        public bool CanReadImageFromWebCam(int userId, DateTime requestTime)
        {
            var canReadFromWebCam = false;

            lock (LockCanReadImageFromWebCam)
            {
                if (_cacheUpdater.UserId == userId)
                {
                    canReadFromWebCam = true;

                    LogCacheUpdaterRenewed(userId, _cacheUpdater.LastUpdate, requestTime);
                }
                else if (IsCurrentCacheUpdaterUserOverdue(requestTime))
                {
                    canReadFromWebCam = true;

                    LogCacheUpdaterReplaced(userId, _cacheUpdater.LastUpdate, requestTime);
                }
                else
                {
                    LogCacheUpdaterRejected(userId, _cacheUpdater.LastUpdate, requestTime);
                }

                if (canReadFromWebCam) _cacheUpdater.Update(userId, requestTime);
            }

            return canReadFromWebCam;
        }

        private bool IsCurrentCacheUpdaterUserOverdue(DateTime requestTime)
        {
            return requestTime.Subtract(_cacheUpdater.LastUpdate).TotalMilliseconds >
                                     _cacheUpdaterExpirationMilliSec;
        }



        private void LogCacheUpdaterRejected(int userId, DateTime lastUpdate, DateTime requestTime)
        {
            (string strPrevTime, string strRequestTime) = GetLogDateTimeValuesAsStrings(lastUpdate, requestTime);

            string msg = "Cache Updater = *REJECTED*. userId: " + _cacheUpdater.UserId + ",  time: " + strPrevTime;
            string reqBy = " .Requested by userId " + userId + " , at " + strRequestTime;
            _logger?.LogCacheStat(msg + reqBy);
        }



        private void LogCacheUpdaterReplaced(int userId, DateTime lastUpdate, DateTime requestTime)
        {
            (string strPrevTime, string strRequestTime) = GetLogDateTimeValuesAsStrings(lastUpdate, requestTime);

            string msg = "Cache Updater = *REPLACED* userId: " + _cacheUpdater.UserId + ",  time: " +
                  strPrevTime + " with userId: " + userId + ", time: " + strRequestTime;
            _logger?.LogCacheStat(msg);
        }

        private void LogCacheUpdaterRenewed(int userId, DateTime lastUpdate, DateTime requestTime)
        {
            (string strPrevTime, string strRequestTime) = GetLogDateTimeValuesAsStrings(lastUpdate, requestTime);

            string msg = "Cache Updater = *RENEWED*: userId: " + userId + ", previous time: " + strPrevTime +
                  ", new time: " + strRequestTime;
            _logger?.LogCacheStat(msg);
        }

        private void LogReadOldImageFromCache(int UserId)
        {
            string statusMessage = "From cache  . OLD IMAGE until cache is updated" +
                                   " Requested by = userId: " + UserId
                                   + ", LastUpdate: " + DateTimeFormatter.ConvertTimeToCompactString(_lastCacheAccess, true);
            _logger?.LogCacheStat(statusMessage);
        }

        private void LogBeforeReadingFromProvider(DateTime requestTime, int UserId)
        {
            _logger?.LogCacheStat(
                "[1]--Start  reading image from source. UserId: " + UserId + ", time: " +
                DateTimeFormatter.ConvertTimeToCompactString(requestTime, true));
        }

        private void LogAfterReadingFromProvider(int UserId)
        {
            _logger?.LogCacheStat(
                "[2]--Finish reading image from source. UserId: " + UserId + ", time: " +
                DateTimeFormatter.ConvertTimeToCompactString(_lastCacheAccess, true));
        }

        private void LogDurationReadingFromProvider(DateTime requestTime, int userId)
        {
            _logger?.LogCacheStat("[3]--Duration for userId " + userId +
                                    " to retrieve image from source and update cache [milliseconds] = "
                                    + (long)_lastCacheAccess.Subtract(requestTime).TotalMilliseconds);
        }


        private void LogRequestAccessToCache(int waitTime, int userId)
        {
            var cacheReaderDelay = (int)_lastCacheAccess.Subtract(_imageFromCacheService.CacheLastUpdate).TotalMilliseconds;
            _logger?.LogCacheStat("Request access to cache. " +
                                  "                     " +
                                  $"LastUpdate: {DateTimeFormatter.ConvertTimeToCompactString(_imageFromCacheService.CacheLastUpdate, true)}.  " +
                                  $"lastCacheAccess  {DateTimeFormatter.ConvertTimeToCompactString(_lastCacheAccess, true)}. " +
                                  $"Delta T: {cacheReaderDelay} ms. " +
                                  $"Waited {waitTime} ms. " +
                                  $"userId: {userId}  ");
        }

        private static (string strPrevTime, string strRequestTime) GetLogDateTimeValuesAsStrings(
                                                                                DateTime lastUpdate,
                                                                                DateTime requestTime)
        {
            string strPrevTime = DateTimeFormatter.ConvertTimeToCompactString(lastUpdate, true);
            string strRequestTime = DateTimeFormatter.ConvertTimeToCompactString(requestTime, true);

            return (strPrevTime, strRequestTime);
        }
    }
}