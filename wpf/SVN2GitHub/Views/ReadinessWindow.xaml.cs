using System.Windows;
using SVN2GitHub.ViewModels;
using SVN2GitHub.Models;

namespace SVN2GitHub.Views;

public partial class ReadinessWindow : Window
{
    public ReadinessWindow(bool cleanHistory = false, MigrationRequest? assessmentRequest = null)
    {
        InitializeComponent();
        var viewModel = new ReadinessViewModel(cleanHistory, assessmentRequest);
        if (assessmentRequest is not null)
        {
            Title = "Read-only migration assessment";
        }
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.RefreshCommand.Execute(null);
        Closed += (_, _) => viewModel.Dispose();
    }
}
