using System;
using System.Drawing;
using System.IO;

namespace IpWebCam3.Helpers
{
    public class ImageHelper
    {
        public static byte[] ConvertImageToByteArray(Image image)
        {
            byte[] imageBytes;

            using (image)
            {
                using (var memoryStream = new MemoryStream())
                {
                    image.Save(memoryStream, image.RawFormat);
                    imageBytes = memoryStream.ToArray();
                }
            }

            return imageBytes;
        }

        public static Image ConvertByteArrayToImage(byte[] imageAsByteArray)
        {
            using (var ms = new MemoryStream(imageAsByteArray))
            {
                return Image.FromStream(ms);
            }
        }

        public static string ConvertImageToBase64String(Image image)
        {
            byte[] imageBytes = ConvertImageToByteArray(image);
            string base64String = Convert.ToBase64String(imageBytes);

            return base64String;
        }
    }

}