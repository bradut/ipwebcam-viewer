using System.Drawing;
using System.IO;
using System.Net;
using IpWebCam3.Models;

namespace IpWebCam3.Services
{
    /// <summary>
    /// Get an image from an IP camera
    /// </summary>
    public class ImageProviderService
    {
        public Image GetImage(CameraConnectionInfo connectionInfo)
        {
            return GetImage(connectionInfo.Username, 
                            connectionInfo.Password, 
                            connectionInfo.Url,
                            connectionInfo.Webpage, 
                            connectionInfo.Port);
        }

        private static Image GetImage(string username, string password, string url, string webpage, int port)
        {
            HttpWebResponse cameraResponse  = HttpBasicAuthenticationBypassService.DoWebRequest(username, password, url, webpage, port);
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