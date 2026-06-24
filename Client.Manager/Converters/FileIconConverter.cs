using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Client.Manager.Converters;

/// <summary>
/// Maps file extension to a color string for Path icon fills
/// </summary>
public class FileExtToColorConverter : IValueConverter
{
    public static readonly FileExtToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString()?.ToLowerInvariant() switch
        {
            "docx" or "doc" => "#3B82F6",
            "xlsx" or "xls" => "#22C55E",
            "pptx" or "ppt" => "#F59E0B",
            "pdf" => "#EF4444",
            "png" or "jpg" or "jpeg" or "gif" or "webp" => "#A855F7",
            "json" or "xml" or "cs" or "js" => "#64748B",
            _ => "#94A3B8"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Maps file extension to icon geometry key
/// </summary>
public class FileExtToIconConverter : IValueConverter
{
    public static readonly FileExtToIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString()?.ToLowerInvariant() switch
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

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}