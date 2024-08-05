using System.ComponentModel;
using System.Reflection;

namespace NitroxDiscordBot.Core.Extensions;

public static class EnumExtensions
{
    public static string GetDescriptionOrName<TEnum>(this TEnum enumValue) where TEnum : Enum
    {
        FieldInfo fi = enumValue.GetType().GetField(enumValue.ToString());
        if (fi != null && fi.GetCustomAttributes(typeof(DescriptionAttribute), false) is DescriptionAttribute[] attributes && attributes.Any())
        {
            return attributes.First().Description;
        }
        return enumValue.ToString();
    }
}