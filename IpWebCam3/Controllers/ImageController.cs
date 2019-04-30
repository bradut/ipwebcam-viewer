using IpWebCam3.Helpers;
using IpWebCam3.Helpers.Cache;
using IpWebCam3.Helpers.ImageHelpers;
using IpWebCam3.Services.ImageServices;
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

        private readonly string _snapshotImagePath;
        private readonly ImageProviderService _imageProviderService;
        
        // ToDo: Use DI to inject these values
        public ImageController()
        {
            _snapshotImagePath = _configuration.SnapShotImagePath;
            
            var imageCache = new ImageCache();
            var cacheLifeTimeMilliSec = 2000;
            var cameraFps = 5;
            var imageFromCacheService = new ImageFromCacheService(imageCache, Logger, cacheLifeTimeMilliSec, cameraFps);

            var imageFromWebCamService = new ImageFromWebCamService(_configuration.CameraConnectionInfo);

            var cacheUpdaterExpirationMilliSec = 600;
            DateTime lastImageAccess = DateTimeProvider.DateTimeNow;
            _imageProviderService = new ImageProviderService(imageFromCacheService,
                                                             imageFromWebCamService,
                                                             DateTimeProvider,
                                                             Logger,
                                                             cacheUpdaterExpirationMilliSec,
                                                             _configuration.ErrorImageLogPath,
                                                             lastImageAccess);

            AddConnectedUser();
        }

        private void AddConnectedUser()
        {
            if (ConnectedUsers.ContainsKey(UserId)) return;

            ConnectedUsers.TryAdd(UserId, UserIp);

            LogNewUserHasConnected();
        }


        [HttpGet]
        public HttpResponseMessage GetImage(string id)
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
            ImageFileWriter.WriteImageToFile(image, DateTimeProvider.DateTimeNow, _snapshotImagePath, Logger);
        }

        private static bool IsTimeToWriteAPicture()
        {
            return
                DateTimeProvider.DateTimeNow.Second >= 00 && // provide an interval of a few seconds to avoid
                DateTimeProvider.DateTimeNow.Second <= 03 // missing time ending in '00' seconds
            &&
            (
                DateTimeProvider.DateTimeNow.Minute == 00 ||
                DateTimeProvider.DateTimeNow.Minute == 15 ||
                DateTimeProvider.DateTimeNow.Minute == 30 ||
                DateTimeProvider.DateTimeNow.Minute == 45);
        }


        private void LogNewUserHasConnected()
        {
            string currentBrowserInfo = HttpContextHelper.GetBrowserInfo(HttpContext.Current);
            Logger?.LogUserIp(UserIp, UserId, currentBrowserInfo);
        }
    }
}
