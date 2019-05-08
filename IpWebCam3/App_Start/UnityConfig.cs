using System;
using System.Web.Http;
using IpWebCam3.Helpers;
using IpWebCam3.Helpers.Configuration;
using IpWebCam3.Helpers.TimeHelpers;
using IpWebCam3.Models;
using IpWebCam3.Services.ImageServices;
using Unity;
using Unity.AspNet.WebApi;
using Unity.Injection;
using Unity.Lifetime;

namespace IpWebCam3
{
    /// <summary>
    /// Specifies the Unity configuration for the main container.
    /// </summary>
    public static class UnityConfig
    {
        #region Unity Container
        private static readonly Lazy<IUnityContainer> LazyContainer =
          new Lazy<IUnityContainer>(() =>
          {
              var container = new UnityContainer();
              RegisterTypes(container);
              return container;
          });

        /// <summary>
        /// Configured Unity Container.
        /// </summary>
        public static IUnityContainer Container => LazyContainer.Value;
        #endregion

        /// <summary>
        /// Registers the type mappings with the Unity container (IoC container).
        /// </summary>
        /// <param name="container">The unity container to configure.</param>
        /// <remarks>
        /// There is no need to register concrete types such as controllers or
        /// API controllers (unless you want to change the defaults), as Unity
        /// allows resolving a concrete type even if it was not previously
        /// registered.
        /// </remarks>
        public static void RegisterTypes(IUnityContainer container)
        {
            // NOTE: To load from web.config uncomment the line below.
            // Make sure to add a Unity.Configuration to the using statements.
            // container.LoadConfiguration();

            // TODO: Register your type's mappings here.
            // container.RegisterType<IProductRepository, ProductRepository>();

            // Register interfaces
            AppConfiguration _configuration = AppConfiguration.Instance;
            container.RegisterInstance(
                                _configuration,
                                new ContainerControlledLifetimeManager() //Singleton
                );

            container.RegisterType<IDateTimeProvider, DateTimeProvider>(
                                new ContainerControlledLifetimeManager() //Singleton
                );

            container.RegisterType<IMiniLogger, MiniLogger>(
                        new InjectionConstructor(
                                 container.Resolve<IDateTimeProvider>(),
                                 container.Resolve<AppConfiguration>().UserIPsLogPath,
                                 container.Resolve<AppConfiguration>().UserPtzCmdLogPath,
                                 container.Resolve<AppConfiguration>().ErrorsLogPath,
                                 container.Resolve<AppConfiguration>().CacheStatsLogPath
                ));

            var imageCache = new CacheUpdateService();
            container.RegisterInstance(imageCache);

            container.RegisterType<IImageFromCacheService, ImageFromCacheService>(
                        new InjectionConstructor(
                                 container.Resolve<CacheUpdateService>(),
                                 container.Resolve<IMiniLogger>(),
                                 container.Resolve<AppConfiguration>().CacheLifeTimeMilliSec,
                                 container.Resolve<AppConfiguration>().CameraFps
                ));

            container.RegisterType<IImageFromWebCamService, ImageFromWebCamService>(
                        new InjectionConstructor(
                                container.Resolve<AppConfiguration>().CameraConnectionInfo
                ));

            var cacheUpdater = new CacheUpdaterInfo();
            container.RegisterInstance(cacheUpdater);

            container.RegisterType<IImageProviderService, ImageProviderService>(
                        new InjectionConstructor(
                                container.Resolve<IImageFromCacheService>(),
                                container.Resolve<IImageFromWebCamService>(),
                                container.Resolve<IDateTimeProvider>(),
                                container.Resolve<IMiniLogger>(),
                                container.Resolve<AppConfiguration>().CacheUpdaterExpirationMilliSec,
                                container.Resolve<AppConfiguration>().ErrorImageLogPath,
                                container.Resolve<CacheUpdaterInfo>()
                ));


            //// Register controllers - not needed
            //container.RegisterType<BaseApiController>();
            //container.RegisterType<ImageController>();
            //container.RegisterType<PtzController>();

            GlobalConfiguration.Configuration.DependencyResolver = new UnityDependencyResolver(container);
        }
    }
}