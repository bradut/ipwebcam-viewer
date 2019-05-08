﻿using IpWebCam3.Helpers;
using IpWebCam3.Helpers.ImageHelpers;
using IpWebCam3.Services.ImageServices;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using IpWebCam3.Helpers.Configuration;
using IpWebCam3.Helpers.TimeHelpers;

namespace IpWebCam3.Controllers
{
    public class ImageController : BaseApiController
    {
        private static readonly ConcurrentDictionary<int, string> ConnectedUsers = new ConcurrentDictionary<int, string>();

        private readonly string _snapshotImagePath;
        private readonly IImageProviderService _imageProviderService;

        public ImageController(IImageProviderService imageProviderService,
                                AppConfiguration configuration, 
                                IDateTimeProvider dateTimeProvider, 
                                IMiniLogger logger)
            : base( configuration,  dateTimeProvider, logger)
        {
            _imageProviderService = imageProviderService;
            _snapshotImagePath = AppConfiguration.SnapShotImagePath;
            AddConnectedUser();
        }

        private void AddConnectedUser()
        {
            if (ConnectedUsers.ContainsKey(UserId))
                return;

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

        // ToDo: read these values from config and move this method out of here
        private bool IsTimeToWriteAPicture()
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
