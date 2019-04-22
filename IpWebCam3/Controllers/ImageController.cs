using IpWebCam3.Helpers;
using IpWebCam3.Helpers.Cache;
using IpWebCam3.Helpers.TimeHelpers;
using IpWebCam3.Models;
using IpWebCam3.Services;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;

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
        private readonly IDateTimeHelper _dateTimeHelper = new DateTimeHelper();

        private DateTime _lastCacheAccess;

        private readonly int _cacheUpdaterExpirationMilliSec;

        // ToDo: Use DI to inject these values
        public ImageController()
        {
            string imageErrorLogoUrl = _configuration.AppRootDir + @"/images/earth_hd_1.jpg";
            string snapshotImagePath = _configuration.AppRootDir + "/outputImages/";

            _connectionInfo = _configuration.CameraConnectionInfo;

            _imageErrorLogoUrl = imageErrorLogoUrl;
            _snapshotImagePath = snapshotImagePath;

            _cacheUpdaterExpirationMilliSec = 600;
            var cacheLifeTimeMilliSec = 2000;
            var cameraFps = 5;

            var imageCache = new ImageCache();
            var imageCacheService = new ImageCachingService(imageCache, Logger, cacheLifeTimeMilliSec, cameraFps);
            var imageProviderService = new ImageProviderService();

            _imageCacheService = imageCacheService;
            _imageProviderService = imageProviderService;
            _lastCacheAccess = _dateTimeHelper.GetDateTimeNow();

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

        private static HttpResponseMessage CreateImageResponseMessage(byte[] imgData)
        {
            if (imgData == null) return null;

            var memoryStream = new MemoryStream(imgData);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(memoryStream) };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

            return response;
        }
        
        // Get image from webCam or from cache
        private byte[] GetImageAsByteArray(string userUtc)
        {
            _imageCacheService.WaitBeforeGettingNextImage(userId: UserId, timeRequested: _lastCacheAccess);

            DateTime requestTime = _dateTimeHelper.GetDateTimeNow();
            byte[] imageByteArray = _imageCacheService.GetImageAsByteArray(userId: UserId, timeRequested: requestTime);

            if (imageByteArray != null)
            {
                _lastCacheAccess = _dateTimeHelper.GetDateTimeNow();

                return imageByteArray;
            }

            if (CanReadImageFromService(UserId, requestTime))
            {
                LogBeforeReadingFromCache(requestTime);

                imageByteArray = GetImageAsByteArrayFromService(userUtc);
                _lastCacheAccess = _dateTimeHelper.GetDateTimeNow();

                LogAfterReadingFromCache();
                
                _imageCacheService.UpdateCachedImage(imageByteArray: imageByteArray,
                                                     userId: UserId,
                                                     timeUpdated: _lastCacheAccess);
                LogReadingDuration(requestTime);
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
                string cgiImagePath = _connectionInfo.Webpage + "?" + userUtc;
                var newConnInfo = new CameraConnectionInfo(_connectionInfo) { Webpage = cgiImagePath };
                image = _imageProviderService.GetImage(newConnInfo);
            }
            catch (Exception ex)
            {
                image = Image.FromFile(_imageErrorLogoUrl);

                Logger.LogError($"{nameof(GetImageAsByteArrayFromService)}: {ex.Message}"); //throw;
            }

            byte[] imageByteArray = ImageHelper.ConvertImageToByteArray(image);
            return imageByteArray;
        }


        private static readonly object LockCanReadImageFromService = new object();
        
        // Ensure that only one client is the *cache updater* which connects to the webCam.
        // The others read images only from cache (Better performance, less traffic)
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
            WriteImageToFile(image);
        }

        private static bool IsTimeToWriteAPicture()
        {
            return
                DateTime.Now.Second == 0 &&
                (
                    DateTime.Now.Minute == 00 ||
                    DateTime.Now.Minute == 15 ||
                    DateTime.Now.Minute == 30 ||
                    DateTime.Now.Minute == 45);
        }

        private void WriteImageToFile(Image image)
        {
            string dateTimeCompact = _dateTimeHelper.GetCurrentTimeAsCompactString();
            string imagePath = _snapshotImagePath + "img_" + dateTimeCompact + ".jpg";
            try
            {
                image.Save(imagePath, ImageFormat.Jpeg);
            }
            catch (Exception e)
            {
                Logger?.LogError($"{nameof(WriteImageToFile)}(): {e.Message}");
            }
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
                                   + ", LastUpdate: " + DateTimeFormatter.ConvertTimeToCompactString(_lastCacheAccess, true);
            Logger?.LogCacheStat(statusMessage);
        }

        private void LogBeforeReadingFromCache(DateTime requestTime)
        {
            Logger?.LogCacheStat(
                "[1]--Start  reading image from source. UserId: " + UserId + ", time: " +
                DateTimeFormatter.ConvertTimeToCompactString(requestTime, true));
        }

        private void LogAfterReadingFromCache()
        {
            Logger?.LogCacheStat(
                "[2]--Finish reading image from source. UserId: " + UserId + ", time: " +
                DateTimeFormatter.ConvertTimeToCompactString(_lastCacheAccess, true));
        }

        private void LogReadingDuration(DateTime requestTime)
        {
            Logger?.LogCacheStat("[3]--Duration for userId " + UserId +
                                    " to retrieve image from source and update cache [milliseconds] = "
                                    + (long)_lastCacheAccess.Subtract(requestTime).TotalMilliseconds);
        }
    }
}
