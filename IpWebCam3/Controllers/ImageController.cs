using IpWebCam3.Helpers;
using IpWebCam3.Helpers.ImageHelpers;
using IpWebCam3.Services.ImageServices;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using IpWebCam3.Helpers.Configuration;
using IpWebCam3.Helpers.Logging;
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

            RegisterConnectedUser();
        }

        private void RegisterConnectedUser()
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

            ImageFileWriter.SaveImageSnapshot(imageByteArray, DateTimeProviderInstance, _snapshotImagePath, Logger, UserId, UserIp);
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

        

        private void LogNewUserHasConnected()
        {
            string currentBrowserInfo = HttpContextHelper.GetBrowserInfo(HttpContext.Current);
            Logger?.LogUserIp(UserId, UserIp, currentBrowserInfo);
        }
    }
}
