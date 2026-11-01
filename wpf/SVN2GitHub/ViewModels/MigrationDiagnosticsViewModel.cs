using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Windows;
using SVN2GitHub.Models;
using SVN2GitHub.Services;

namespace SVN2GitHub.ViewModels;

public partial class MigrationDiagnosticsViewModel : ObservableObject
{
    private readonly MigrationDetail _migration;
    private readonly string _report;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredLogs))]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Snapshot of recorded logs. Review confidential details before sharing.";

    public string Summary { get; }
    public string VerificationSummary => _migration.VerificationReport is { } report
        ? MigrationSupportService.BuildCheckReport(report)
        : "No verification report is recorded for this attempt. Older jobs may predate report capture. A missing report is not a pass.";
    public string FilteredLogs => string.Join(Environment.NewLine,
        _migration.Logs
            .Where(log => log.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(AiAssistantService.RedactSensitiveData));

    public MigrationDiagnosticsViewModel(MigrationDetail migration)
    {
        _migration = migration;
        Summary = MigrationSupportService.BuildSnapshot(migration);
        _report = MigrationSupportService.BuildReport(migration);
    }

    [RelayCommand]
    private void CopyReport()
    {
        try
        {
            Clipboard.SetText(_report);
            StatusMessage = "Complete redacted report copied. Review it before sharing; log filtering does not change the export.";
        }
        catch (System.Runtime.InteropServices.ExternalException exception)
        {
            StatusMessage = $"Could not copy the report: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportReportAsync(CancellationToken cancellationToken)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export redacted migration support report",
            Filter = "Text report (*.txt)|*.txt",
            FileName = "migration-support-report.txt",
            DefaultExt = ".txt"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(dialog.FileName, _report, Encoding.UTF8, cancellationToken);
            StatusMessage = "Complete report exported. Review confidential details before sharing; automated redaction is best-effort.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Could not export the report: {exception.Message}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "Report export cancelled. Do not share an incomplete file.";
        }
    }
}
