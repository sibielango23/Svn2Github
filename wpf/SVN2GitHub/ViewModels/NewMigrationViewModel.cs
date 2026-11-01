using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SVN2GitHub.Models;
using SVN2GitHub.Services;
using System.Text.RegularExpressions;
using System.Windows;
using SVN2GitHub.Views;
using SVN2GitHub.Shared;

namespace SVN2GitHub.ViewModels
{
    public partial class NewMigrationViewModel : ObservableObject
    {
        private readonly ApiService _apiService;
        private readonly NotificationService _notificationService;

        [ObservableProperty]
        private string _svnUrl = string.Empty;

        [ObservableProperty]
        private string _githubOrg = string.Empty;

        [ObservableProperty]
        private string _githubRepo = string.Empty;

        [ObservableProperty]
        private string _githubToken = string.Empty;

        [ObservableProperty]
        private string _trunkPath = "trunk";

        [ObservableProperty]
        private string _branchesPath = "branches";

        [ObservableProperty]
        private string _tagsPath = "tags";

        [ObservableProperty]
        private bool _cleanHistory;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PreviousStepCommand))]
        [NotifyCanExecuteChangedFor(nameof(NextStepCommand))]
        [NotifyCanExecuteChangedFor(nameof(StartMigrationCommand))]
        [NotifyCanExecuteChangedFor(nameof(CheckReadinessCommand))]
        [NotifyCanExecuteChangedFor(nameof(AssessMigrationCommand))]
        private bool _isSubmitting;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PreviousStepCommand))]
        [NotifyCanExecuteChangedFor(nameof(NextStepCommand))]
        [NotifyCanExecuteChangedFor(nameof(StartMigrationCommand))]
        private int _currentStep = 1;

        [ObservableProperty]
        private string _validationMessage = string.Empty;

        [ObservableProperty]
        private string? _currentMigrationId;

        public List<AuthorCreateRequest> PreparedAuthors { get; private set; } = new();
        public string? PreparedAuthorsSvnUrl { get; private set; }
        public int PreparedAuthorCount => PreparedAuthors.Count;
        public string AuthorMappingSummary => PreparedAuthors.Count == 0
            ? "No custom mapping saved yet. Review authors now or continue with generated placeholders."
            : $"{PreparedAuthors.Count} author mappings are ready for this source.";

        public NewMigrationViewModel()
        {
            _apiService = new ApiService();
            _notificationService = new NotificationService();
        }

        [RelayCommand(CanExecute = nameof(CanGoBack))]
        private void PreviousStep()
        {
            ValidationMessage = string.Empty;
            CurrentStep = Math.Max(1, CurrentStep - 1);
        }

        [RelayCommand(CanExecute = nameof(CanCheckReadiness))]
        private void CheckReadiness()
        {
            new ReadinessWindow(CleanHistory)
            {
                Owner = Application.Current.MainWindow
            }.ShowDialog();
        }

        private bool CanCheckReadiness() => !IsSubmitting;

        [RelayCommand(CanExecute = nameof(CanCheckReadiness))]
        private void AssessMigration()
        {
            if (!ValidateSource() || !ValidateDestination())
            {
                return;
            }

            if (PreparedAuthors.Count > 0 &&
                !string.Equals(PreparedAuthorsSvnUrl, SvnUrl, StringComparison.OrdinalIgnoreCase))
            {
                ValidationMessage = "Review author mappings again after changing the SVN URL.";
                return;
            }

            if (!_notificationService.ShowConfirmation(
                "Assessment will contact SVN and github.com using cached SVN credentials and your GitHub token. " +
                "It reads visible SVN history and the current trunk inventory; it does not import, create, or push a repository. " +
                "Large repositories may take several minutes. Continue?", "Read-only migration assessment"))
            {
                return;
            }

            new ReadinessWindow(CleanHistory, new MigrationRequest
            {
                SvnUrl = SvnUrl.Trim(), GithubOrg = GithubOrg.Trim(), GithubRepo = GithubRepo.Trim(),
                GithubToken = GithubToken, TrunkPath = TrunkPath.Trim(), BranchesPath = BranchesPath.Trim(),
                TagsPath = TagsPath.Trim(), CleanHistory = CleanHistory, Authors = PreparedAuthors
            }) { Owner = Application.Current.MainWindow }.ShowDialog();
        }

        [RelayCommand(CanExecute = nameof(CanGoNext))]
        private void NextStep()
        {
            ValidationMessage = string.Empty;
            if (CurrentStep == 1 && !ValidateSource())
            {
                return;
            }

            if (CurrentStep == 2 && !ValidateDestination())
            {
                return;
            }

            CurrentStep = Math.Min(3, CurrentStep + 1);
        }

        [RelayCommand(CanExecute = nameof(CanStart))]
        private async Task StartMigrationAsync()
        {
            if (!ValidateSource() || !ValidateDestination())
            {
                return;
            }

            if (PreparedAuthors.Count > 0 &&
                !string.Equals(PreparedAuthorsSvnUrl, SvnUrl, StringComparison.OrdinalIgnoreCase))
            {
                ValidationMessage =
                    "The SVN URL changed after author mappings were prepared. Review the mappings again before starting.";
                return;
            }

            var historyWarning = CleanHistory
                ? "\n\nHistory cleaning is enabled and will rewrite commit IDs."
                : string.Empty;
            var confirmed = _notificationService.ShowConfirmation(
                $"Source: {SvnUrl}\nDestination: {GithubOrg}/{GithubRepo}\n" +
                $"Author mappings: {(PreparedAuthors.Count == 0 ? "generated defaults" : PreparedAuthors.Count)}" +
                $"{historyWarning}\n\nThe migration may take several hours. Completed stages are checkpointed and can be resumed after a failure. If the SVN import itself is interrupted, that import must be repeated. Continue?",
                "Review migration");

            if (!confirmed)
            {
                return;
            }

            IsSubmitting = true;
            ValidationMessage = string.Empty;

            try
            {
                var request = new MigrationRequest
                {
                    SvnUrl = SvnUrl.Trim(),
                    GithubOrg = GithubOrg.Trim(),
                    GithubRepo = GithubRepo.Trim(),
                    GithubToken = GithubToken,
                    TrunkPath = TrunkPath.Trim(),
                    BranchesPath = BranchesPath.Trim(),
                    TagsPath = TagsPath.Trim(),
                    CleanHistory = CleanHistory,
                    Authors = PreparedAuthors
                };

                var response = await _apiService.CreateMigrationAsync(request);
                CurrentMigrationId = response.Id;
                _notificationService.ShowSuccess(
                    $"Your migration was queued.\n\nID: {response.Id}\n\nOpen the dashboard to follow its progress.",
                    "Migration queued");
                ClearForm();
            }
            catch (Exception exception)
            {
                ValidationMessage = "The migration could not be queued. Check the API is running, then try again.";
                _notificationService.ShowError(
                    $"Failed to start migration:\n{exception.Message}",
                    "Migration not started");
            }
            finally
            {
                IsSubmitting = false;
            }
        }

        [RelayCommand]
        private void ClearForm()
        {
            ValidationMessage = string.Empty;
            CurrentStep = 1;
            SvnUrl = string.Empty;
            GithubOrg = string.Empty;
            GithubRepo = string.Empty;
            GithubToken = string.Empty;
            TrunkPath = "trunk";
            BranchesPath = "branches";
            TagsPath = "tags";
            CleanHistory = false;
            PreparedAuthors = new List<AuthorCreateRequest>();
            PreparedAuthorsSvnUrl = null;
            NotifyAuthorMappingChanged();
        }

        public void SetPreparedAuthors(IEnumerable<Author> authors, string svnUrl)
        {
            PreparedAuthors = authors.Select(author => new AuthorCreateRequest
            {
                SvnUsername = author.SvnUsername,
                GitName = author.GitName,
                GitEmail = author.GitEmail,
                Verified = author.Verified
            }).ToList();
            PreparedAuthorsSvnUrl = SvnUrlNormalizer.Normalize(svnUrl);
            NotifyAuthorMappingChanged();
        }

        private bool CanGoBack() => CurrentStep > 1 && !IsSubmitting;
        private bool CanGoNext() => CurrentStep < 3 && !IsSubmitting;
        private bool CanStart() => CurrentStep == 3 && !IsSubmitting;

        private bool ValidateSource()
        {
            if (!NormalizeSourceUrl())
            {
                return false;
            }

            if (new[] { TrunkPath, BranchesPath, TagsPath }
                .Any(path => string.IsNullOrWhiteSpace(path) ||
                             path.Contains('\n') ||
                             path.Contains('\r')))
            {
                ValidationMessage =
                    "Trunk, branches, and tags paths are required. Use paths relative to the repository URL.";
                return false;
            }

            return true;
        }

        private bool ValidateDestination()
        {
            if (!Regex.IsMatch(GithubOrg.Trim(),
                    @"\A[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?\z"))
            {
                ValidationMessage = "Enter a valid GitHub organization or account name.";
                return false;
            }

            if (!Regex.IsMatch(GithubRepo.Trim(), @"\A[A-Za-z0-9._-]{1,100}\z") ||
                GithubRepo.Trim() is "." or "..")
            {
                ValidationMessage =
                    "Enter a valid repository name using letters, numbers, dots, hyphens, or underscores.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(GithubToken))
            {
                ValidationMessage =
                    "Enter a GitHub token with permission to create and push to the destination repository.";
                return false;
            }

            return true;
        }

        public bool NormalizeSourceUrl()
        {
            if (!SvnUrlNormalizer.TryNormalize(SvnUrl, out var normalized))
            {
                ValidationMessage = SvnUrlNormalizer.ValidationMessage;
                return false;
            }

            SvnUrl = normalized;
            return true;
        }

        private void NotifyAuthorMappingChanged()
        {
            OnPropertyChanged(nameof(PreparedAuthorCount));
            OnPropertyChanged(nameof(AuthorMappingSummary));
        }
    }
}
