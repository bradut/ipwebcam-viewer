using IpWebCam3.Helpers;
using IpWebCam3.Helpers.TimeHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace IpWebCam3.Controllers
{
    public class BaseApiController : ApiController
    {
        // shared, static vars
        protected readonly ImageHandlerConfiguration _configuration;
        private static readonly IDateTimeProvider DateTimeProvider = new DateTimeProvider();

        protected string UserIp { get; set; }
        protected int UserId { get; set; }


        private static MiniLogger _logger;
        protected static MiniLogger Logger
        {
            get
            {
                ImageHandlerConfiguration cfg = ImageHandlerConfiguration.Instance;
                return _logger ??
                       (_logger = new MiniLogger(DateTimeProvider,
                           cfg.UserIPsLogPath, cfg.UserPtzCmdLogPath,
                           cfg.ErrorsLogPath, cfg.CacheStatsLogPath));
            }
        }

        protected BaseApiController()
        {
            try
            {
                _configuration = ImageHandlerConfiguration.Instance;
            }
            catch (Exception e)
            {
                Logger?.LogError(e.Message);
                Console.WriteLine(e);
                throw;
            }

            if (!_configuration.IsValid)
            {
                LogInvalidConfiguration();
                _configuration.Reset();
            }
            
            UserIp = HttpContextHelper.GetIpFromHttpContext(HttpContext.Current);
            UserId = HttpContextHelper.GetUniqueUserIdFromBrowser(HttpContext.Current, UserIp);

            Logger?.SetUserInfo(currentUserId: UserId, currentUserIp: UserIp);
        }

        private void LogInvalidConfiguration()
        {
            IEnumerable<string> nullProperties = _configuration.GetNullValueProperties();
            string csv = string.Join(",", nullProperties.Where(x => x != null).Select(x => x.ToString()).ToArray());
            Logger?.LogError($"Could not read settings from configuration file: {csv}");
        }
    }
}
