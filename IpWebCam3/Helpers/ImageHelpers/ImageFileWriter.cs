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
                                            bool roundSecondsToZero = true)
        {
            if (image == null) return;
            if (string.IsNullOrWhiteSpace(imageDirectory)) return;

            dateTime = RoundSecondsToZero(dateTime, roundSecondsToZero);

            string dateTimeCompact = DateTimeFormatter.ConvertTimeToCompactString(dateTime: dateTime, withMilliSeconds:false);

            WriteImageToFile(image, imageDirectory, dateTimeCompact, logger);
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

        private static void WriteImageToFile(Image image, string snapshotImagePath, string dateTimeCompact, IMiniLogger logger)
        {
            TryCreateDir(snapshotImagePath, logger);

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
                    logger?.LogError($"{nameof(WriteImageToFile)}(): {sriException.Message} filepath: {imagePath}");

                    TrySaveImageAgain(image, logger, imagePath);
                }
                catch (Exception e)
                {
                    logger?.LogError($"{nameof(WriteImageToFile)}(): {e.Message} filepath: {imagePath}");
                }
            }
        }

        private static void TrySaveImageAgain(Image image, IMiniLogger logger, string imagePath)
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
                                 $"{e.Message} filepath: {imagePath}");
            }
        }

        private static void TryCreateDir(string snapshotImagePath, IMiniLogger logger)
        {
            string directoryName = Path.GetDirectoryName(snapshotImagePath);
            try
            {
                if (string.IsNullOrWhiteSpace(directoryName)) return;

                if (!Directory.Exists(directoryName)) Directory.CreateDirectory(directoryName);
            }
            catch (Exception ex)
            {
                logger?.LogError($"{nameof(WriteImageToFile)}( ): {ex.Message} directoryName: {directoryName}");
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

    }
}