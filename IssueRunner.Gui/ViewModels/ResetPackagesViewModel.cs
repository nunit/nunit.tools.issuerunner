using ReactiveUI;
using System.Reactive;
using Avalonia.Controls;

namespace IssueRunner.Gui.ViewModels;

/// <summary>
/// ViewModel for the Reset Packages dialog.
/// </summary>
public class ResetPackagesViewModel : ViewModelBase
{
    private string _statusText = "Ready to reset packages...";
    private int _issuesProcessed = 0;
    private bool _isRunning = false;
    private bool _canCancel = true;
    private double _progress = 0.0;
    private int _totalIssues = 0;
    private string _currentIssue = "";
    private Avalonia.Controls.Window? _dialogWindow;

    public ResetPackagesViewModel()
    {
        // Commands will be set before showing dialog
        CancelCommand = ReactiveCommand.Create(() => { });
        CloseCommand = ReactiveCommand.Create(() => CloseDialog());
    }

    /// <summary>
    /// Sets the dialog window reference for closing.
    /// </summary>
    public void SetDialogWindow(Avalonia.Controls.Window window)
    {
        _dialogWindow = window;
    }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; set; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public int IssuesProcessed
    {
        get => _issuesProcessed;
        set
        {
            if (SetProperty(ref _issuesProcessed, value))
            {
                UpdateProgress();
            }
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                CanCancel = value;
            }
        }
    }

    public bool CanCancel
    {
        get => _canCancel;
        set => SetProperty(ref _canCancel, value);
    }

    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public int TotalIssues
    {
        get => _totalIssues;
        set
        {
            if (SetProperty(ref _totalIssues, value))
            {
                UpdateProgress();
            }
        }
    }

    public string CurrentIssue
    {
        get => _currentIssue;
        set => SetProperty(ref _currentIssue, value);
    }

    public string ProgressText
    {
        get
        {
            if (TotalIssues > 0)
            {
                return $"{IssuesProcessed} / {TotalIssues} issues processed";
            }
            return IssuesProcessed > 0 ? $"{IssuesProcessed} issues processed" : "";
        }
    }

    private void UpdateProgress()
    {
        if (TotalIssues > 0)
        {
            Progress = (double)IssuesProcessed / TotalIssues * 100.0;
        }
        else
        {
            Progress = 0.0;
        }
        OnPropertyChanged(nameof(ProgressText));
    }

    public void Reset()
    {
        StatusText = "Ready to reset packages...";
        IssuesProcessed = 0;
        Progress = 0.0;
        TotalIssues = 0;
        IsRunning = false;
        CurrentIssue = "";
    }

    private void CloseDialog()
    {
        if (_dialogWindow != null)
        {
            _dialogWindow.Close();
        }
        else if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.Windows.OfType<Avalonia.Controls.Window>()
                .FirstOrDefault(w => w.DataContext == this);
            window?.Close();
        }
    }
}
