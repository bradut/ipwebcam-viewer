using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace IpWebCam3.Helpers
{
    public class ImageHelper
    {
        private static readonly ImageConverter ImageConverter = new ImageConverter();

        public static byte[] ConvertImageToByteArray(Image image)
        {
            var imageBytes = (byte[])ImageConverter.ConvertTo(image, typeof(byte[]));

            return imageBytes;
        }

        //https://stackoverflow.com/questions/3801275/how-to-convert-image-to-byte-array/16576471#16576471
        public static Image ConvertByteArrayToImage(byte[] imageAsByteArray)
        {
            var bitmap = (Bitmap)ImageConverter.ConvertFrom(imageAsByteArray);

            return bitmap;
        }

        public static string ConvertImageToBase64String(Image image)
        {
            byte[] imageBytes = ConvertImageToByteArray(image);
            string base64String = Convert.ToBase64String(imageBytes);

            return base64String;
        }

        public static void WriteImageToFile(Image image, string snapshotImagePath, string dateTimeCompact, MiniLogger logger)
        {
            TryCreateDir(snapshotImagePath, logger);

            string imagePath = snapshotImagePath + "img_" + dateTimeCompact + ".jpg";

            if (File.Exists(imagePath))
                return;

            try
            {
                image.Save(imagePath, ImageFormat.Jpeg);
            }
            catch (System.Runtime.InteropServices.ExternalException sriException)
            {
                logger?.LogError($"{nameof(WriteImageToFile)}(): {sriException.Message} filepath: {imagePath}");

                try
                {
                    if (File.Exists(imagePath)) File.Delete(imagePath);
                    System.Threading.Thread.Sleep(1000);
                    SaveImage(image, imagePath);
                }
                catch (Exception e)
                {
                     logger?.LogError($"{nameof(WriteImageToFile)}(): {e.Message} filepath: {imagePath}");
                }
            }
            catch (Exception e)
            {
                logger?.LogError($"{nameof(WriteImageToFile)}(): {e.Message} filepath: {imagePath}");
            }
        }

        private static void TryCreateDir(string snapshotImagePath, MiniLogger logger)
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

        // https://stackoverflow.com/questions/15862810/a-generic-error-occurred-in-gdi-in-bitmap-save-method
        public static void SaveImage(Image image, string imagePath)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var fileStream = new FileStream(imagePath, FileMode.Create, FileAccess.ReadWrite))
                {
                    image.Save(memoryStream, ImageFormat.Jpeg);
                    byte[] bytes = memoryStream.ToArray();
                    fileStream.Write(bytes, 0, bytes.Length);
                }
            }
        }

    }
}