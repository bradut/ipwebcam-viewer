using IpWebCam3.Helpers.ImageHelpers;
using IpWebCam3.Helpers.Logging;
using IpWebCam3.Helpers.TimeHelpers;
using IpWebCam3.Models;
using System;
using System.Collections.Concurrent;
using System.Drawing;

namespace IpWebCam3.Services.ImageServices
{
    public interface IImageProviderService
    {
        bool CanReadImageFromWebCam(int userId, DateTime requestTime);
        byte[] GetImageAsByteArray(int userId, string userUtc);
    }

    // Logic to decide if the image is read from WebCam or from Cache
    public class ImageProviderService : IImageProviderService
    {
        private readonly IImageFromCacheService _imageFromCacheService;
        private readonly IImageFromWebCamService _imageFromWebCamService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IMiniLogger _logger;

        //private DateTime _lastCacheAccess;
        private readonly int _cacheUpdaterExpirationMilliSec;
        private readonly CacheUpdaterInfo _cacheUpdaterInfo;

        private readonly string _imageErrorLogoUrl;

        private const int MinValueCacheUpdaterLifeTimeMilliSec = 1;

        private readonly ConcurrentDictionary<int, DateTime> _usersLastCacheAccess = new ConcurrentDictionary<int, DateTime>();

        private void UpdateLastCacheAccess(int userId, DateTime requestTime)
        {
            _usersLastCacheAccess.AddOrUpdate(userId, requestTime, (key, oldValue) => requestTime);
        }
        private DateTime GetLastCacheAccess(int userId)
        {
            return _usersLastCacheAccess.GetOrAdd(userId, (key) => _dateTimeProvider.DateTimeNow);
        }

        public ImageProviderService(IImageFromCacheService imageFromCacheService,
            IImageFromWebCamService imageFromWebCamService,
            IDateTimeProvider dateTimeProvider,
            IMiniLogger logger,
            int cacheUpdaterExpirationMilliSec,
            CacheUpdaterInfo cacheUpdaterInfo,
            string imageErrorLogoUrl)
        {
            if (cacheUpdaterExpirationMilliSec < MinValueCacheUpdaterLifeTimeMilliSec) throw new ArgumentException(
                nameof(cacheUpdaterExpirationMilliSec) + $" = {cacheUpdaterExpirationMilliSec} < {MinValueCacheUpdaterLifeTimeMilliSec}");

            _imageFromCacheService = imageFromCacheService ?? throw new ArgumentNullException(nameof(imageFromCacheService));
            _imageFromWebCamService = imageFromWebCamService ?? throw new ArgumentNullException(nameof(imageFromWebCamService));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _logger = logger;
            _cacheUpdaterExpirationMilliSec = cacheUpdaterExpirationMilliSec;
            _cacheUpdaterInfo = cacheUpdaterInfo ?? new CacheUpdaterInfo();
            _imageErrorLogoUrl = imageErrorLogoUrl;
        }

        public byte[] GetImageAsByteArray(int userId, string userUtc)
        {
            (byte[] imageByteArray, DateTime requestTime) = ReadNewImageFromCache(userId);

            if (imageByteArray != null)
            {
                LogNewImageReadFromCache(userId, requestTime);

                UpdateLastCacheAccess(userId, requestTime);

                return imageByteArray;
            }

            bool canReadImageFromWebCam = CanReadImageFromWebCam(userId, requestTime);

            if (canReadImageFromWebCam)
            {
                lock (LockCacheUpdaterOverdue)
                {
                    _cacheUpdaterInfo.Update(userId, requestTime);
                }
            }

            imageByteArray = canReadImageFromWebCam
                ? ReadImageFromWebCam(userId, userUtc, requestTime)
                : ReadCurrentImageFromCache(userId);

            return imageByteArray;
        }

        private (byte[] imageByteArray, DateTime requestTime) ReadNewImageFromCache(int userId)
        {
            _imageFromCacheService.WaitBeforeGettingNextImage(userId: userId, timeRequested: GetLastCacheAccess(userId));

            DateTime requestTime = _dateTimeProvider.DateTimeNow;
            byte[] imageByteArray = _imageFromCacheService.GetNewImageAsByteArray(userId: userId, timeRequested: requestTime);

            return (imageByteArray, requestTime);
        }

        private byte[] ReadCurrentImageFromCache(int userId)
        {
            byte[] imageByteArray;
            {
                imageByteArray = _imageFromCacheService.GetCurrentImageAsByteArray();
                LogReadOldImageFromCache(userId);
            }

            return imageByteArray;
        }

        private byte[] ReadImageFromWebCam(int userId, string userUtc, DateTime requestTime)
        {
            LogBeforeReadingFromProvider(requestTime, userId);

            byte[] imageByteArray = GetImageAsByteArrayFromWebCam(userId, userUtc);
            UpdateLastCacheAccess(userId, _dateTimeProvider.DateTimeNow);

            LogAfterReadingFromProvider(userId);

            _imageFromCacheService.UpdateCachedImage(imageByteArray: imageByteArray,
                userId: userId,
                timeUpdated: GetLastCacheAccess(userId));
            LogDurationReadingFromProvider(requestTime, userId);

            return imageByteArray;
        }

        private byte[] GetImageAsByteArrayFromWebCam(int userId, string userUtc)
        {
            Image image;

            try
            {
                image = _imageFromWebCamService.GetImage(userUtc);
            }
            catch (Exception ex)
            {
                image = Image.FromFile(_imageErrorLogoUrl);

                _logger?.LogError($"{nameof(GetImageAsByteArrayFromWebCam)}: {ex.Message}", userId);
            }

            byte[] imageByteArray = ImageHelper.ConvertImageToByteArray(image);

            return imageByteArray;
        }

        private static readonly object LockCanReadImageFromWebCam = new object();
        // Only one user can be in the role of *cache updater* and has permission to connect to the webCam.
        // The others can only read images from cache (Better performance, less traffic)
        public bool CanReadImageFromWebCam(int userId, DateTime requestTime)
        {
            var canReadFromWebCam = false;

            lock (LockCanReadImageFromWebCam)
            {
                if (_cacheUpdaterInfo.UserId == userId)
                {
                    canReadFromWebCam = true;

                    LogCacheUpdaterRenewed(userId, _cacheUpdaterInfo.LastUpdate, requestTime);
                }
                else if (IsCurrentCacheUpdaterUserOverdue(requestTime))
                {
                    canReadFromWebCam = true;

                    LogCacheUpdaterReplaced(userId, _cacheUpdaterInfo.LastUpdate, requestTime);
                }
                else
                {
                    LogCacheUpdaterRejected(userId, _cacheUpdaterInfo.LastUpdate, requestTime);
                }
            }

            return canReadFromWebCam;
        }

        private static readonly object LockCacheUpdaterOverdue = new object();

        private bool IsCurrentCacheUpdaterUserOverdue(DateTime requestTime)
        {
            lock (LockCacheUpdaterOverdue)
            {
                return
                  requestTime.Subtract(_cacheUpdaterInfo.LastUpdate).TotalMilliseconds >
                        _cacheUpdaterExpirationMilliSec;
            }
        }



        private void LogNewImageReadFromCache(int userId, DateTime requestTime)
        {
            string msg = "From cache (3) = Read image.".PadRight(50) 
                         + "LastUpdate: " +
                         DateTimeFormatter.ConvertTimeToCompactString(_imageFromCacheService.CacheLastUpdate, true)
                         + ".   Request by UserId: " + userId.GetFormattedUserId()
                         + "    lastCacheAccess "
                         + DateTimeFormatter.ConvertTimeToCompactString(GetLastCacheAccess(userId), true)
                         + " => "
                         + DateTimeFormatter.ConvertTimeToCompactString(requestTime, true);

            _logger?.LogCacheStat(msg, userId);
        }

        private void LogCacheUpdaterRejected(int userId, DateTime lastUpdate, DateTime requestTime)
        {
            (string strPrevTime, string strRequestTime) = GetLogDateTimeValuesAsStrings(lastUpdate, requestTime);

            string msg = "CacheUpdater *REJECTED*   UserId: " + _cacheUpdaterInfo.UserId.GetFormattedUserId() + " ,  time: " + strPrevTime;
            string reqBy = " .         Request by UserId: " + userId.GetFormattedUserId() + " , at " + strRequestTime;
            _logger?.LogCacheStat(msg + reqBy, userId);
        }

        private void LogCacheUpdaterReplaced(int userId, DateTime lastUpdate, DateTime requestTime)
        {
            (string strPrevTime, string strRequestTime) = GetLogDateTimeValuesAsStrings(lastUpdate, requestTime);

            string msg = "CacheUpdater *REPLACED*  UserId: " + _cacheUpdaterInfo.UserId.GetFormattedUserId() + " ,  time: " +
                  strPrevTime + " with UserId: " + userId.GetFormattedUserId() + " , time: " + strRequestTime;
            _logger?.LogCacheStat(msg, userId);
        }

        private void LogCacheUpdaterRenewed(int userId, DateTime lastUpdate, DateTime requestTime)
        {
            (string strPrevTime, string strRequestTime) = GetLogDateTimeValuesAsStrings(lastUpdate, requestTime);

            string msg = "CacheUpdater *RENEWED*   UserId: " + userId.GetFormattedUserId() + " , previous time: " + strPrevTime +
                  ", new time: " + strRequestTime;
            _logger?.LogCacheStat(msg, userId);
        }

        private void LogReadOldImageFromCache(int userId)
        {
            string statusMessage = "From cache  . OLD IMAGE until cache is updated"
                                   + " ,  LastUpdate: " + DateTimeFormatter.ConvertTimeToCompactString(GetLastCacheAccess(userId), true) //(_lastCacheAccess, true) //
                                   + "    Request by UserId: " + userId.GetFormattedUserId();
            _logger?.LogCacheStat(statusMessage, userId);
        }

        private void LogBeforeReadingFromProvider(DateTime requestTime, int userId)
        {
            string msg = "[1]--Start reading image from source.".PadRight(84) +
                         " Request by UserId: " + userId.GetFormattedUserId() + " , at " +
                      DateTimeFormatter.ConvertTimeToCompactString(requestTime, true);
            _logger?.LogCacheStat(msg, userId);
        }

        private void LogAfterReadingFromProvider(int userId)
        {
            string msg = "[2]--Finish reading image from source. " +
                         "                                              " +
                         "Request by UserId: " + userId.GetFormattedUserId() + " , at " +
                         DateTimeFormatter.ConvertTimeToCompactString(GetLastCacheAccess(userId), true);
            _logger?.LogCacheStat(msg, userId);
        }

        private void LogDurationReadingFromProvider(DateTime requestTime, int userId)
        {
            DateTime lastCacheAccess = GetLastCacheAccess(userId);
            string msg = "[3]--Duration for        UserId: " + userId.GetFormattedUserId() +
                         " to retrieve image from source and update cache = "
                         + (long)lastCacheAccess.Subtract(requestTime).TotalMilliseconds + " ms";
            _logger?.LogCacheStat(msg, userId);
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