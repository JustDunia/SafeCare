using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace SafeCare.Utils
{
    /// <summary>
    /// Reads <see cref="DisplayAttribute"/> labels so that the Polish text shown in the UI
    /// lives next to the enum member it describes rather than in the markup.
    /// </summary>
    public static class DisplayHelper
    {
        extension<T>(T value) where T : Enum
        {
            /// <summary>
            /// Returns the member's <see cref="DisplayAttribute.Name"/>, falling back to the
            /// member name when it carries no attribute.
            /// </summary>
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
