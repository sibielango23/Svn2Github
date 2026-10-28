using System.Windows;
using System.Windows.Controls;
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

        private void ConfigureAuthors_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is NewMigrationViewModel viewModel)
            {
                if (string.IsNullOrWhiteSpace(viewModel.SvnUrl))
                {
                    MessageBox.Show(
                        "Please enter an SVN repository URL first.",
                        "SVN URL Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // For new migrations, we need to create a temporary migration
                // or use a placeholder ID to allow author extraction
                var tempMigrationId = "temp_" + Guid.NewGuid().ToString("N");

                var authorWindow = new AuthorMappingWindow(tempMigrationId)
                {
                    Owner = Window.GetWindow(this)
                };

                authorWindow.ShowDialog();
            }
        }
    }
}
