using IpWebCam3.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

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

        public string ErrorsLogPath { get; private set; }

        public string UserIPsLogPath { get; private set; }

        public string UserPtzCmdLogPath { get; private set; }

        public string CacheStatsLogPath { get; private set; }

        public string SnapShotImagePath { get; set; }

        public string ErrorImageLogPath { get; set; }

        public bool IsValid
        {
            get
            {
                return CameraConnectionInfo != null &&
                       CameraConnectionInfo.IsValid;
            }
        }

        private ImageHandlerConfiguration()
        {
            lock (LockMutex)
            {
                try
                {
                    ReadConfiguration();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }


        private void ReadConfiguration()
        {
            string webCamUserName = System.Configuration.ConfigurationManager.AppSettings["MediaServerUsername"];
            string webCamPassword = System.Configuration.ConfigurationManager.AppSettings["MediaServerPassword"];
            string webCamUrl = System.Configuration.ConfigurationManager.AppSettings["MediaServerUrl"];
            string webCamWebPage = System.Configuration.ConfigurationManager.AppSettings["MediaServerImagePath"];
            int.TryParse(System.Configuration.ConfigurationManager.AppSettings["MediaServerPort"], out int webCamPort);
            
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
            string logUserPtzCmdPath = System.Configuration.ConfigurationManager.AppSettings["LogUserPtzCmdPath"];
            string logCacheStatsPath = System.Configuration.ConfigurationManager.AppSettings["LogCacheStatsPath"];
            string snapShotImagePath = System.Configuration.ConfigurationManager.AppSettings["SnapShotImagePath"];
            string imageErrorLogoPath = System.Configuration.ConfigurationManager.AppSettings["ImageErrorLogoPath"];

            ErrorsLogPath = AppRootDir + (logErrorsPath ?? @"\App_Data\logs\errors.txt");
            UserIPsLogPath = AppRootDir + (logUserIPsPath ?? @"\App_Data\logs\userIps.txt");
            UserPtzCmdLogPath = AppRootDir + (logUserPtzCmdPath ?? @"\App_Data\logs\userPtz.txt");
            CacheStatsLogPath = AppRootDir + (logCacheStatsPath ?? @"\App_Data\logs\cacheStats.txt");
            SnapShotImagePath = AppRootDir + (snapShotImagePath ?? @"\App_Data\outputimages\");
            ErrorImageLogPath = AppRootDir + (imageErrorLogoPath ?? @"images\earth_hd_1.jpg");
        }

        public IEnumerable<string> GetNullValueProperties()
        {
            List<string> retVal = this.GetNullValuePropertyNames().ToList();

            if (CameraConnectionInfo.IsValid)
                return retVal;

            IEnumerable<string> cameraConnInfoNullProperties = CameraConnectionInfo.GetNullValuePropertyNames();
            retVal.AddRange(cameraConnInfoNullProperties);

            return retVal;
        }

        // allow re-reading values from config files
        public void Reset()
        {
            lock (LockMutex)
            {
                _instance = null;
            }
        }

    }
}