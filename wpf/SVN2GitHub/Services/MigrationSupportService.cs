using System.Text;
using SVN2GitHub.Models;

namespace SVN2GitHub.Services;

public static class MigrationSupportService
{
    public static string BuildCheckReport(ReadinessReport report)
    {
        var text = new StringBuilder($"SVN2GitHub report checked at {report.CheckedAt}\n");
        foreach (var check in report.Checks)
        {
            text.AppendLine($"[{check.Status}] {check.Name}: {check.Detail}");
        }

        return AiAssistantService.RedactSensitiveData(text.ToString());
    }

    public static string GetRecoveryAdvice(MigrationDetail migration)
    {
        if (migration.Status == "completed")
        {
            return "Review the verification logs and validate the destination branches, tags, author identities, and application build before cutover. Completion is not a production acceptance certificate.";
        }

        if (migration.Status != "failed")
        {
            if (migration.Status == "cleaning")
            {
                return "History cleanup is destructive and cannot be safely cancelled in place. Wait for it to finish; do not close the app/API or edit the workspace.";
            }
            if (migration.Status == "cancelling")
            {
                return "Cancellation is in progress. Wait until the job stops before Resume or workspace changes. Remote repositories/refs are not rolled back.";
            }
            return "This job is queued or running. Keep the app/API open, monitor stage updates, and do not edit its workspace. Percentages are milestones, not an exact remaining-time estimate.";
        }

        return migration.LastCompletedStage switch
        {
            "github_setup" => "Resolve the push/verification error, then Resume with a fresh GitHub token. The imported repository and GitHub setup are reused. Divergent remote refs must be reviewed; the app does not force-push.",
            "history_cleanup" => "Resolve GitHub access/setup issues, then Resume with a fresh token. The saved cleaned Git repository is reused.",
            "import" => migration.CleanHistory
                ? "The SVN import is saved. Review the cleanup error and git-filter-repo availability before Resume. Cleanup rewrites commits; an incomplete cleanup repeats."
                : "The SVN import is saved. Review GitHub access/setup errors before Resume with a fresh token.",
            "authors" => "Analysis and authors are saved, but SVN import is incomplete. Resume repeats the import and replaces its generated incomplete repository; this may take hours.",
            "analysis" => "Analysis is saved. Check author mapping/SVN access before Resume; the incomplete mapping stage repeats.",
            _ => "No completed checkpoint is recorded. Resolve the reported cause; Resume begins with SVN analysis."
        };
    }

    public static string BuildSnapshot(MigrationDetail migration)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Migration ID: {migration.Id}");
        builder.AppendLine($"GitHub destination: {migration.GithubOrg}/{migration.GithubRepo}");
        builder.AppendLine($"SVN URL: {migration.SvnUrl}");
        builder.AppendLine($"Status: {migration.Status}");
        builder.AppendLine($"Progress milestone: {migration.Progress}%");
        builder.AppendLine(migration.ConversionTimeDisplay);
        builder.AppendLine("Timing includes recorded processing across attempts, excluding queue/offline/retry waits. Older attempts may have no timing; abrupt-stop timing is limited to the last saved sample.");
        builder.AppendLine($"Current step: {migration.CurrentStep}");
        builder.AppendLine($"Last completed checkpoint: {migration.LastCompletedStage}");
        builder.AppendLine($"History cleanup: {migration.CleanHistory}");
        builder.AppendLine($"Resume offered: {migration.CanResume}");
        builder.AppendLine($"Created: {migration.CreatedAt}");
        builder.AppendLine($"Last updated: {migration.UpdatedAt}");
        builder.AppendLine($"Error: {migration.Error}");
        if (migration.VerificationReport is { } report)
        {
            builder.AppendLine($"Verification snapshot: {report.CheckedAt}");
            foreach (var check in report.Checks)
            {
                builder.AppendLine($"[{check.Status}] {check.Name}: {check.Detail}");
            }
        }
        else
        {
            builder.AppendLine("Verification report: not recorded for this attempt.");
        }
        builder.AppendLine($"Local guidance (not AI-generated): {GetRecoveryAdvice(migration)}");
        return AiAssistantService.RedactSensitiveData(builder.ToString());
    }

    public static string BuildReport(MigrationDetail migration)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SVN2GitHub migration support report");
        builder.AppendLine($"Exported: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine("Snapshot only; not a tamper-proof audit or full migration acceptance report.");
        builder.AppendLine("Review repository names, URLs and logs for confidential information before sharing. Automated secret redaction is best-effort.");
        builder.AppendLine();
        builder.Append(BuildSnapshot(migration));
        builder.AppendLine();
        builder.AppendLine("Recorded migration logs:");
        foreach (var log in migration.Logs)
        {
            builder.AppendLine(log);
        }

        return AiAssistantService.RedactSensitiveData(builder.ToString());
    }
}
