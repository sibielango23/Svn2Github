using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SVN2GitHub.Services;
using SVN2GitHub.Models;

namespace SVN2GitHub.ViewModels
{
    public partial class NewMigrationViewModel : ObservableObject
    {
        private readonly ApiService _apiService;
        private readonly NotificationService _notificationService;

        [ObservableProperty]
        private string _svnUrl = "";

        [ObservableProperty]
        private string _githubOrg = "";

        [ObservableProperty]
        private string _githubRepo = "";

        [ObservableProperty]
        private string _githubToken = "";

        [ObservableProperty]
        private string _trunkPath = "trunk";

        [ObservableProperty]
        private string _branchesPath = "branches";

        [ObservableProperty]
        private string _tagsPath = "tags";

        [ObservableProperty]
        private bool _cleanHistory = false;

        [ObservableProperty]
        private bool _isSubmitting = false;

        [ObservableProperty]
        private string? _currentMigrationId = null;

        public List<AuthorCreateRequest> PreparedAuthors { get; private set; } = new();
        public string? PreparedAuthorsSvnUrl { get; private set; }

        public NewMigrationViewModel()
        {
            _apiService = new ApiService();
            _notificationService = new NotificationService();
        }

        [RelayCommand]
        private async Task StartMigrationAsync()
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(SvnUrl))
            {
                _notificationService.ShowWarning("Please enter SVN URL");
                return;
            }

            if (string.IsNullOrWhiteSpace(GithubOrg))
            {
                _notificationService.ShowWarning("Please enter GitHub organization");
                return;
            }

            if (string.IsNullOrWhiteSpace(GithubRepo))
            {
                _notificationService.ShowWarning("Please enter GitHub repository name");
                return;
            }

            if (string.IsNullOrWhiteSpace(GithubToken))
            {
                _notificationService.ShowWarning("Please enter GitHub token");
                return;
            }

            if (PreparedAuthors.Count > 0 &&
                !string.Equals(PreparedAuthorsSvnUrl, SvnUrl, StringComparison.OrdinalIgnoreCase))
            {
                _notificationService.ShowWarning(
                    "The SVN URL changed after author mappings were prepared. Configure authors again before starting.");
                return;
            }

            // Confirm before starting
            var confirmed = _notificationService.ShowConfirmation(
                $"Start migration from:\n{SvnUrl}\n\nto:\n{GithubOrg}/{GithubRepo}?\n\n" +
                "This process may take several hours.",
                "Confirm Migration");

            if (!confirmed) return;

            IsSubmitting = true;

            try
            {
                var request = new MigrationRequest
                {
                    SvnUrl = SvnUrl,
                    GithubOrg = GithubOrg,
                    GithubRepo = GithubRepo,
                    GithubToken = GithubToken,
                    TrunkPath = TrunkPath,
                    BranchesPath = BranchesPath,
                    TagsPath = TagsPath,
                    CleanHistory = CleanHistory,
                    Authors = PreparedAuthors
                };

                var response = await _apiService.CreateMigrationAsync(request);
                CurrentMigrationId = response.Id;
                PreparedAuthors = new List<AuthorCreateRequest>();
                PreparedAuthorsSvnUrl = null;

                _notificationService.ShowSuccess(
                    $"Migration started successfully!\n\nID: {response.Id}\n\n" +
                    "You can monitor progress from the Dashboard.",
                    "Migration Started");

                // Clear form
                ClearForm();
            }
            catch (Exception ex)
            {
                _notificationService.ShowError(
                    $"Failed to start migration:\n{ex.Message}",
                    "Error");
            }
            finally
            {
                IsSubmitting = false;
            }
        }

        [RelayCommand]
        private void ClearForm()
        {
            SvnUrl = "";
            GithubOrg = "";
            GithubRepo = "";
            GithubToken = "";
            TrunkPath = "trunk";
            BranchesPath = "branches";
            TagsPath = "tags";
            CleanHistory = false;
            PreparedAuthors = new List<AuthorCreateRequest>();
            PreparedAuthorsSvnUrl = null;
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
            PreparedAuthorsSvnUrl = svnUrl;
        }
    }
}
