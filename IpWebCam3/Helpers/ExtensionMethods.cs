using System.Collections.Generic;
using System.Linq;

namespace IpWebCam3.Helpers
{
    public static class ExtensionMethods
    {
        public static IEnumerable<string> GetNullValuePropertyNames(this object obj)
        {
            return obj.GetType().GetProperties()
                .Where(propertyInfo => propertyInfo.PropertyType == typeof(string))
                .Select(propertyInfo => new
                    { propertyName = propertyInfo.Name, propertyValue = propertyInfo.GetValue(obj) })
                .Where(tuple => tuple.propertyValue == null)
                .Select(tuple => tuple.propertyName.ToString());
        }
    }
}