using System.Windows;
using SVN2GitHub.Models;

namespace SVN2GitHub.Views
{
    public partial class ResumeMigrationWindow : Window
    {
        public string TakeGithubToken()
        {
            var token = GithubTokenBox.Password.Trim();
            GithubTokenBox.Clear();
            return token;
        }

        public ResumeMigrationWindow(MigrationDetail migration)
        {
            InitializeComponent();
            DestinationText.Text = $"{migration.GithubOrg}/{migration.GithubRepo}";
            CheckpointText.Text =
                $"Last completed: {GetStageName(migration.LastCompletedStage)}. " +
                $"Next resume stage: {GetNextStage(migration.LastCompletedStage, migration.CleanHistory)}.";
        }

        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(GithubTokenBox.Password))
            {
                StatusText.Text = "Enter a GitHub token to resume this migration.";
                return;
            }

            DialogResult = true;
        }

        private static string GetStageName(string stage) => stage switch
        {
            "analysis" => "SVN analysis",
            "authors" => "author mapping",
            "import" => "SVN import",
            "history_cleanup" => "history cleanup",
            "github_setup" => "GitHub repository setup",
            _ => "migration start"
        };

        private static string GetNextStage(string stage, bool cleanHistory) => stage switch
        {
            "analysis" => "author mapping",
            "authors" => "SVN import (may take hours)",
            "import" when cleanHistory => "history cleanup",
            "import" => "GitHub repository setup",
            "history_cleanup" => "GitHub repository setup",
            "github_setup" => "branch/tag push and verification",
            _ => "SVN analysis"
        };
    }
}
