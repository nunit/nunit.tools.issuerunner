using Avalonia.Data.Converters;
using IssueRunner.Gui.ViewModels;
using System;
using System.Globalization;

namespace IssueRunner.Gui.Converters;

/// <summary>
/// Returns true when the value is <see cref="IssueViewMode.Diff"/> or when the value is
/// an <see cref="IssueListViewModel"/> whose ViewMode is Diff; used to show/hide the
/// Details column in the Issue List (header and row template).
/// </summary>
public class ViewModeIsDiffConverter : IValueConverter
{
    public static readonly ViewModeIsDiffConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IssueListViewModel listVm)
            return listVm.ViewMode == IssueViewMode.Diff;
        return value is IssueViewMode mode && mode == IssueViewMode.Diff;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
