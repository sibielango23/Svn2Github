using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using SVN2GitHub.Models;
using SVN2GitHub.Services;

namespace SVN2GitHub.Views
{
    public partial class AuthorMappingWindow : Window
    {
        private readonly ApiService _apiService;
        private readonly string _migrationId;
        private ObservableCollection<Author> _allAuthors = new();
        private ObservableCollection<Author> _filteredAuthors = new();
        private bool _hasChanges = false;

        public IReadOnlyList<Author> AllAuthors => _allAuthors;

        public ObservableCollection<Author> Authors
        {
            get => _filteredAuthors;
            set
            {
                _filteredAuthors = value;
                AuthorsDataGrid.ItemsSource = _filteredAuthors;
                UpdateAuthorCount();
            }
        }

        public AuthorMappingWindow(string migrationId)
        {
            InitializeComponent();
            _apiService = new ApiService();
            _migrationId = migrationId;
            DataContext = this;
            LoadAuthorsFromServer();
        }

        private async void LoadAuthorsFromServer()
        {
            try
            {
                ShowLoading("Loading existing authors...");

                var authors = await _apiService.GetAuthorsAsync(_migrationId);

                _allAuthors = new ObservableCollection<Author>(authors);
                Authors = new ObservableCollection<Author>(_allAuthors);

                if (authors.Count == 0)
                {
                    var result = MessageBox.Show(
                        "No authors found for this migration. Would you like to extract them from SVN now?",
                        "No Authors Found",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        await LoadAuthorsFromSvn();
                    }
                }

                HideLoading();
            }
            catch (Exception ex)
            {
                HideLoading();
                MessageBox.Show(
                    $"Failed to load authors: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void LoadAuthors_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will extract all authors from the SVN repository. This may take several minutes for large repositories. Continue?",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await LoadAuthorsFromSvn();
            }
        }

        private async Task LoadAuthorsFromSvn()
        {
            try
            {
                ShowLoading("Extracting authors from SVN repository...\nThis may take several minutes for large repositories.");

                // Trigger author extraction through API
                var extracted = await _apiService.ExtractAuthorsAsync(_migrationId);

                if (extracted != null && extracted.Count > 0)
                {
                    _allAuthors = new ObservableCollection<Author>(extracted);
                    Authors = new ObservableCollection<Author>(_allAuthors);

                    MessageBox.Show(
                        $"Successfully extracted {extracted.Count} authors from SVN repository.\n\nPlease review and verify the mappings.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                HideLoading();
            }
            catch (Exception ex)
            {
                HideLoading();
                MessageBox.Show(
                    $"Failed to extract authors from SVN: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void AddAuthor_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddAuthorDialog();
            if (dialog.ShowDialog() == true)
            {
                var newAuthor = new Author
                {
                    SvnUsername = dialog.SvnUsername,
                    GitName = dialog.GitName,
                    GitEmail = dialog.GitEmail,
                    Verified = false
                };

                _allAuthors.Add(newAuthor);
                Authors = new ObservableCollection<Author>(_allAuthors);
                _hasChanges = true;
            }
        }

        private void DeleteAuthor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Author author)
            {
                var result = MessageBox.Show(
                    $"Delete author mapping for '{author.SvnUsername}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _allAuthors.Remove(author);
                    Authors = new ObservableCollection<Author>(_allAuthors);
                    _hasChanges = true;
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchBox.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                Authors = new ObservableCollection<Author>(_allAuthors);
            }
            else
            {
                var filtered = _allAuthors.Where(a =>
                    a.SvnUsername.ToLower().Contains(searchText) ||
                    a.GitName.ToLower().Contains(searchText) ||
                    a.GitEmail.ToLower().Contains(searchText)
                ).ToList();

                Authors = new ObservableCollection<Author>(filtered);
            }
        }

        private void AuthorsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            _hasChanges = true;
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    FileName = $"authors_{_migrationId}.txt",
                    DefaultExt = ".txt"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (var writer = new StreamWriter(saveDialog.FileName))
                    {
                        foreach (var author in _allAuthors)
                        {
                            var gitName = string.IsNullOrWhiteSpace(author.GitName)
                                ? author.SvnUsername
                                : author.GitName;
                            var gitEmail = string.IsNullOrWhiteSpace(author.GitEmail)
                                ? $"{author.SvnUsername}@example.com"
                                : author.GitEmail;

                            await writer.WriteLineAsync($"{author.SvnUsername} = {gitName} <{gitEmail}>");
                        }
                    }

                    MessageBox.Show(
                        $"Authors file exported successfully to:\n{saveDialog.FileName}",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to export authors file: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_hasChanges)
            {
                try
                {
                    ShowLoading("Saving author mappings...");

                    // Save all authors via bulk update
                    var request = new AuthorBulkUpdateRequest
                    {
                        Authors = _allAuthors.Select(a => new AuthorCreateRequest
                        {
                            SvnUsername = a.SvnUsername,
                            GitName = a.GitName,
                            GitEmail = a.GitEmail,
                            Verified = a.Verified
                        }).ToList()
                    };

                    await _apiService.BulkUpdateAuthorsAsync(_migrationId, request);

                    HideLoading();

                    MessageBox.Show(
                        $"Successfully saved {_allAuthors.Count} author mappings.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    HideLoading();
                    MessageBox.Show(
                        $"Failed to save authors: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            else
            {
                DialogResult = true;
                Close();
            }
        }

        private void ShowLoading(string message)
        {
            LoadingText.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadAuthorsButton.IsEnabled = false;
            AddAuthorButton.IsEnabled = false;
            ExportButton.IsEnabled = false;
            SaveButton.IsEnabled = false;
        }

        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            LoadAuthorsButton.IsEnabled = true;
            AddAuthorButton.IsEnabled = true;
            ExportButton.IsEnabled = true;
            SaveButton.IsEnabled = true;
        }

        private void UpdateAuthorCount()
        {
            var verifiedCount = _filteredAuthors.Count(a => a.Verified);
            AuthorCountText.Text = $"{_filteredAuthors.Count} authors ({verifiedCount} verified)";
        }
    }
}
