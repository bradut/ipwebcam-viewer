﻿using IpWebCam3.Helpers;
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
            _lastImageAccess = _dateTimeHelper.DateTimeNow;

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

            DateTime requestTime = _dateTimeHelper.DateTimeNow;
            byte[] imageByteArray = _imageCacheService.GetImageAsByteArray(userId: UserId, timeRequested: requestTime);

            if (imageByteArray != null)
            {
                _lastImageAccess = _dateTimeHelper.DateTimeNow;

                return imageByteArray;
            }

            if (CanReadImageFromService(UserId, requestTime))
            {
                LogBeforeReadingFromService(requestTime);

                imageByteArray = GetImageAsByteArrayFromService(userUtc);
                _lastImageAccess = _dateTimeHelper.DateTimeNow;

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
            WriteImageToFile(image, _dateTimeHelper.DateTimeNow);
        }

        private bool IsTimeToWriteAPicture()
        {
            return
                _dateTimeHelper.DateTimeNow.Second >= 00 && // provide an interval to avoid
                _dateTimeHelper.DateTimeNow.Second <= 03 // missing time ending in '00' seconds
            &&
            (
                _dateTimeHelper.DateTimeNow.Minute == 00 ||
                _dateTimeHelper.DateTimeNow.Minute == 15 ||
                _dateTimeHelper.DateTimeNow.Minute == 30 ||
                _dateTimeHelper.DateTimeNow.Minute == 45);
        }

        private void WriteImageToFile(Image image, DateTime dateTime, bool roundSecondsToZero = true)
        {
            if (roundSecondsToZero && dateTime.Second != 0)
            {
                dateTime =
                new DateTime(dateTime.Year, dateTime.Month, dateTime.Day,
                             dateTime.Hour, dateTime.Minute, 00);
            }

            string dateTimeCompact = DateTimeFormatter.ConvertTimeToCompactString(dateTime, false);

            ImageHelper.WriteImageToFile(image, _snapshotImagePath, dateTimeCompact, Logger);
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
