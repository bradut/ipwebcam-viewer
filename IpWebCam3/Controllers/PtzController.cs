﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using IpWebCam3.Helpers;
using IpWebCam3.Services;

namespace IpWebCam3.Controllers
{
    public class PtzController : BaseApiController
    {
        [HttpGet]
        public HttpResponseMessage ExecutePtzCommand()
        {
            IEnumerable<KeyValuePair<string, string>> allUrlKeyValues = 
                                ControllerContext.Request.GetQueryNameValuePairs().ToList();
            string ptzCommand = allUrlKeyValues.FirstOrDefault(k => k.Key == "ptz").Value;
            string ptzParameters = allUrlKeyValues.FirstOrDefault(k => k.Key == "prms").Value;

            if (string.IsNullOrWhiteSpace(ptzCommand))
                return new HttpResponseMessage(HttpStatusCode.OK);

            Result result = PtzCgiService.ExecutePtzCommand(ptzCommand, ptzParameters,
                _configuration.CameraConnectionInfo);

            if (result.Error != null)
            {
                Logger.LogError(result.Message);
                return new HttpResponseMessage(result.StatusCode);
            }
            else
            {
                Logger.LogUserPtz(result.Message);
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }
    }
}

