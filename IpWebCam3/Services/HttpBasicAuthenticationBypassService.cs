using System;
using System.Net;
using System.Text;

namespace IpWebCam3.Services
{
    /// <summary>
    /// Http Basic Authentication Bypass Service
    /// </summary>
    public class HttpBasicAuthenticationBypassService
    {
        // C# Http Basic Authentication Bypass
        // https://stackoverflow.com/questions/8228393/c-sharp-http-basic-authentication-bypass
        public static HttpWebResponse DoWebRequest(
            string username, string password, string url, string webpage, int port = 80, 
            string requestMethod = "GET", string contentType = "image/jpeg")
        {
            string path = url + ":" + port + "/" + webpage;

            string userData = username + ":" + password;
            //ASCIIEncoding encoding = new ASCIIEncoding();
            //byte[] data = encoding.GetBytes(path);
            byte[] authBytes = Encoding.UTF8.GetBytes(userData.ToCharArray());
            string reqShortHostTemp = url;
            string reqShortHost = reqShortHostTemp.Replace("http://", "");

            Uri uri = new Uri(path);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; .NET CLR 1.0.3705;)";
            request.Method = requestMethod;
            request.PreAuthenticate = false;
            request.Headers["Authorization"] = "Basic " + Convert.ToBase64String(authBytes);
            request.Accept = "image/png,image/jpeg,application/json,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

            request.Headers.Add("Accept-Language: en-us,en;q=0.5");
            request.Headers.Add("Accept-Encoding: gzip,deflate");
            request.Headers.Add("Accept-Charset: ISO-8859-1,utf-8;q=0.7,*;q=0.7");

            request.KeepAlive = true;
            request.Headers.Add("Keep-Alive: 1000");
            request.ReadWriteTimeout = 320000;
            request.Timeout = 320000;
            request.Host = reqShortHost;
            request.AllowAutoRedirect = true;

            request.ContentType = contentType;

            HttpWebResponse httpWebResponse;
            try
            {
                httpWebResponse = (HttpWebResponse)request.GetResponse();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return httpWebResponse;
        }
    }
}