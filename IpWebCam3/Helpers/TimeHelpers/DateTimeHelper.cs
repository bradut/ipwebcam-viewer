using System;

namespace IpWebCam3.Helpers.TimeHelpers
{
    public interface IDateTimeHelper
    {
        DateTime GetDateTimeNow();

        string GetCurrentTimeAsString(bool includeMilliseconds = false);

        string GetCurrentTimeAsCompactString(bool includeMilliseconds = false);
    }

    public class DateTimeHelper: IDateTimeHelper
    {
        public  DateTime GetDateTimeNow()
        {
            return DateTime.Now;
        }

        public string GetCurrentTimeAsString(bool includeMilliseconds = false)
        {
           return DateTimeFormatter.ConvertTimeToString(GetDateTimeNow(), includeMilliseconds);
        }

        public string GetCurrentTimeAsCompactString(bool includeMilliseconds = false)
        {
            return DateTimeFormatter.ConvertTimeToCompactString(GetDateTimeNow(), includeMilliseconds);
        }
    }
}