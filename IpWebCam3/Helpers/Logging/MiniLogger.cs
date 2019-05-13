using IpWebCam3.Helpers.TimeHelpers;
using System;
using System.IO;

namespace IpWebCam3.Helpers.Logging
{
    public interface IMiniLogger
    {
        void LogError(string errorMessage, int userId, string userIp = null);

        void LogUserIp(int userId, string userIp, string currentBrowserInfo);

        void LogUserPtz(string ptzMessage, int userId, string userIp);

        void LogCacheStat(string cacheMessage, int userId);
    }

    /// <summary>
    /// Simple Logger. To be replaced by ILogger
    /// </summary>
    public class MiniLogger : IMiniLogger
    {
        private static readonly object LockFileWrite = new object();

        private static IDateTimeProvider _dateTimeProvider;
        private static string _logUserIPsPath;
        private static string _logUserPtzCmdPath;
        private static string _logErrorsPath;
        private static string _logCacheStatsPath;

        public MiniLogger(IDateTimeProvider dateTimeProvider,
                          string logUserIPsPath, string logUserPtzCmdPath,
                          string logErrorsPath, string logCacheStatsPath)
        {
            _dateTimeProvider = dateTimeProvider;
            _logUserIPsPath = logUserIPsPath;
            _logUserPtzCmdPath = logUserPtzCmdPath;
            _logErrorsPath = logErrorsPath;
            _logCacheStatsPath = logCacheStatsPath;
        }
        

        public void LogError(string errorMessage, int userId, string userIp = null)
        {
            errorMessage = userIp + "," + userId + "," + errorMessage.Replace(",", "_");
            WriteToLogFile(_logErrorsPath, errorMessage);
        }

        public void LogUserIp(int userId, string userIp, string currentBrowserInfo)
        {
            string logInfo = userIp + "," + userId + "," + currentBrowserInfo.Replace(",", "_");
            WriteToLogFile(_logUserIPsPath, logInfo);
        }


        public void LogUserPtz(string ptzMessage, int userId, string userIp)
        {
            ptzMessage = userIp + "," + userId + "," + ptzMessage.Replace(",", "_");
            WriteToLogFile(_logUserPtzCmdPath, ptzMessage);
        }

        public void LogCacheStat(string cacheMessage, int userId)
        {
            cacheMessage = cacheMessage.Replace(",", "_");
            cacheMessage = userId.GetFormattedUserId() + "," + cacheMessage;
            WriteToLogFile(_logCacheStatsPath, cacheMessage);
        }


        private static void WriteToLogFile(string fileName, string text)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            if (string.IsNullOrEmpty(text)) return;

            string logFileName = GetTodaysLogFileName(fileName);

            try
            {
                lock (LockFileWrite)
                {
                    DateTime dateTimeNow = _dateTimeProvider.DateTimeNow;
                    string strDateTime = DateTimeFormatter.ConvertTimeToCompactString(dateTime: dateTimeNow, withMilliSeconds: true);
                    text = strDateTime + "," + text;
                    File.AppendAllText(logFileName, text + Environment.NewLine);
                }
            }
            catch (DirectoryNotFoundException)
            {
                string directoryName = Path.GetDirectoryName(logFileName);
                if (!string.IsNullOrWhiteSpace(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                    File.AppendAllText(logFileName, text + Environment.NewLine);
                }
            }

            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        // There will be one log file per day
        private static string GetTodaysLogFileName(string fileName)
        {
            lock (LockFileWrite)
            {
                return fileName.Replace(".txt",
                    _dateTimeProvider.DateTimeNow.ToString("_yyyy-MM-dd") + ".txt");
            }
        }

    }
}