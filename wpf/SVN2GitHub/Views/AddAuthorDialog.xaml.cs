using System.Windows;

namespace SVN2GitHub.Views
{
    public partial class AddAuthorDialog : Window
    {
        public string SvnUsername => SvnUsernameBox.Text;
        public string GitName => GitNameBox.Text;
        public string GitEmail => GitEmailBox.Text;

        public AddAuthorDialog()
        {
            InitializeComponent();
            SvnUsernameBox.Focus();
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(SvnUsername))
            {
                MessageBox.Show(
                    "SVN Username is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                SvnUsernameBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(GitName))
            {
                MessageBox.Show(
                    "Git Name is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                GitNameBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(GitEmail))
            {
                MessageBox.Show(
                    "Git Email is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                GitEmailBox.Focus();
                return;
            }

            // Basic email validation
            if (!GitEmail.Contains("@") || !GitEmail.Contains("."))
            {
                MessageBox.Show(
                    "Please enter a valid email address.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                GitEmailBox.Focus();
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
