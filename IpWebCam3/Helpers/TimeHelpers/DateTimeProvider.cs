using System;

namespace IpWebCam3.Helpers.TimeHelpers
{
    // Helper to allow unit testing
    public interface IDateTimeProvider
    {
        DateTime DateTimeNow { get; }
    }

    public class DateTimeProvider: IDateTimeProvider
    {
        public DateTime DateTimeNow
        {
            get { return DateTime.Now; }
        }
    }
}