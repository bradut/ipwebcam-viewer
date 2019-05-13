using IpWebCam3.Helpers.Logging;
using IpWebCam3.Helpers.TimeHelpers;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace IpWebCam3.Helpers.ImageHelpers
{
    public class ImageFileWriter
    {
        private static readonly object LockImageFileWriter = new object();

        public static void WriteImageToFile(Image image, DateTime dateTime,
                                            string imageDirectory, IMiniLogger logger,
                                            int userId, string userIp,
                                            bool roundSecondsToZero = true)
        {
            if (image == null) return;
            if (string.IsNullOrWhiteSpace(imageDirectory)) return;

            dateTime = RoundSecondsToZero(dateTime, roundSecondsToZero);

            string dateTimeCompact = DateTimeFormatter.ConvertTimeToCompactString(dateTime: dateTime, withMilliSeconds: false);

            WriteImageToFile(image, imageDirectory, dateTimeCompact, logger, userId, userIp);
        }

        private static DateTime RoundSecondsToZero(DateTime dateTime, bool roundSecondsToZero)
        {
            if (roundSecondsToZero && dateTime.Second != 0)
            {
                dateTime =
                new DateTime(dateTime.Year, dateTime.Month, dateTime.Day,
                             dateTime.Hour, dateTime.Minute, 00);
            }

            return dateTime;
        }

        private static void WriteImageToFile(Image image, string snapshotImagePath, string dateTimeCompact,
                                             IMiniLogger logger, int userId, string userIp)
        {
            TryCreateDir(snapshotImagePath, logger, userId, userIp);

            string imagePath = snapshotImagePath + "img_" + dateTimeCompact + ".jpg";

            if (File.Exists(imagePath))
                return;

            lock (LockImageFileWriter)
            {
                if (File.Exists(imagePath))
                    return;

                try
                {
                    image.Save(imagePath, ImageFormat.Jpeg);
                }
                catch (System.Runtime.InteropServices.ExternalException sriException)
                {
                    logger?.LogError($"{nameof(WriteImageToFile)}(): {sriException.Message} filepath: {imagePath}", userId, userIp);

                    TrySaveImageAgain(image, logger, imagePath, userId, userIp);
                }
                catch (Exception e)
                {
                    logger?.LogError($"{nameof(WriteImageToFile)}(): {e.Message} filepath: {imagePath}", userId, userIp);
                }
            }
        }

        private static void TrySaveImageAgain(Image image, IMiniLogger logger, string imagePath, int userId, string userIp)
        {
            try
            {
                if (File.Exists(imagePath)) File.Delete(imagePath);
                System.Threading.Thread.Sleep(1000);
                SaveImage(image, imagePath);
            }
            catch (Exception e)
            {
                logger?.LogError($"{nameof(WriteImageToFile)}->{nameof(TrySaveImageAgain)}(): " +
                                 $"{e.Message} filepath: {imagePath}", userId, userIp);
            }
        }

        private static void TryCreateDir(string snapshotImagePath, IMiniLogger logger, int userId, string userIp)
        {
            string directoryName = Path.GetDirectoryName(snapshotImagePath);
            try
            {
                if (string.IsNullOrWhiteSpace(directoryName)) return;

                if (!Directory.Exists(directoryName)) Directory.CreateDirectory(directoryName);
            }
            catch (Exception ex)
            {
                logger?.LogError($"{nameof(WriteImageToFile)}( ): {ex.Message} directoryName: {directoryName}", userId, userIp);
                throw;
            }
        }

        // A safer way to save image to a file
        // https://stackoverflow.com/questions/15862810/a-generic-error-occurred-in-gdi-in-bitmap-save-method
        public static void SaveImage(Image image, string imagePath)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var fileStream = new FileStream(imagePath, FileMode.Create, FileAccess.ReadWrite))
                {
                    image.Save(memoryStream, ImageFormat.Jpeg);
                    byte[] bytes = memoryStream.ToArray();
                    fileStream.WriteAsync(bytes, 0, bytes.Length);
                }
            }
        }

        public static void SaveImageSnapshot(byte[] imageAsBytes, IDateTimeProvider dateTimeProvider, 
                                             string snapshotImagePath, IMiniLogger logger, 
                                             int userId, string userIp)
        {
            if (!IsTimeToWriteAPicture(dateTimeProvider))
                return;

            Image image = ImageHelper.ConvertByteArrayToImage(imageAsBytes);
            WriteImageToFile(image, dateTimeProvider.DateTimeNow, snapshotImagePath, logger, userId, userIp);
        }

        // ToDo: read these values from config and move this method out of here
        private static bool IsTimeToWriteAPicture(IDateTimeProvider dateTimeProvider)
        {
            return
                dateTimeProvider.DateTimeNow.Second >= 00 && // provide an interval of a few seconds to avoid
                dateTimeProvider.DateTimeNow.Second <= 03 // missing time ending in '00' seconds
            &&
            (
                dateTimeProvider.DateTimeNow.Minute == 00 ||
                dateTimeProvider.DateTimeNow.Minute == 15 ||
                dateTimeProvider.DateTimeNow.Minute == 30 ||
                dateTimeProvider.DateTimeNow.Minute == 45);
        }
    }
}