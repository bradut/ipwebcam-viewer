namespace IpWebCam3.Helpers.Logging
{
    public static class LogExtensionMethods
    {
        public static string GetFormattedUserId(this int id)
        {
            return id.ToString().PadLeft(12, ' ');
        }
    }
}