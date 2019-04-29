using System;
using IpWebCam3.Helpers;
using IpWebCam3.Helpers.Cache;
using IpWebCam3.Helpers.TimeHelpers;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using IpWebCam3.Helpers.ImageHelpers;
using IpWebCam3.Services.ImageServices;

namespace IpWebCam3.Controllers
{
    public class ImageController : BaseApiController
    {
        private static readonly ConcurrentDictionary<int, string> ConnectedUsers = new ConcurrentDictionary<int, string>();

        private readonly string _snapshotImagePath;
        private readonly ImageProviderService _imageProviderService;
        private readonly IDateTimeProvider _dateTimeProvider = new DateTimeProvider();

        // ToDo: Use DI to inject these values
        public ImageController()
        {
            string snapshotImagePath = _configuration.SnapShotImagePath;
            
            _snapshotImagePath = snapshotImagePath;

            var cacheUpdaterExpirationMilliSec = 600;
            var cacheLifeTimeMilliSec = 2000;
            var cameraFps = 5;

            DateTime _lastImageAccess = _dateTimeProvider.DateTimeNow;
            var imageCache = new ImageCache();
            var imageFromCacheService = new ImageFromCacheService(imageCache, Logger, cacheLifeTimeMilliSec, cameraFps);
            var imageFromWebCamService = new ImageFromWebCamService(_configuration.CameraConnectionInfo);

            _imageProviderService = new ImageProviderService(imageFromCacheService, 
                                                             imageFromWebCamService, 
                                                             _dateTimeProvider, 
                                                             Logger,
                                                             cacheUpdaterExpirationMilliSec,
                                                             _configuration.ErrorImageLogPath, 
                                                             _lastImageAccess);

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
            byte[] imageByteArray = _imageProviderService.GetImageAsByteArray(UserId, id);

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
    }
}
