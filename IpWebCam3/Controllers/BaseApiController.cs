using IpWebCam3.Helpers;
using IpWebCam3.Helpers.TimeHelpers;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using IpWebCam3.Helpers.Configuration;

namespace IpWebCam3.Controllers
{
    public class BaseApiController : ApiController
    {
        protected readonly AppConfiguration AppConfiguration;
        protected readonly IDateTimeProvider DateTimeProvider;

        protected string UserIp { get; set; }
        protected int UserId { get; set; }


        protected static IMiniLogger Logger { get; private set; }
        
        public BaseApiController(AppConfiguration appConfiguration, IDateTimeProvider dateTimeProvider, IMiniLogger logger)
        {
            AppConfiguration = appConfiguration;
            DateTimeProvider = dateTimeProvider;
            Logger = logger;

            UserIp = HttpContextHelper.GetIpFromHttpContext(HttpContext.Current);
            UserId = HttpContextHelper.GetUniqueUserIdFromBrowser(HttpContext.Current, UserIp);

            Logger?.SetUserInfo(currentUserId: UserId, currentUserIp: UserIp);

            if (!AppConfiguration.IsValid)
            {
                LogInvalidConfiguration();
                AppConfiguration.Reset();
            }
        }

        private void LogInvalidConfiguration()
        {
            IEnumerable<string> nullProperties = AppConfiguration.GetNullValueProperties();
            string csv = string.Join(",", nullProperties.Where(x => x != null).Select(x => x.ToString()).ToArray());
            Logger?.LogError($"Could not read settings from configuration file: {csv}");
        }
    }
}
