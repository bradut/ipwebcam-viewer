using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using IpWebCam3.Helpers;
using IpWebCam3.Helpers.Configuration;
using IpWebCam3.Helpers.Logging;
using IpWebCam3.Helpers.TimeHelpers;
using IpWebCam3.Services.PtzServices;

namespace IpWebCam3.Controllers
{
    public class PtzController : BaseApiController
    {

        public PtzController(AppConfiguration configuration, 
                            IDateTimeProvider dateTimeProvider, 
                            IMiniLogger logger) 
            : base(configuration, dateTimeProvider, logger)
        {}

        [HttpGet]
        public HttpResponseMessage ExecutePtzCommand()
        {
            IEnumerable<KeyValuePair<string, string>> allUrlKeyValues = ControllerContext
                                                                            .Request
                                                                            .GetQueryNameValuePairs()
                                                                            .ToList();

            string ptzCommand = allUrlKeyValues.FirstOrDefault(k => k.Key == "ptz").Value;

            //ToDo: This only reads the first parameter.  May need to read more parameters and pass them to the CGI API.
            // See how they the params are being passed by the JavaScript in the web page.
            string ptzParameters = allUrlKeyValues.FirstOrDefault(k => k.Key == "prms").Value;

            if (string.IsNullOrWhiteSpace(ptzCommand))
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            Result result = PtzCgiService.ExecutePtzCommand(ptzCmd: ptzCommand, ptzParameters: ptzParameters,
                                                            connectionInfo: AppConfiguration.CameraConnectionInfo);
            if (result.Error == null)
            {
                Logger?.LogError(result.Message, UserId, UserIp);
                return new HttpResponseMessage(result.StatusCode);
            }
            else
            {
                Logger?.LogUserPtz(result.Message, UserId, UserIp);
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }
    }
}

