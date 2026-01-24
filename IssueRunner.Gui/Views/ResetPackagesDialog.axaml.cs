using Avalonia.Controls;
using Avalonia.ReactiveUI;
using IssueRunner.Gui.ViewModels;

namespace IssueRunner.Gui.Views;

/// <summary>
/// Dialog for resetting packages.
/// </summary>
public partial class ResetPackagesDialog : ReactiveWindow<ResetPackagesViewModel>
{
    public ResetPackagesDialog() : this(new ResetPackagesViewModel())
    {
    }

    public ResetPackagesDialog(ResetPackagesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.SetDialogWindow(this);
    }
}
