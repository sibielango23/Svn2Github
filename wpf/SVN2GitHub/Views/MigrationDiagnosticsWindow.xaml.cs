using System.Windows;
using SVN2GitHub.Models;
using SVN2GitHub.ViewModels;

namespace SVN2GitHub.Views;

public partial class MigrationDiagnosticsWindow : Window
{
    public MigrationDiagnosticsWindow(MigrationDetail migration)
    {
        InitializeComponent();
        var viewModel = new MigrationDiagnosticsViewModel(migration);
        DataContext = viewModel;
        Closed += (_, _) => viewModel.ExportReportCommand.Cancel();
    }
}
