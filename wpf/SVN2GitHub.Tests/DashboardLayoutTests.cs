using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SVN2GitHub.Models;
using SVN2GitHub.Services;
using SVN2GitHub.ViewModels;
using SVN2GitHub.Views;

namespace SVN2GitHub.Tests;

public sealed class DashboardLayoutTests
{
    [Fact]
    public void DashboardFiltersAndWizardNavigation_WorkAtNarrowAndWideWidths()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App();
                app.InitializeComponent();
                var dashboard = new DashboardViewModel();
                for (var i = 0; i < 15; i++)
                {
                    dashboard.RecentMigrations.Add(new MigrationDetail
                    {
                        Id = i.ToString(),
                        GithubOrg = "example", GithubRepo = $"repo-{i}",
                        SvnUrl = $"https://svn.example.test/project-{i}",
                        Status = i % 3 == 0 ? "completed" : i % 3 == 1 ? "failed" : "migrating"
                    });
                }
                Assert.Equal(15, dashboard.VisibleMigrationCount);
                foreach (var filter in new[] { DashboardFilter.Active, DashboardFilter.Completed, DashboardFilter.Failed })
                {
                    dashboard.SelectFilterCommand.Execute(filter);
                    Assert.Equal(5, dashboard.VisibleMigrationCount);
                }
                dashboard.SearchText = "EXAMPLE/REPO-1";
                Assert.Equal(3, dashboard.VisibleMigrationCount);
                dashboard.SearchText = "no-match";
                Assert.True(dashboard.NoMatchingMigrations);
                dashboard.ClearFiltersCommand.Execute(null);
                Assert.Equal(15, dashboard.VisibleMigrationCount);
                dashboard.SearchText = "project-14";
                Assert.Equal(1, dashboard.VisibleMigrationCount);
                dashboard.ClearFiltersCommand.Execute(null);
                dashboard.SelectedFilter = DashboardFilter.Failed;
                dashboard.RecentMigrations[0] = new MigrationDetail { Status = "failed" };
                Assert.Equal(6, dashboard.VisibleMigrationCount);
                dashboard.ClearFiltersCommand.Execute(null);
                var navigated = false;
                dashboard.NewMigrationRequested += () => navigated = true;
                dashboard.NewMigrationCommand.Execute(null);
                Assert.True(navigated);

                foreach (var width in new[] { 400d, 760d, 1100d })
                {
                    var wizardModel = new NewMigrationViewModel();
                    var wizard = new NewMigrationView { DataContext = wizardModel };
                    foreach (var step in new[] { 1, 2, 3 })
                    {
                        wizardModel.CurrentStep = step;
                        wizardModel.ValidationMessage = "A long validation message must wrap above the navigation without misaligning or hiding the buttons.";
                        Layout(wizard, width);
                        var buttons = Descendants(wizard).OfType<Button>().ToList();
                        var back = buttons.Single(button => Equals(button.Content, "Back"));
                        var next = buttons.Single(button => Equals(button.Content, step == 3 ? "Start migration" : "Next"));
                        var backBounds = back.TransformToAncestor(wizard).TransformBounds(new Rect(back.RenderSize));
                        var nextBounds = next.TransformToAncestor(wizard).TransformBounds(new Rect(next.RenderSize));
                        Assert.Equal(44, back.ActualHeight, 1);
                        Assert.Equal(44, next.ActualHeight, 1);
                        Assert.Equal(backBounds.Top, nextBounds.Top, 1);
                        Assert.True(backBounds.Right <= nextBounds.Left);
                        Assert.True(nextBounds.Right <= width && nextBounds.Bottom <= 650);
                        Assert.Equal(step > 1, back.IsEnabled);
                    }
                    Layout(new DashboardView { DataContext = dashboard }, width);
                }

                var migration = new MigrationDetail { Status = "failed", LastCompletedStage = "github_setup" };
                var diagnostics = new MigrationDiagnosticsWindow(migration);
                Layout(diagnostics.Content as FrameworkElement ?? throw new InvalidOperationException(), 520);
                var readiness = new ReadinessWindow();
                Layout(readiness.Content as FrameworkElement ?? throw new InvalidOperationException(), 480);
                var root = Path.Combine(Path.GetTempPath(), "copilot-layout-" + Guid.NewGuid().ToString("N"));
                try
                {
                    var copilot = new AiAssistantViewModel(
                        new AiProviderSettings(), new AiAssistantService(new HttpClient(new ChatHandler())),
                        new AiChatHistoryStore(Path.Combine(root, "history.dat")), () => { });
                    var view = new AiAssistantView { DataContext = copilot };
                    Layout(view, 420);
                    copilot.StartNewConversation();
                    Layout(view, 420);
                    var conversation = copilot.SelectedConversation;
                    copilot.Question = "How do I start a migration?";
                    var sendTask = copilot.SendCommand.ExecuteAsync(null);
                    Assert.True(sendTask.IsCompletedSuccessfully, "The local provider fake must finish synchronously.");
                    Layout(view, 420);
                    Assert.Same(conversation, copilot.SelectedConversation);
                    Assert.Equal(2, copilot.Messages.Count);
                    Assert.Contains(Descendants(view).OfType<TextBlock>(),
                        block => block.Text == "A visible test reply.");
                    foreach (var expander in Descendants(view).OfType<Expander>())
                    {
                        expander.IsExpanded = true;
                    }
                    Layout(view, 420);
                    var send = Descendants(view).OfType<Button>()
                        .Single(button => Equals(button.ToolTip, "Send (Enter)"));
                    var bounds = send.TransformToAncestor(view).TransformBounds(new Rect(send.RenderSize));
                    Assert.True(bounds.Bottom <= 650, "Composer must stay visible with guided tasks/context expanded.");
                }
                finally
                {
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(45)), "WPF layout check timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void Layout(FrameworkElement view, double width)
    {
        view.Measure(new Size(width, 650));
        view.Arrange(new Rect(0, 0, width, 650));
        view.UpdateLayout();
    }

    private sealed class ChatHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"choices":[{"message":{"content":"A visible test reply."}}]}""")
            });
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
