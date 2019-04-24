using System;

namespace IpWebCam3.Helpers.TimeHelpers
{
    public interface IDateTimeHelper
    {
        DateTime DateTimeNow { get; }

        string GetCurrentTimeAsString(bool includeMilliseconds = false);

        string GetCurrentTimeAsCompactString(bool includeMilliseconds = false);
    }

    public class DateTimeHelper: IDateTimeHelper
    {
        public DateTime DateTimeNow
        {
            get { return DateTime.Now; }
        }

        public string GetCurrentTimeAsString(bool includeMilliseconds = false)
        {
           return DateTimeFormatter.ConvertTimeToString(DateTimeNow, includeMilliseconds);
        }

        public string GetCurrentTimeAsCompactString(bool includeMilliseconds = false)
        {
            return DateTimeFormatter.ConvertTimeToCompactString(DateTimeNow, includeMilliseconds);
        }
    }
}