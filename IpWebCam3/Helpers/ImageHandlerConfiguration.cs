using System;
using System.Web;
using IpWebCam3.Models;

namespace IpWebCam3.Helpers
{
    /// <summary>
    /// Singleton: Image Handler Configuration
    /// </summary>
    public class ImageHandlerConfiguration
    {
        private static ImageHandlerConfiguration _instance;

        private static readonly object LockMutex = new object();

        public readonly string AppRootDir =
            System.Web.Hosting.HostingEnvironment.MapPath(HttpRuntime.AppDomainAppVirtualPath);

        public static ImageHandlerConfiguration Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (LockMutex)
                    {
                        if (_instance == null)
                        {
                            _instance = new ImageHandlerConfiguration();
    }
}
                }

                return _instance;
            }
        }

        public CameraConnectionInfo CameraConnectionInfo { get; private set; }

        public string LogErrorsPath { get; private set; }

        public string LogUserIPsPath { get; private set; }

        public string LogUserPtzCmdPath { get; private set; }

        public string LogCacheStatsPath { get; private set; }

        private ImageHandlerConfiguration()
        {
            lock (LockMutex)
            {
                ReadConfiguration();
                if (!IsValid) throw new ArgumentException("Could not read settings from configuration");
            }
        }

        private bool IsValid
        {
            get
            {
                return CameraConnectionInfo != null &&
                       !string.IsNullOrWhiteSpace(CameraConnectionInfo.Url) &&
                       !string.IsNullOrWhiteSpace(LogErrorsPath)
                    ;
            }
        }

        private void ReadConfiguration()
        {
            string webCamUrl = System.Configuration.ConfigurationManager.AppSettings["MediaServerUrl"];
            int.TryParse(System.Configuration.ConfigurationManager.AppSettings["MediaServerPort"], out int webCamPort);
            string webCamWebPage = System.Configuration.ConfigurationManager.AppSettings["MediaServerImagePath"];

            string webCamUserName = System.Configuration.ConfigurationManager.AppSettings["MediaServerUsername"];
            string webCamPassword = System.Configuration.ConfigurationManager.AppSettings["MediaServerPassword"];

            CameraConnectionInfo = new CameraConnectionInfo
            (
                username: webCamUserName,
                password: webCamPassword,
                url: webCamUrl,
                webpage: webCamWebPage,
                port: webCamPort
            );

            string logErrorsPath = System.Configuration.ConfigurationManager.AppSettings["LogErrorsPath"];
            string logUserIPsPath = System.Configuration.ConfigurationManager.AppSettings["LogUserIPsPath"];
            string logUserPtzCmdPath = System.Configuration.ConfigurationManager.AppSettings["LogUserPtzCmdsPath"];
            string logCacheStatsPath = System.Configuration.ConfigurationManager.AppSettings["LogCacheStatsPath"];

            LogErrorsPath = AppRootDir + logErrorsPath;
            LogUserIPsPath = AppRootDir + logUserIPsPath;
            LogUserPtzCmdPath = AppRootDir + logUserPtzCmdPath;
            LogCacheStatsPath = AppRootDir + logCacheStatsPath;
        }
    }
}