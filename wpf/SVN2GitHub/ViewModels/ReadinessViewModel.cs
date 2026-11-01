using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SVN2GitHub.Models;
using SVN2GitHub.Services;
using System.Windows;
using System.Text;
using System.IO;
using System.Net.Http;
using Microsoft.Win32;

namespace SVN2GitHub.ViewModels;

public partial class ReadinessViewModel(bool cleanHistory, MigrationRequest? assessmentRequest = null) : ObservableObject, IDisposable
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromMinutes(30) };
    private ApiService? _apiService;
    private ApiService Api => _apiService ??= new(httpClient: _client);
    private readonly CancellationTokenSource _lifetime = new();

    [ObservableProperty]
    private ReadinessReport? _report;

    [ObservableProperty]
    private string _statusMessage = "Check local tooling, writable storage, and free space before a long import.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isBusy;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = assessmentRequest is null
            ? "Checking local tools and storage. No SVN/GitHub network calls or repository changes."
            : "Reading SVN history/layout/inventory and GitHub access. No import, creation, or push. Close to cancel assessment.";
        try
        {
            Report = assessmentRequest is null
                ? await Api.CheckReadinessAsync(cleanHistory, _lifetime.Token)
                : await Api.AssessMigrationAsync(assessmentRequest, _lifetime.Token);
            StatusMessage = Report.HasBlockingIssues
                ? "Blocking issues found. Resolve failed checks before starting."
                : assessmentRequest is null
                    ? "Local checks passed. Review warnings; repository access and permissions are not verified."
                    : "Assessment finished. Review warnings and scope limits; this is not a migration acceptance certificate.";
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Report = null;
            StatusMessage = $"Readiness check failed: {exception.Message}. No readiness result is available.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRefresh() => !IsBusy && !_lifetime.IsCancellationRequested;

    [RelayCommand]
    private void CopyReport()
    {
        if (Report is null)
        {
            StatusMessage = "No report is available to copy.";
            return;
        }

        try
        {
            Clipboard.SetText(MigrationSupportService.BuildCheckReport(Report));
            StatusMessage = "Redacted report copied. Review confidential metadata before sharing.";
        }
        catch (System.Runtime.InteropServices.ExternalException exception)
        {
            StatusMessage = $"Report could not be copied: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportReportAsync()
    {
        if (Report is null)
        {
            StatusMessage = "No report is available to export.";
            return;
        }

        var text = MigrationSupportService.BuildCheckReport(Report);
        var dialog = new SaveFileDialog
        {
            Title = "Export migration assessment/readiness report",
            Filter = "Text report (*.txt)|*.txt", FileName = "migration-assessment.txt", DefaultExt = ".txt"
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            await File.WriteAllTextAsync(dialog.FileName, text, Encoding.UTF8, _lifetime.Token);
            StatusMessage = "Report exported. Review confidential metadata before sharing.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Report export failed: {exception.Message}";
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            StatusMessage = "Export cancelled. Do not share an incomplete file.";
        }
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _client.Dispose();
        if (assessmentRequest is not null)
        {
            assessmentRequest.GithubToken = string.Empty;
        }
        _lifetime.Dispose();
    }
}
