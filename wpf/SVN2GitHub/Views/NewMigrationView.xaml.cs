using System.Windows;
using System.Windows.Controls;
using SVN2GitHub.Services;
using SVN2GitHub.ViewModels;

namespace SVN2GitHub.Views
{
    public partial class NewMigrationView : UserControl
    {
        public NewMigrationView()
        {
            InitializeComponent();
        }

        private void TokenPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is NewMigrationViewModel viewModel)
            {
                viewModel.GithubToken = TokenPasswordBox.Password;
            }
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e)
        {
            TokenPasswordBox.Clear();
            if (DataContext is NewMigrationViewModel viewModel)
            {
                viewModel.ClearFormCommand.Execute(null);
            }
        }

        private async void ConfigureAuthors_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is NewMigrationViewModel viewModel)
            {
                if (!viewModel.NormalizeSourceUrl())
                {
                    MessageBox.Show(
                        viewModel.ValidationMessage,
                        "SVN URL Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // For new migrations, we need to create a temporary migration
                // or use a placeholder ID to allow author extraction
                var tempMigrationId = "temp_" + Guid.NewGuid().ToString("N");
                var apiService = new ApiService();
                var sessionPrepared = false;

                try
                {
                    await apiService.PrepareAuthorSessionAsync(tempMigrationId, viewModel.SvnUrl);
                    sessionPrepared = true;

                    var authorWindow = new AuthorMappingWindow(tempMigrationId)
                    {
                        Owner = Window.GetWindow(this)
                    };

                    if (authorWindow.ShowDialog() == true)
                    {
                        viewModel.SetPreparedAuthors(authorWindow.AllAuthors, viewModel.SvnUrl);
                        MessageBox.Show(
                            $"Prepared {authorWindow.AllAuthors.Count} author mappings for this SVN URL.",
                            "Author Mappings",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to configure author mappings: {ex.Message}",
                        "Author Mapping Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    if (sessionPrepared)
                    {
                        try
                        {
                            await apiService.CloseAuthorSessionAsync(tempMigrationId);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"The temporary author session could not be closed: {ex.Message}",
                                "Cleanup Warning",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                }
            }
        }
    }
}
