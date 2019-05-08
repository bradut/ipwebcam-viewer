using System.Collections.Generic;
using System.Linq;

namespace IpWebCam3.Helpers.Configuration
{
    public static class ConfigExtensionMethods
    {
        // For current object, return its properties of type string whose values are null or empty.
        public static IEnumerable<string> GetNullValuePropertyNames(this object obj)
        {
            return obj.GetType().GetProperties()
                .Where(propertyInfo => propertyInfo.PropertyType == typeof(string))
                .Select(propertyInfo => new
                    { propertyName = propertyInfo.Name, propertyValue = propertyInfo.GetValue(obj) })
                .Where(tuple => tuple.propertyValue == null || string.IsNullOrWhiteSpace(tuple.propertyValue.ToString()))
                .Select(tuple => tuple.propertyName.ToString());
        }
    }
}