using System.Windows;
using SVN2GitHub.ViewModels;

namespace SVN2GitHub.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
