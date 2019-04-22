using System.Web;
using IpWebCam3.Helpers;
using System.Web.Http;
using IpWebCam3.Helpers.TimeHelpers;

namespace IpWebCam3.Controllers
{
    public class BaseApiController : ApiController
    {
        // shared, static vars
        protected readonly ImageHandlerConfiguration _configuration = ImageHandlerConfiguration.Instance;
        private static readonly IDateTimeHelper _dateTimeHelper = new DateTimeHelper();

        protected string UserIp { get; set; }
        protected int UserId { get; set; }


        private static MiniLogger _logger;
        protected static MiniLogger Logger
        {
            get
            {
                ImageHandlerConfiguration cfg = ImageHandlerConfiguration.Instance;
                return _logger ??
                       (_logger = new MiniLogger(  _dateTimeHelper,
                           cfg.LogUserIPsPath, cfg.LogUserPtzCmdPath,
                           cfg.LogErrorsPath, cfg.LogCacheStatsPath));
            }
        }

        protected BaseApiController()
        {
            UserIp = HttpContextHelper.GetIpFromHttpContext(HttpContext.Current);
            UserId = HttpContextHelper.GetUniqueUserIdFromBrowser(HttpContext.Current, UserIp);

            Logger.SetUserInfo(currentUserId:UserId, currentUserIp:UserIp);
        }

    }
}
