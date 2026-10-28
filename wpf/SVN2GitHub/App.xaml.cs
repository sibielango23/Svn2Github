using System.Configuration;
using System.Data;
using System.Windows;

namespace SVN2GitHub
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Material Design themes will be loaded from App.xaml
            // Theme configuration is in the XAML resources
        }
    }
}
