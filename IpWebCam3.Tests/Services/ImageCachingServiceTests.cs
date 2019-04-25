using IpWebCam3.Helpers.Cache;
using IpWebCam3.Services;
using NUnit.Framework;
using System;

namespace IpWebCam3.Tests.Services
{
    [TestFixture]
    public class ImageCachingServiceTests
    {

        [Test]
        public void Constructor_InvalidNullImageCache_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ImageCachingService(
                                                                imageCache: null,
                                                                logger: null,
                                                                cacheLifeTimeMilliSec: 2500,
                                                                framesPerSecond: 10));
        }

        [TestCase(-1, -1)]
        [TestCase(0, 0)]
        [TestCase(1, 1)]
        public void Constructor_InvalidParams_Throws(int expiration, int fps)
        {
            // Arrange
            var imageCache = new ImageCache();
            Assert.Throws<ArgumentException>(() => new ImageCachingService(imageCache, null, expiration, fps));
        }

        [TestCase(2, 2)]
        [TestCase(2000, 5)]
        public void Constructor_ValidParams_HappyPath(int expiration, int fps)
        {
            // Arrange
            var imageCache = new ImageCache();

            // Act
            var imgCacheSvc = new ImageCachingService(imageCache, null, expiration, fps);

            // Assert
            Assert.IsNotNull(imgCacheSvc);
        }


        [Test]
        public void GetImageAsByteArray_NoParamsAndImageCacheHasNoData_ReturnsNull()
        {
            // Arrange
            var imageCache = new ImageCache();

            // Act
            var imgCacheSvc = new ImageCachingService(imageCache);

            // Assert
            Assert.IsNull(imgCacheSvc.GetImageAsByteArray());
        }

        [Test]
        public void GetImageAsByteArray_NoParamsImageCacheHasData_ReturnsNotNull()
        {
            // Arrange
            var imageCache = new ImageCache();
            imageCache.UpdateImage(new byte[2], 1, DateTime.MaxValue);

            // Act
            var imgCacheSvc = new ImageCachingService(imageCache);


            // Assert
            Assert.IsNotNull(imgCacheSvc.GetImageAsByteArray());
        }


        private const string StrCacheLastUpdate = "2019-03-31 14:50:00.123";

        [TestCase(1, StrCacheLastUpdate, 2000, 5, true, 99, 1000, true)]   // Success: updaterId != readerId and readerDelay < lifetime
        [TestCase(1, StrCacheLastUpdate, 2000, 5, false, 99, 1000, false)] // Failure: cache has no data
        [TestCase(99, StrCacheLastUpdate, 2000, 5, true, 99, 1000, false)] // Failure: updaterId == readerId
        [TestCase(1, StrCacheLastUpdate, 2000, 5, true, 99, 5000, false)]  // Failure: readerDelay > lifetime
        public void GetImageAsByteArray_WithParamsAndFreshData_ReturnNotNull(int cacheUpdaterUserId, string strCacheLastUpdate, int cacheLifeTime,
                                                                             int cacheFps, bool cacheHasData,
                                                                             int cacheReaderUserId, int cacheReaderDelay, bool expectedSuccess)
        {
            // Arrange
            DateTime cacheLastUpdate = DateTime.ParseExact(s: strCacheLastUpdate, format: "yyyy-MM-dd HH:mm:ss.fff", provider: null);
            DateTime timeWhenCacheIsRead = cacheLastUpdate.AddMilliseconds(cacheReaderDelay);

            byte[] imageBytes = cacheHasData ? new byte[2] : null;
            ImageCache imageCache = CreateImageCache(imageBytes, cacheUpdaterUserId, cacheLastUpdate);
            var cachingService = new ImageCachingService(imageCache: imageCache, logger: null, cacheLifeTimeMilliSec: cacheLifeTime, framesPerSecond: cacheFps);

            // Act
            byte[] imgBytes = cachingService.GetImageAsByteArray(userId: cacheReaderUserId, timeRequested: timeWhenCacheIsRead);
            bool resultIsNotNull = imgBytes != null;

            // Assert
            Assert.AreEqual(resultIsNotNull, expectedSuccess);
        }


        [TestCase(99, StrCacheLastUpdate, 2000, 5, true, 99, 1000, 0)]  // No wait time: updaterId == readerId
        [TestCase(10, StrCacheLastUpdate, 2000, 5, true, 99, 1000, 0)]  // No wait time: FPS time btw images (200 ms) <  cacheReaderDelay = (1000 ms)
        [TestCase(10, StrCacheLastUpdate, 2000, 5, true, 99, 50, 0)]    // No wait time (0 ms) = FPS time btw images (200 ms) > cacheReaderDelay ( 50 ms)
        [TestCase(10, StrCacheLastUpdate, 2000, 5, true, 99, 125, 0)]   // No wait time (  ms) = FPS time btw images (200 ms) > cacheReaderDelay (125 ms)
        [TestCase(10, StrCacheLastUpdate, 2000, 5, true, 99, 225, 175)] // Wait time ( 75 ms) = FPS time btw images (200 ms) - cacheReaderDelay (225 = 1 frame *200 ms/frame + k25 ms)
        [TestCase(10, StrCacheLastUpdate, 2000, 5, true, 99, 999, 1)]   // Wait time ( 1 ms) = FPS time btw images (200 ms) - cacheReaderDelay (999 = 4 frames *200+199 ms)
        [TestCase(10, StrCacheLastUpdate, 2000, 5, true, 99, 1000, 0)]  // Wait time ( 0 ms) = FPS time btw images (200 ms) - cacheReaderDelay (1000 = 5*200 ms)
        [TestCase(10, StrCacheLastUpdate, 2000, 5, true, 99, 1850, 150)] // Wait time ( 150 ms) = FPS time btw images (200 ms) - cacheReaderDelay (1850 = 9 *200 + 50 ms)
        [TestCase(10, StrCacheLastUpdate, 2000, 5, true, 99, 2050, 150)] // Wait time ( 150 ms) = FPS time btw images (200 ms) - cacheReaderDelay (1850 = 10*200 + 50 ms)
        [TestCase(10, StrCacheLastUpdate, 2000, 5, true, 99, 990 * 200 + 100, 100)] // Wait time ( 100 ms) = FPS time btw images (200 ms) - cacheReaderDelay (99100 = 990 frames *200 + 100 ms)
        [TestCase(10, StrCacheLastUpdate, 2000, 5, true, 99, 1000 * 200 * 102 + 50, 0)] // Wait time ( 0 ms) = cacheReaderDelay > 1000 frames * 200 ms/frame
        [TestCase(10, StrCacheLastUpdate, 2000, 5, true, 99, 0, 0)]     // No wait time:  cacheReaderDelay = 0 ms
        [TestCase(10, StrCacheLastUpdate, 2000, 5, true, 99, -1, 0)]    // No wait time:  cacheReaderDelay < 0 ms
        public void WaitBeforeGettingNextImageTest_AnyParameters_ReturnExpectedResult(int cacheUpdaterUserId, string strCacheLastUpdate, int cacheLifeTime,
                                                   int cacheFps, bool cacheHasData,
                                                   int cacheReaderUserId, int cacheReaderDelay, int expectedWaitTimeMilliSec)
        {
            // Arrange
            DateTime cacheLastUpdate = DateTime.ParseExact(s: strCacheLastUpdate, format: "yyyy-MM-dd HH:mm:ss.fff", provider: null);
            DateTime timeWhenCacheIsRead = cacheLastUpdate.AddMilliseconds(cacheReaderDelay);
            byte[] imageBytes = cacheHasData ? new byte[2] : null;

            ImageCache imageCache = CreateImageCache(imageBytes, cacheUpdaterUserId, cacheLastUpdate);
            var cachingService = new ImageCachingService(imageCache: imageCache, logger: null, cacheLifeTimeMilliSec: cacheLifeTime, framesPerSecond: cacheFps);
            int fpsTimeBetweenTwoFramesMilliSec = 1000 / cacheFps;

            // Act
            int waitTimeMilliSec = cachingService.WaitBeforeGettingNextImage(userId: cacheReaderUserId, timeRequested: timeWhenCacheIsRead);

            // Assert
            Assert.That(waitTimeMilliSec, Is.EqualTo(expectedWaitTimeMilliSec));

            Assert.That(waitTimeMilliSec, Is.LessThanOrEqualTo(fpsTimeBetweenTwoFramesMilliSec));
        }



        [TestCase(10, StrCacheLastUpdate, 2000, 5, true, 99, 1, true)]  // Can update = new date is more recent
        [TestCase(10, StrCacheLastUpdate, 2000, 5, true, 99, 0, false)]  // Cannot update = new date is the same
        [TestCase(10, StrCacheLastUpdate, 2000, 5, true, 99, -1, false)]  // Cannot update = new date is in the past
        public void UpdateCachedImage_AnyParameters_ReturnExpectedResult(int oldUpdaterUserId, string strCacheLastUpdate, int cacheLifeTime,
            int cacheFps, bool cacheHasData,
            int newCacheUpdaterUserId, int newCacheUpdaterDelay, bool expectedSuccess)
        {
            // Arrange
            DateTime cacheLastUpdate = DateTime.ParseExact(s: strCacheLastUpdate, format: "yyyy-MM-dd HH:mm:ss.fff", provider: null);
            DateTime timeWhenCacheIsRead = cacheLastUpdate.AddMilliseconds(newCacheUpdaterDelay);
            var oldImageArraySize = 2;
            byte[] imageBytes = cacheHasData ? new byte[oldImageArraySize] : null;

            ImageCache imageCache = CreateImageCache(imageBytes, oldUpdaterUserId, cacheLastUpdate);
            var cachingService = new ImageCachingService(imageCache: imageCache, logger: null, cacheLifeTimeMilliSec: cacheLifeTime, framesPerSecond: cacheFps);
            var newImageArraySize = 44;

            // Act
            cachingService.UpdateCachedImage(new byte[newImageArraySize], newCacheUpdaterUserId, timeWhenCacheIsRead);

            // Assert
            bool cacheHasBeenUpdated = cachingService.GetImageAsByteArray().Length == newImageArraySize;
            Assert.That(cacheHasBeenUpdated, Is.EqualTo(expectedSuccess));
        }


        //Helpers
        private ImageCache CreateImageCache(byte[] imgBytes, int userId, DateTime dateUpdated)
        {
            var imageCache = new ImageCache();
            imageCache.UpdateImage(imgBytes, userId, dateUpdated);

            return imageCache;
        }
    }
}