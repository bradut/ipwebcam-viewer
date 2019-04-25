using System;

namespace IpWebCam3.Helpers.TimeHelpers
{
    public interface IDateTimeProvider
    {
        DateTime DateTimeNow { get; }

        string GetCurrentTimeAsString(bool includeMilliseconds = false);

        string GetCurrentTimeAsCompactString(bool includeMilliseconds = false);
    }

    public class DateTimeProvider: IDateTimeProvider
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