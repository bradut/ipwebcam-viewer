using System;
using System.Net;
using IpWebCam3.Helpers;
using IpWebCam3.Models;

namespace IpWebCam3.Services.PtzServices
{
    /// <summary>
    /// Execute PTZ (Pan Tilt Zoom) commands by calling camera's CGI API (not ONVIF API)
    /// http://tutoriels.domotique-store.fr/content/52/293/fr/api-des-cameras-ip-wanscam-onvif-hw-nouvelle-generation.htmlk 
    /// </summary>
    public class PtzCgiService
    {
        public struct PtzCommands
        {
            // Mobile commands: Some IP WebCams use these:
            public const string MobileYtUp = "ytup";
            public const string MobileYtDown = "ytdown";
            public const string MobileYtLeft = "ytleft";
            public const string MobileYtRight = "ytright";

            // Mobile commands: Others use these:
            public const string MobilePtzUp = "ptzup";
            public const string MobilePtzDown = "ptzdown";
            public const string MobilePtzLeft = "ptzleft";
            public const string MobilePtzRight = "ptzright";

            public const string MobilePtzZoomIn = "ptzzoomin";
            public const string MobilePtzZoomOut = "ptzzoomout";

            public const string PtzCtrl = "ptzctrl";
            public const string PtzPreset = "preset";
        }

        public static Result ExecutePtzCommand(string ptzCmd, string ptzParameters, CameraConnectionInfo connectionInfo)
        {
            return ExecutePtzCommand(ptzCmd, ptzParameters, connectionInfo.Username, connectionInfo.Password, connectionInfo.Url, connectionInfo.Port);
        }

        public static Result ExecutePtzCommand(string ptzCmd, string ptzParameters, string webCamUsername, string webCamPassword, string url, int port)
        {
            string ptzCmdCgiPath;

            try
            {
                ptzCmdCgiPath = CreatePtzCmdCgiPath(ptzCmd, ptzParameters);
            }
            catch (Exception e)
            {
                return new Result { Success = false, Message = $"PTZ command {ptzCmd}/{ptzParameters} had path creation problem: {e.Message}" };
            }


            HttpWebResponse httpWebResponse;

            try
            {
                httpWebResponse = HttpBasicAuthenticationBypassService.DoWebRequest(webCamUsername, webCamPassword, url, ptzCmdCgiPath, port, "GET", "application/json");
            }
            catch (Exception ex)
            {
                string exceptionMessage = "PTZ: " + ptzCmdCgiPath + " Error: Could not execute " + url + " " + port + " " + ptzCmd + " Error: " + ex.Message;

                return new Result { Success = false, Message = exceptionMessage, Error = ex };
            }

            string ptzMessage = httpWebResponse == null
                ? $"PTZ: {ptzCmd}/{ptzParameters}  Error: Could not execute " + url + " " + port + " " + ptzCmd
                : $"PTZ: {ptzCmd}/{ptzParameters}  Status: " + httpWebResponse.StatusCode;

            return new Result { Success = httpWebResponse != null, Message = ptzMessage, StatusCode = httpWebResponse?.StatusCode ?? HttpStatusCode.Ambiguous };
        }

        private const string CgiRoot = "cgi-bin/hi3510/";

        // Create the path of the CGI command. Example: "cgi-bin/hi3510/ytup.cgi"
        private static string CreatePtzCmdCgiPath(string ptzCmd, string ptzParameters)
        {
            if (string.IsNullOrWhiteSpace(ptzCmd)) throw new ArgumentException("PTZ cmd is null or empty");

            string ptzCmdCgiPath;

            switch (ptzCmd)
            {
                case PtzCommands.MobileYtUp:
                case PtzCommands.MobileYtDown:
                case PtzCommands.MobileYtLeft:
                case PtzCommands.MobileYtRight:

                case PtzCommands.MobilePtzUp:
                case PtzCommands.MobilePtzDown:
                case PtzCommands.MobilePtzLeft:
                case PtzCommands.MobilePtzRight:

                case PtzCommands.MobilePtzZoomIn:
                case PtzCommands.MobilePtzZoomOut:
                    ptzCmdCgiPath = CgiRoot + ptzCmd + ".cgi";
                    break;

                case PtzCommands.PtzCtrl:
                    ptzCmdCgiPath = CreatePtzCtrlCgiPath(ptzParameters);
                    break;

                case PtzCommands.PtzPreset:
                    ptzCmdCgiPath = CreatePtzPresetCgiPath(ptzParameters);
                    break;

                default:
                    throw new ArgumentException($"Unexpected PTZ cmd: {ptzCmd}");
            }

            if (string.IsNullOrWhiteSpace(ptzCmdCgiPath)) throw new ArgumentException($"Could not create PTZ cmd path for: {ptzCmd}");

            return ptzCmdCgiPath;
        }

        /// <summary>
        /// Create the path of a PTZ Preset cmd to turn the camera towards a given preset point
        /// </summary>
        /// <param name="ptzPresetParameters">Example:-act=goto,-number=0</param>
        /// <returns></returns>
        private static string CreatePtzPresetCgiPath(string ptzPresetParameters)
        {
            if (string.IsNullOrWhiteSpace(ptzPresetParameters)) throw new ArgumentNullException(nameof(ptzPresetParameters));

            string[] @params = GetParametersArray(ptzPresetParameters);
            var presetPointNumber = -1;
            var action = "";

            foreach (string kvParam in @params)
            {
                string[] keyVal = kvParam.Split('=');
                if (keyVal.Length != 2 || string.IsNullOrWhiteSpace(keyVal[0]) || string.IsNullOrWhiteSpace(keyVal[1]))
                {
                    continue;
                }

                action = "goto"; // only 'goto' is allowed: users are not allowed to update preset points 

                if (keyVal[0].Trim().StartsWith("-number"))
                {
                    int.TryParse(keyVal[1], out presetPointNumber);
                }
            }

            string ptzCmdCgiPath = CgiRoot + "param.cgi?cmd=preset&-act=" + action + "&-number=" + presetPointNumber;

            return ptzCmdCgiPath;
        }

        private static string[] GetParametersArray(string ptzCtrlParams)
        {
            char[] splitters = { ',', '&' };
            string[] @params = ptzCtrlParams.Split(splitters);
            return @params;
        }

        /// <summary>
        /// Create the path of a PTZ Ctrl cmd to allow precise movement of the camera
        /// </summary>
        /// <param name="ptzCtrlParams">Example:-step=0,act=left,-speed=45</param>
        /// <returns></returns>
        private static string CreatePtzCtrlCgiPath(string ptzCtrlParams)
        {
            if (string.IsNullOrWhiteSpace(ptzCtrlParams)) throw new ArgumentNullException(nameof(ptzCtrlParams));

            string[] @params = GetParametersArray(ptzCtrlParams);
            var stepMode = -1;
            var action = "";
            var speed = -1;

            foreach (string kvParam in @params)
            {
                string[] keyVal = kvParam.Split('=');
                if (keyVal.Length != 2 || string.IsNullOrWhiteSpace(keyVal[0]) || string.IsNullOrWhiteSpace(keyVal[1]))
                {
                    continue;
                }

                if (keyVal[0].Trim().StartsWith("-step"))
                {
                    int.TryParse(keyVal[1], out stepMode);
                }

                if (keyVal[0].Trim().StartsWith("-act"))
                {
                    action = keyVal[1].Trim();
                }

                if (keyVal[0].Trim().StartsWith("-speed"))
                {
                    int.TryParse(keyVal[1], out speed);
                }
            }

            string ptzCmdCgiPath = CreatePtzCtrlCgiPath(stepMode, action, speed);
            return ptzCmdCgiPath;
        }

        /// <summary>
        /// Validate and create a PtzCtrl command path:
        /// </summary>
        /// <param name="stepMode">After command run, when 0:  PTZ needs new CGI cmd to stop, when 1: PTZ stops automatically</param>
        /// <param name="action">left, right, up, down, home, (zoomin, zoomout)/(zoom in, zoom out), hscan, vscan, focusin, focusout, stop</param>
        /// <param name="speed">PTZ speed: 1...63</param>
        /// <returns></returns>
        private static string CreatePtzCtrlCgiPath(int stepMode, string action, int speed)
        {
            if (stepMode != 0 && stepMode != 1) throw new ArgumentException("stepMode should be 0 or 1 but is " + stepMode);
            if (speed < 1 || speed > 63) speed = 3;

            string ptzCmdCgiPath = CgiRoot + "/ptzctrl.cgi?-step=" + stepMode + "&-act=" + action + "&-speed=" + speed;

            return ptzCmdCgiPath;
        }
    }
}