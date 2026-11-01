using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Windows;
using SVN2GitHub.Views;

namespace SVN2GitHub
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private Process? _portableApiProcess;

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                await StartBundledApiAsync();
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"The local API could not be started.\n\n{exception.Message}",
                    "SVN2GitHub startup error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
                return;
            }

            base.OnStartup(e);
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_portableApiProcess is { HasExited: false } apiProcess)
            {
                try
                {
                    apiProcess.Kill(entireProcessTree: true);
                    apiProcess.WaitForExit(5000);
                }
                catch (InvalidOperationException)
                {
                }
                finally
                {
                    apiProcess.Dispose();
                }
            }

            base.OnExit(e);
        }

        private async Task StartBundledApiAsync()
        {
            var baseDirectory = AppContext.BaseDirectory;
            var apiExecutable = Path.Combine(baseDirectory, "backend", "SVN2GitHub.Api.exe");
            if (!File.Exists(apiExecutable))
            {
                return;
            }

            var localDataDirectory =
                Environment.GetEnvironmentVariable("SVN2GITHUB_PORTABLE_DATA_ROOT");
            if (string.IsNullOrWhiteSpace(localDataDirectory))
            {
                localDataDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SVN2GitHub");
            }

            localDataDirectory = Path.GetFullPath(localDataDirectory);
            var dataDirectory = Path.Combine(localDataDirectory, "data");
            var workspaceDirectory = Path.Combine(localDataDirectory, "workspaces");
            Directory.CreateDirectory(dataDirectory);
            Directory.CreateDirectory(workspaceDirectory);

            var toolsDirectory = Path.Combine(baseDirectory, "tools");
            var gitDirectory = Path.Combine(toolsDirectory, "git");
            var gitPath = Path.Combine(gitDirectory, "cmd");
            var svnPath = Path.Combine(toolsDirectory, "svn", "bin");
            var pathEntries = new[]
            {
                gitPath,
                Path.Combine(gitDirectory, "mingw64", "bin"),
                Path.Combine(gitDirectory, "usr", "bin"),
                svnPath,
                Path.Combine(toolsDirectory, "gh")
            };
            var existingPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            Environment.SetEnvironmentVariable(
                "PATH",
                string.Join(Path.PathSeparator, pathEntries) + Path.PathSeparator + existingPath);
            Environment.SetEnvironmentVariable(
                "SVN2GITHUB_DATA_DIRECTORY",
                dataDirectory);
            Environment.SetEnvironmentVariable(
                "SVN2GITHUB_WORKSPACE_DIRECTORY",
                workspaceDirectory);

            var port = FindAvailablePort();
            var apiUrl = $"http://127.0.0.1:{port}";
            Environment.SetEnvironmentVariable("SVN2GITHUB_API_URL", apiUrl);

            _portableApiProcess = Process.Start(new ProcessStartInfo
            {
                FileName = apiExecutable,
                WorkingDirectory = baseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("The bundled API process could not be created.");

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            for (var attempt = 0; attempt < 30; attempt++)
            {
                if (_portableApiProcess.HasExited)
                {
                    throw new InvalidOperationException(
                        $"The bundled API exited during startup with code {_portableApiProcess.ExitCode}.");
                }

                try
                {
                    using var response = await httpClient.GetAsync($"{apiUrl}/api/health");
                    if (response.IsSuccessStatusCode)
                    {
                        Environment.SetEnvironmentVariable("SVN2GITHUB_API_URL", apiUrl);
                        return;
                    }
                }
                catch (HttpRequestException)
                {
                }
                catch (TaskCanceledException)
                {
                }

                await Task.Delay(500);
            }

            throw new TimeoutException("The bundled API did not become healthy within 15 seconds.");
        }

        private static int FindAvailablePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
