using IpWebCam3.Helpers.TimeHelpers;
using System;
using System.IO;

namespace IpWebCam3.Helpers
{
    public interface IMiniLogger
    {
        void SetUserInfo(string currentUserIp, int currentUserId);
        void LogError(string errorMessage);

        void LogUserIp(string userIp, int userId, string currentBrowserInfo) //(string text)
            ;

        void LogUserPtz(string ptzMessage);
        void LogCacheStat(string statMessage);
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

        private string _currentUserIp;
        private int _currentUserId;


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

        public void SetUserInfo(string currentUserIp, int currentUserId)
        {
            _currentUserIp = currentUserIp;
            _currentUserId = currentUserId;
        }

        public void LogError(string errorMessage)
        {
            errorMessage = _currentUserIp + "," + _currentUserId + "," + errorMessage.Replace(",", "_");
            WriteToLogFile(_logErrorsPath, errorMessage);
        }

        public void LogUserIp(string userIp, int userId, string currentBrowserInfo) //(string text)
        {
            string logInfo = userIp + "," + userId + "," + currentBrowserInfo.Replace(",", "_");
            WriteToLogFile(_logUserIPsPath, logInfo);
        }


        public void LogUserPtz(string ptzMessage)
        {
            ptzMessage = _currentUserIp + "," + _currentUserId + "," + ptzMessage.Replace(",", "_");
            WriteToLogFile(_logUserPtzCmdPath, ptzMessage);
        }

        public void LogCacheStat(string statMessage)
        {
            WriteToLogFile(_logCacheStatsPath, statMessage.Replace(",", "_"));
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