using IpWebCam3.Helpers;
using IpWebCam3.Helpers.TimeHelpers;
using IpWebCam3.Models;
using IpWebCam3.Services.ImageServices;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Drawing;
using IpWebCam3.Helpers.ImageHelpers;
using Assert = NUnit.Framework.Assert;

namespace IpWebCam3.Tests.Services.ImageServices
{
    [TestFixture]
    public class ImageProviderServiceTests
    {
        private IImageFromCacheService _imageFromCacheService;
        private IImageFromWebCamService _imageFromWebCamService;
        private IDateTimeProvider _dateTimeProvider;
        private MiniLogger _logger;
        private CacheUpdaterInfo _cacheUpdater;
        private int _cacheUpdaterExpirationMilliSec;


        [SetUp]
        public void Init()
        {
            _imageFromCacheService = Substitute.For<IImageFromCacheService>();
            _imageFromWebCamService = Substitute.For<IImageFromWebCamService>();
            _dateTimeProvider = Substitute.For<IDateTimeProvider>();
            _logger = null;
            _cacheUpdater = new CacheUpdaterInfo();
            _cacheUpdaterExpirationMilliSec = 600;
        }


        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        public void Create_InvalidNullServices_Throws(bool isNullImageFromCacheService, bool isNullImageFromWebCamService, bool isNullDateTimeProvider)
        {
            // Arrange:
            IImageFromCacheService cacheService = isNullImageFromCacheService ? null : _imageFromCacheService;
            IImageFromWebCamService webCamService = isNullImageFromWebCamService ? null : _imageFromWebCamService;
            IDateTimeProvider dateTimeService = isNullDateTimeProvider ? null : _dateTimeProvider;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ImageProviderService(
                imageFromCacheService: cacheService,
                imageFromWebCamService: webCamService,
                dateTimeProvider: dateTimeService,
                logger: _logger,
                cacheUpdaterExpirationMilliSec: 1000,
                imageErrorLogoUrl: "some_value",
                lastCacheAccess: DateTime.Now,
                cacheUpdater: null
                )
            );
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Create_InvalidExpirationValue_Throws(int expirationValueMilliSec)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new ImageProviderService(
                imageFromCacheService: _imageFromCacheService,
                imageFromWebCamService: _imageFromWebCamService,
                dateTimeProvider: _dateTimeProvider,
                logger: _logger,
                cacheUpdaterExpirationMilliSec: expirationValueMilliSec,
                imageErrorLogoUrl: "some_value",
                lastCacheAccess: DateTime.Now,
                cacheUpdater: null
                )
            );
        }


        [TestCase(1, int.MinValue)]
        [TestCase(1, int.MaxValue)]
        [TestCase(1, 0)]
        [TestCase(1, 10)]
        public void CanReadImageFromWebCam_SameUserIdAndAnyRequestTime_ReturnTrue(int userId, int requestDelayMilliSec)
        {
            // Arrange
            _dateTimeProvider.DateTimeNow.Returns(new DateTime(2019, 05, 01, 15, 30, 30, 100));
            _cacheUpdater.Update(userId, _dateTimeProvider.DateTimeNow);
            IImageProviderService imageProviderService = CreateValidImageProviderService();
            DateTime requestTime = RequestTime(requestDelayMilliSec);

            // Act
            bool canReadImageFromWebCam = imageProviderService.CanReadImageFromWebCam(userId, requestTime);

            // Assert
            Assert.That(canReadImageFromWebCam, Is.True);
        }


        [TestCase(10000, true)]
        [TestCase(1, true)]
        [TestCase(0, false)]
        [TestCase(-1, false)]
        [TestCase(-10000, false)]

        public void CanReadImageFromWebCam_DifferentUserIdAndCacheUpdaterIsGiven_ReturnExpectedValue(int requestDelayMilliSec, bool expectedCanRead)
        {
            // Arrange
            _dateTimeProvider.DateTimeNow.Returns(new DateTime(2019, 05, 01, 15, 30, 30, 100));

            const int userId = 1;
            const int cacheUpdaterUserId = userId + 10;
            _cacheUpdater.Update(userId: cacheUpdaterUserId, lastUpdate: _dateTimeProvider.DateTimeNow);

            IImageProviderService imageProviderService = CreateValidImageProviderService();
            DateTime requestTime = RequestTime(requestDelayMilliSec + _cacheUpdaterExpirationMilliSec);

            // Act
            bool canReadImageFromWebCam = imageProviderService.CanReadImageFromWebCam(userId, requestTime);

            // Assert
            Assert.That(canReadImageFromWebCam, Is.EqualTo(expectedCanRead));
        }



        [TestCase(1, 1)]
        [TestCase(1, 2)]
        public void GetImageAsByteArray_WhenCannotReadNewImageFromCacheAndCanReadFromWebCam_ReturnImageFromWebCam(int userId, int cacheUpdaterUserId)
        {
            // Arrange
            const string userUtc = "some_UTC_value";
            _dateTimeProvider.DateTimeNow.Returns(new DateTime(2019, 05, 01, 15, 30, 30, 100));
            _imageFromCacheService.WaitBeforeGettingNextImage(Arg.Any<int>(), Arg.Any<DateTime>()).Returns(0);

            _imageFromCacheService.GetNewImageAsByteArray(Arg.Any<int>(), Arg.Any<DateTime>()).Returns((byte[])null);
            _cacheUpdater.Update(userId: cacheUpdaterUserId, lastUpdate: DateTime.MinValue);

            var imageFromWebCam = new Bitmap(1, 1);
            byte[] imageArrayFromWebCam = ImageHelper.ConvertImageToByteArray(imageFromWebCam);
            _imageFromWebCamService.GetImage(Arg.Any<string>()).Returns(imageFromWebCam);

            IImageProviderService imageProviderService = CreateValidImageProviderService();


            // Act
            bool canReadImageFromWebCam = imageProviderService.CanReadImageFromWebCam(userId, _dateTimeProvider.DateTimeNow);
            byte[] imageAsByteArray = imageProviderService.GetImageAsByteArray(userId, userUtc);

            // Assert 
            Assert.That(canReadImageFromWebCam, Is.True);
            Assert.That(imageAsByteArray, Is.Not.Null);

            Assert.That(imageAsByteArray.Length, Is.EqualTo(imageArrayFromWebCam.Length));

            Assert.That(_cacheUpdater.LastUpdate, Is.EqualTo(_dateTimeProvider.DateTimeNow));
            Assert.That(_cacheUpdater.UserId, Is.EqualTo(userId));
        }


        [TestCase(1, 2)]
        public void GetImageAsByteArray_WhenCannotReadNewImageFromCacheAndCannotReadFromWebCam_ReturnCurrentImageFromCache(int userId, int cacheUpdaterUserId)
        {
            // Arrange
            const string userUtc = "some_UTC_value";
            _dateTimeProvider.DateTimeNow.Returns(new DateTime(2019, 05, 01, 15, 30, 30, 100));
            _imageFromCacheService.WaitBeforeGettingNextImage(Arg.Any<int>(), Arg.Any<DateTime>()).Returns(0);

            _imageFromCacheService.GetNewImageAsByteArray(Arg.Any<int>(), Arg.Any<DateTime>()).Returns((byte[]) null);
            var currentImageArrayFromCache = new byte[20];
            _imageFromCacheService.GetCurrentImageAsByteArray().Returns(currentImageArrayFromCache);

            _cacheUpdater.Update(userId: cacheUpdaterUserId, lastUpdate: _dateTimeProvider.DateTimeNow);

            var imageFromWebCam = new Bitmap(1, 1);
            byte[] imageArrayFromWebCam = ImageHelper.ConvertImageToByteArray(imageFromWebCam);
            _imageFromWebCamService.GetImage(Arg.Any<string>()).Returns(imageFromWebCam);

            IImageProviderService imageProviderService = CreateValidImageProviderService();


            // Act
            bool canReadImageFromWebCam = imageProviderService.CanReadImageFromWebCam(userId, _dateTimeProvider.DateTimeNow);
            byte[] imageAsByteArray = imageProviderService.GetImageAsByteArray(userId, userUtc);

            // Assert 
            Assert.That(canReadImageFromWebCam, Is.False);
            Assert.That(imageAsByteArray, Is.Not.Null);

            Assert.That(imageAsByteArray.Length, Is.EqualTo(currentImageArrayFromCache.Length));
            Assert.That(imageAsByteArray.Length, Is.Not.EqualTo(imageArrayFromWebCam.Length));
            
            Assert.That(_cacheUpdater.UserId, Is.Not.EqualTo(userId));
        }


        // Helpers
        private IImageProviderService CreateValidImageProviderService()
        {
            return new ImageProviderService(
                imageFromCacheService: _imageFromCacheService,
                imageFromWebCamService: _imageFromWebCamService,
                dateTimeProvider: _dateTimeProvider,
                logger: _logger,
                cacheUpdaterExpirationMilliSec: _cacheUpdaterExpirationMilliSec,
                imageErrorLogoUrl: "some_value",
                lastCacheAccess: DateTime.Now,
                cacheUpdater: _cacheUpdater
                );
        }

        private DateTime RequestTime(int delayMilliSec)
        {
            DateTime requestTime;
            switch (delayMilliSec)
            {
                case Int32.MinValue:
                    requestTime = DateTime.MinValue;
                    break;
                case Int32.MaxValue:
                    requestTime = DateTime.MaxValue;
                    break;
                default:
                    requestTime = _dateTimeProvider.DateTimeNow.AddMilliseconds(delayMilliSec);
                    break;
            }

            return requestTime;
        }

    }
}