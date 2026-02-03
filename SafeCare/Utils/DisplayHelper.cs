using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace SafeCare.Utils
{
    public static class DisplayHelper
    {
        extension<T>(T value) where T : Enum
        {
            public string GetDisplayName()
            {
                var field = value.GetType().GetField(value.ToString());
                var attribute = (DisplayAttribute?)Attribute.GetCustomAttribute(field!, typeof(DisplayAttribute));

                return attribute?.Name ?? value.ToString();
            }
        }

        public static string GetDisplayName(Type type)
        {
            return type.GetCustomAttribute<DisplayAttribute>()?.Name
                ?? type.Name;
        }
    }
}
