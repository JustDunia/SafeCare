using System.ComponentModel.DataAnnotations;

namespace SafeCare.Utils
{
    public static class EnumExtensions
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
    }
}
