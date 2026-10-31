using System.Windows;
using SVN2GitHub.ViewModels;

namespace SVN2GitHub.Views
{
    public partial class AiAssistantWindow : Window
    {
        public AiAssistantWindow(AiAssistantViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
