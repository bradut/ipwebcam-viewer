using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using IpWebCam3.Models;

namespace IpWebCam3.Helpers.Configuration
{
    /// <summary>
    /// App Configuration (Singleton)
    /// </summary>
    public class AppConfiguration
    {
        private static AppConfiguration _appConfigInstance;

        private static readonly object LockMutex = new object();

        private readonly string AppRootDir =
            System.Web.Hosting.HostingEnvironment.MapPath(HttpRuntime.AppDomainAppVirtualPath);

        public static AppConfiguration Instance
        {
            get
            {
                if (_appConfigInstance == null)
                {
                    lock (LockMutex)
                    {
                        if (_appConfigInstance == null)
                        {
                            _appConfigInstance = new AppConfiguration();
                        }
                    }
                }

                return _appConfigInstance;
            }
        }

        public CameraConnectionInfo CameraConnectionInfo { get; private set; }

        public string ErrorsLogPath { get; private set; }

        public string UserIPsLogPath { get; private set; }

        public string UserPtzCmdLogPath { get; private set; }

        public string CacheStatsLogPath { get; private set; }

        public string SnapShotImagePath { get; private set; }

        public string ErrorImageLogPath { get; private set; }

        public int CameraFps { get; private set; }

        public int CacheLifeTimeMilliSec { get; private set; }

        public int CacheUpdaterExpirationMilliSec { get; private set; }


        public bool IsValid
        {
            get
            {
                return CameraConnectionInfo != null &&
                       CameraConnectionInfo.IsValid;
            }
        }

        private AppConfiguration()
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
            string imageErrorLogoPath = System.Configuration.ConfigurationManager.AppSettings["ErrorImagePath"];

            ErrorsLogPath = AppRootDir + (logErrorsPath ?? @"\App_Data\logs\errors.txt");
            UserIPsLogPath = AppRootDir + (logUserIPsPath ?? @"\App_Data\logs\userIps.txt");
            UserPtzCmdLogPath = AppRootDir + (logUserPtzCmdPath ?? @"\App_Data\logs\userPtz.txt");
            CacheStatsLogPath = AppRootDir + (logCacheStatsPath ?? @"\App_Data\logs\cacheStats.txt");
            SnapShotImagePath = AppRootDir + (snapShotImagePath ?? @"\App_Data\outputimages\");
            ErrorImageLogPath = AppRootDir + (imageErrorLogoPath ?? @"\images\earth_hd_1.jpg");

            CameraFps = int.TryParse(System.Configuration.ConfigurationManager.AppSettings["CameraFPS"], out int cameraFps)
                ? cameraFps
                : 5;

            CacheLifeTimeMilliSec = int.TryParse(System.Configuration.ConfigurationManager.AppSettings["CacheLifeTimeMilliSec"],
                out int cacheLifeTimeMilliSec)
                ? cacheLifeTimeMilliSec
                : 2000;

            CacheUpdaterExpirationMilliSec = int.TryParse(
                System.Configuration.ConfigurationManager.AppSettings["CacheUpdaterExpirationMilliSec"],
                out int cacheUpdaterExpirationMilliSec)
                ? cacheUpdaterExpirationMilliSec
                : 600;
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

        // Allow re-reading values from the config file
        public void Reset()
        {
            lock (LockMutex)
            {
                _appConfigInstance = null;
            }
        }

    }
}