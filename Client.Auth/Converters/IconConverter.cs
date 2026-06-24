using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Client.Auth.Converters;

public class FileIconConverter : IValueConverter
{
    public static readonly FileIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string ext)
        {
            return ext.ToLowerInvariant() switch
            {
                "docx" or "doc" => "IconFileDoc",
                "xlsx" or "xls" => "IconFileXls",
                "pptx" or "ppt" => "IconFilePpt",
                "pdf" => "IconFilePdf",
                "png" or "jpg" or "jpeg" or "gif" or "webp" => "IconImage",
                "json" or "xml" or "cs" or "js" => "IconFileDoc",
                _ => "IconFileDoc"
            };
        }
        return "IconFileDoc";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToPasswordCharConverter : IValueConverter
{
    public static readonly BoolToPasswordCharConverter Instance = new();

    /// <summary>
    /// true  → '\0' (no masking, visible text)
    /// false → '●'  (masked)
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? '\0' : '●';

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class InvertBoolConverter : IValueConverter
{
    public static readonly InvertBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? "#22C55E" : "#EF4444";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToValidationClassConverter : IValueConverter
{
    public static readonly BoolToValidationClassConverter Instance = new();

    /// <summary>
    /// true  → "validation-ok"
    /// false → "validation-err"
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? "validation-ok" : "validation-err";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}