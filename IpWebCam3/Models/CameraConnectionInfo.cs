using System;

namespace IpWebCam3.Models
{
    /// <summary>
    /// IP Camera server info: username, pwd, url, webPage, port
    /// </summary>
    [Serializable]
    public class CameraConnectionInfo
    {
        public string Username { get; }
        public string Password { get; }
        public string Url { get; }
        public string Webpage { get; set; }
        public int Port { get; }

        public CameraConnectionInfo(string username, string password, string url, string webpage, int port)
        {
            Username = username;
            Password = password;
            Url = url;
            Webpage = webpage;
            Port = port;
        }

        public CameraConnectionInfo(CameraConnectionInfo otherConnectionInfo)
        {
            Username = otherConnectionInfo.Username;
            Password = otherConnectionInfo.Password;
            Url = otherConnectionInfo.Url;
            Webpage = otherConnectionInfo.Webpage;
            Port = otherConnectionInfo.Port;
        }

        public bool IsValid => !(string.IsNullOrEmpty(Url));
    }
}