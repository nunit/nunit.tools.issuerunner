using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using IssueRunner.Gui.ViewModels;

namespace IssueRunner.Gui.Views;

/// <summary>
/// Small non-modal dialog showing full diff details for an issue (Diff view).
/// </summary>
public partial class IssueDiffDetailsDialog : Window
{
    public IssueDiffDetailsDialog()
    {
        InitializeComponent();
    }

    public IssueDiffDetailsDialog(IssueListItem item) : this()
    {
        DataContext = item;
        Title = $"Diff details — Issue #{item.Number}";
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
