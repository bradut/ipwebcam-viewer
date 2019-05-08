using System.Net;
using System.Web;

namespace IpWebCam3.Helpers
{
    public static class HttpContextHelper
    {
        public static int GetUniqueUserIdFromBrowser(HttpContext context, string userIp)
        {
            string userAgent = context.Request.UserAgent;
            string userHostAddress = context.Request.UserHostAddress;
            string userHostName = context.Request.UserHostName;
            string userLanguages = context.Request.UserLanguages != null
                ? string.Join(",", context.Request.UserLanguages)
                : "";

            string allProperties = userIp + userAgent + userHostAddress + userHostName + userLanguages;
            int hashValue = allProperties.GetHashCode();

            return hashValue;
        }

        public static string GetBrowserInfo(HttpContext context)
        {
            var httpRequestBase = new HttpRequestWrapper(context.Request);
            HttpBrowserCapabilitiesBase browserCapabilities = httpRequestBase.Browser;
            string browserAndVersion = (browserCapabilities.Browser + " " + browserCapabilities.Version).Replace(",", " ");

            return browserAndVersion;
        }

        public static string GetIpFromHttpContext(HttpContext context)
        {
            string ip =
                (context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"] ??
                 context.Request.ServerVariables["REMOTE_ADDR"]).Split(',')[0].Trim();

            if (ip == "::1")
            {
                ip = GetLocalHostIp4Address();
            }

            return ip;
        }

        private static string GetLocalHostIp4Address()
        {
            string ip4Address = string.Empty;
            const string dnsIp = "192.168.";

            foreach (IPAddress ipAddress in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (ipAddress.AddressFamily.ToString() != "InterNetwork" ||
                    !ipAddress.ToString().StartsWith(dnsIp))
                    continue;

                ip4Address = ipAddress.ToString();
                break;
            }

            return ip4Address;
        }

    }
}