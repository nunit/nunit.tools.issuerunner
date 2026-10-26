using Avalonia.Controls;

namespace IssueRunner.Gui.Views;

public partial class ValidateSystemDialog : Window
{
    public ValidateSystemDialog(string resultText)
    {
        InitializeComponent();
        ResultsTextBlock.Text = resultText;
    }

    private void OnCloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}

