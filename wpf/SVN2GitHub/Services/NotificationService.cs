using System.Windows;

namespace SVN2GitHub.Services
{
    public class NotificationService
    {
        public void ShowSuccess(string message, string title = "Success")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        public void ShowError(string message, string title = "Error")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        public void ShowWarning(string message, string title = "Warning")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        public bool ShowConfirmation(string message, string title = "Confirm")
        {
            var result = MessageBoxResult.None;
            Application.Current.Dispatcher.Invoke(() =>
            {
                result = MessageBox.Show(message, title, MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
            });
            return result == MessageBoxResult.Yes;
        }
    }
}
