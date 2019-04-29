using IpWebCam3.Models;
using System.Drawing;
using System.IO;
using System.Net;

namespace IpWebCam3.Services.ImageServices
{
    /// <summary>
    /// Get an image from an IP camera
    /// </summary>
    public class ImageFromWebCamService
    {
        private readonly CameraConnectionInfo _connectionInfo;

        public ImageFromWebCamService(CameraConnectionInfo connectionInfo)
        {
            _connectionInfo = connectionInfo;
        }


        public Image GetImage(string userUtc = null)
        {
            return GetImage(_connectionInfo.Username,
                _connectionInfo.Password,
                _connectionInfo.Url,
                _connectionInfo.Webpage + (userUtc != null ? "?" + userUtc : ""),
                _connectionInfo.Port);
        }

        private static Image GetImage(string username, string password, string url, string webpage, int port)
        {
            HttpWebResponse cameraResponse = HttpBasicAuthenticationBypassService
                                                 .DoWebRequest(username, password, url, webpage, port);
            Image image = GetImageFromHttpWebResponse(cameraResponse);

            return image;
        }

        private static Image GetImageFromHttpWebResponse(HttpWebResponse cameraResponse)
        {
            using (Stream stream = cameraResponse?.GetResponseStream())
            {
                if (stream == null) return null;

                Image image = Image.FromStream(stream);

                return image;
            }
        }
    }
}