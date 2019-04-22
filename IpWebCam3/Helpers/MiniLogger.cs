using IpWebCam3.Helpers.TimeHelpers;
using System;
using System.IO;

namespace IpWebCam3.Helpers
{
    /// <summary>
    /// Simple Logger. To be replaced by ILogger
    /// </summary>
    public class MiniLogger
    {
        private static readonly object LockFileWrite = new object();

        private static IDateTimeHelper _dateTimeHelper;
        private static string _logUserIPsPath;
        private static string _logUserPtzCmdPath;
        private static string _logErrorsPath;
        private static string _logCacheStatsPath;

        private string _currentUserIp;
        private int _currentUserId;


        public MiniLogger(IDateTimeHelper dateTimeHelper,
                          string logUserIPsPath, string logUserPtzCmdPath,
                          string logErrorsPath, string logCacheStatsPath)
        {
            _dateTimeHelper = dateTimeHelper;
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

        public void LogUserIp(string text)
        {
            WriteToLogFile(_logUserIPsPath, text.Replace(",", "_"));
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
            string logFileName = GetTodaysLogFileName(fileName);

            try
            {
                lock (LockFileWrite)
                {
                    string dateTime = _dateTimeHelper.GetCurrentTimeAsString(includeMilliseconds: true);
                    text = dateTime + "," + text;
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
                    _dateTimeHelper.GetDateTimeNow().ToString("_yyyy-MM-dd") + ".txt");
            }
        }
    }
}