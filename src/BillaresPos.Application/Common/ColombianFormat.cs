using System.Globalization;

namespace BillaresPos.Application.Common;

public static class ColombianFormat
{
    private static readonly NumberFormatInfo Numbers = new() { NumberGroupSeparator = ".", NumberDecimalSeparator = "," };

    public static string Number(decimal value, string format = "N0") => value.ToString(format, Numbers);
}
