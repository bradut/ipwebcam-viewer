using System;
using System.Net;

namespace IpWebCam3.Helpers
{
    /// <summary>
    ///  Contain the result info of an action
    /// </summary>
    public class Result
    {
        public bool  Success { get; set; }
        public string Message { get; set; }
        public Exception Error { get; set; }
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.Ambiguous;
    }
}