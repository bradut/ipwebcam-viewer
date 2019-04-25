using IpWebCam3.Helpers;
using IpWebCam3.Helpers.Cache;
using IpWebCam3.Helpers.TimeHelpers;
using IpWebCam3.Models;
using IpWebCam3.Services;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using IpWebCam3.Helpers.ImageHelpers;

namespace IpWebCam3.Controllers
{
    public class ImageController : BaseApiController
    {
        private static readonly ConcurrentDictionary<int, string> ConnectedUsers = new ConcurrentDictionary<int, string>();

        private readonly CameraConnectionInfo _connectionInfo;

        private readonly string _imageErrorLogoUrl;
        private readonly string _snapshotImagePath;

        private readonly IImageCachingService _imageCacheService;
        private readonly ImageProviderService _imageProviderService;
        private readonly CacheUpdaterInfo _cacheUpdater = new CacheUpdaterInfo();
        private readonly IDateTimeProvider _dateTimeProvider = new DateTimeProvider();

        private DateTime _lastImageAccess;

        private readonly int _cacheUpdaterExpirationMilliSec;

        // ToDo: Use DI to inject these values
        public ImageController()
        {
            string imageErrorLogoPath = _configuration.ErrorImageLogPath;
            string snapshotImagePath = _configuration.SnapShotImagePath;

            _connectionInfo = _configuration.CameraConnectionInfo;

            _imageErrorLogoUrl = imageErrorLogoPath;
            _snapshotImagePath = snapshotImagePath;

            _cacheUpdaterExpirationMilliSec = 600;
            var cacheLifeTimeMilliSec = 2000;
            var cameraFps = 5;

            var imageCache = new ImageCache();
            var imageCacheService = new ImageCachingService(imageCache, Logger, cacheLifeTimeMilliSec, cameraFps);
            var imageProviderService = new ImageProviderService();

            _imageCacheService = imageCacheService;
            _imageProviderService = imageProviderService;
            _lastImageAccess = _dateTimeProvider.DateTimeNow;

            AddConnectedUser();
        }

        private void AddConnectedUser()
        {
            if (ConnectedUsers.ContainsKey(UserId)) return;

            ConnectedUsers.TryAdd(UserId, UserIp);

            LogNewUserHasConnected();
        }



        [HttpGet]
        public HttpResponseMessage Get(string id)
        {
            byte[] imageByteArray = GetImageAsByteArray(id);

            if (imageByteArray == null) return null;

            SaveImageSnapshot(imageByteArray);

            HttpResponseMessage response = CreateImageResponseMessage(imageByteArray);

            return response;
        }

        private static HttpResponseMessage CreateImageResponseMessage(byte[] imageByteArray)
        {
            if (imageByteArray == null) return null;

            var memoryStream = new MemoryStream(imageByteArray);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(memoryStream) };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

            return response;
        }

        // Get image from webCam or from cache
        private byte[] GetImageAsByteArray(string userUtc)
        {
            _imageCacheService.WaitBeforeGettingNextImage(userId: UserId, timeRequested: _lastImageAccess);

            DateTime requestTime = _dateTimeProvider.DateTimeNow;
            byte[] imageByteArray = _imageCacheService.GetImageAsByteArray(userId: UserId, timeRequested: requestTime);

            if (imageByteArray != null)
            {
                _lastImageAccess = _dateTimeProvider.DateTimeNow;

                return imageByteArray;
            }

            if (CanReadImageFromService(UserId, requestTime))
            {
                LogBeforeReadingFromService(requestTime);

                imageByteArray = GetImageAsByteArrayFromService(userUtc);
                _lastImageAccess = _dateTimeProvider.DateTimeNow;

                LogAfterReadingFromService();

                _imageCacheService.UpdateCachedImage(imageByteArray: imageByteArray,
                                                     userId: UserId,
                                                     timeUpdated: _lastImageAccess);
                LogDurationReadingFromService(requestTime);
            }
            else
            {
                imageByteArray = _imageCacheService.GetImageAsByteArray();

                LogReadOldImageFromCache();
            }

            return imageByteArray;
        }

        private byte[] GetImageAsByteArrayFromService(string userUtc)
        {
            Image image;

            try
            {
                image = _imageProviderService.GetImage(_connectionInfo, userUtc);
            }
            catch (Exception ex)
            {
                image = Image.FromFile(_imageErrorLogoUrl);

                Logger.LogError($"{nameof(GetImageAsByteArrayFromService)}: {ex.Message}");
            }

            byte[] imageByteArray = ImageHelper.ConvertImageToByteArray(image);

            return imageByteArray;
        }


        private static readonly object LockCanReadImageFromService = new object();

        // Ensure that only one client is the role *cache updater*, so it has the right to connect to the webCam.
        // The others can only read images from cache (Better performance, less traffic)
        public bool CanReadImageFromService(int userId, DateTime requestTime)
        {
            var canReadFromService = false;

            lock (LockCanReadImageFromService)
            {
                if (_cacheUpdater.UserId == userId)
                {
                    canReadFromService = true;

                    LogCacheUpdaterRenewed(userId, _cacheUpdater.LastUpdate, requestTime);
                }
                else if (IsCurrentCacheUpdaterOverdue(requestTime))
                {
                    canReadFromService = true;

                    LogCacheUpdaterReplaced(userId, _cacheUpdater.LastUpdate, requestTime);
                }
                else
                {
                    LogCacheUpdaterRejected(userId, _cacheUpdater.LastUpdate, requestTime);
                }

                if (canReadFromService) _cacheUpdater.Update(userId, requestTime);
            }

            return canReadFromService;
        }

        private bool IsCurrentCacheUpdaterOverdue(DateTime requestTime)
        {
            return requestTime.Subtract(_cacheUpdater.LastUpdate).TotalMilliseconds >
                                     _cacheUpdaterExpirationMilliSec;
        }

        private void SaveImageSnapshot(byte[] imageAsBytes)
        {
            if (!IsTimeToWriteAPicture())
                return;

            Image image = ImageHelper.ConvertByteArrayToImage(imageAsBytes);
            ImageFileWriter.WriteImageToFile(image, _dateTimeProvider.DateTimeNow, _snapshotImagePath, Logger);
        }

        private bool IsTimeToWriteAPicture()
        {
            return
                _dateTimeProvider.DateTimeNow.Second >= 00 && // provide an interval to avoid
                _dateTimeProvider.DateTimeNow.Second <= 03 // missing time ending in '00' seconds
            &&
            (
                _dateTimeProvider.DateTimeNow.Minute == 00 ||
                _dateTimeProvider.DateTimeNow.Minute == 15 ||
                _dateTimeProvider.DateTimeNow.Minute == 30 ||
                _dateTimeProvider.DateTimeNow.Minute == 45);
        }
        


        private void LogNewUserHasConnected()
        {
            string currentBrowserInfo = HttpContextHelper.GetBrowserInfo(HttpContext.Current);
            Logger?.LogUserIp(UserIp + "," + UserIp + "," + currentBrowserInfo);
        }

        private void LogCacheUpdaterRejected(int userId, DateTime lastUpdate, DateTime requestTime)
        {
            string strPrevTime = DateTimeFormatter.ConvertTimeToCompactString(lastUpdate, true);
            string strRequestTime = DateTimeFormatter.ConvertTimeToCompactString(requestTime, true);
            string msg = "Cache Updater = *REJECTED*. userId: " + _cacheUpdater.UserId + ",  time: " + strPrevTime;
            string reqBy = " .Requested by userId " + userId + ", at " + strRequestTime;
            Logger?.LogCacheStat(msg + reqBy);
        }

        private void LogCacheUpdaterReplaced(int userId, DateTime lastUpdate, DateTime requestTime)
        {
            string strPrevTime = DateTimeFormatter.ConvertTimeToCompactString(lastUpdate, true);
            string strRequestTime = DateTimeFormatter.ConvertTimeToCompactString(requestTime, true);

            string msg = "Cache Updater = *REPLACED* userId: " + _cacheUpdater.UserId + ",  time: " +
                  strPrevTime + " with userId: " + userId + ", time: " + strRequestTime;
            Logger.LogCacheStat(msg);
        }

        private void LogCacheUpdaterRenewed(int userId, DateTime lastUpdate, DateTime requestTime)
        {
            string strPrevTime = DateTimeFormatter.ConvertTimeToCompactString(lastUpdate, true);
            string strRequestTime = DateTimeFormatter.ConvertTimeToCompactString(requestTime, true);

            string msg = "Cache Updater = *RENEWED*: userId: " + userId + ", previous time: " + strPrevTime +
                  ", new time: " + strRequestTime;
            Logger?.LogCacheStat(msg);
        }

        private void LogReadOldImageFromCache()
        {
            string statusMessage = "From cache  . OLD IMAGE until cache is updated" +
                                   " Requested by = userId: " + UserId
                                   + ", LastUpdate: " + DateTimeFormatter.ConvertTimeToCompactString(_lastImageAccess, true);
            Logger?.LogCacheStat(statusMessage);
        }

        private void LogBeforeReadingFromService(DateTime requestTime)
        {
            Logger?.LogCacheStat(
                "[1]--Start  reading image from source. UserId: " + UserId + ", time: " +
                DateTimeFormatter.ConvertTimeToCompactString(requestTime, true));
        }

        private void LogAfterReadingFromService()
        {
            Logger?.LogCacheStat(
                "[2]--Finish reading image from source. UserId: " + UserId + ", time: " +
                DateTimeFormatter.ConvertTimeToCompactString(_lastImageAccess, true));
        }

        private void LogDurationReadingFromService(DateTime requestTime)
        {
            Logger?.LogCacheStat("[3]--Duration for userId " + UserId +
                                    " to retrieve image from source and update cache [milliseconds] = "
                                    + (long)_lastImageAccess.Subtract(requestTime).TotalMilliseconds);
        }
    }
}
