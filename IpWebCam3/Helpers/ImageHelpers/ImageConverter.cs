using System;
using System.Drawing;

namespace IpWebCam3.Helpers.ImageHelpers
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


    }
}