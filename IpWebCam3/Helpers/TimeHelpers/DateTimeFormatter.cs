using System;

namespace IpWebCam3.Helpers.TimeHelpers
{
    public static class DateTimeFormatter
    {      public static string ConvertTimeToString(DateTime dateTime, bool withMilliSeconds)
        {
            string millisecondsFormat = withMilliSeconds ? ".fff" : "";
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss" + millisecondsFormat);
        }

        public static string ConvertTimeToCompactString(DateTime dateTime, bool withMilliSeconds)
        {
            return ConvertTimeToString(dateTime, withMilliSeconds)
                .Replace("-", "").Replace(" ", "_").Replace(":", "");
        }
    }
}