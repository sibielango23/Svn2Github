using SVN2GitHub.ViewModels;
using SVN2GitHub.Models;

namespace SVN2GitHub.Tests;

public sealed class SvnUrlTests
{
    [Fact]
    public void Wizard_NormalizesSpacesBeforeNavigationAndPreservesPreparedAuthorAssociation()
    {
        var model = new NewMigrationViewModel { SvnUrl = "https://svn.example.test/Source Code/" };
        model.SetPreparedAuthors(new[] { new Author { SvnUsername = "alice" } }, model.SvnUrl);
        model.NextStepCommand.Execute(null);
        Assert.Equal(2, model.CurrentStep);
        Assert.Equal("https://svn.example.test/Source%20Code/", model.SvnUrl);
        Assert.Equal(model.SvnUrl, model.PreparedAuthorsSvnUrl);
        Assert.Empty(model.ValidationMessage);
    }

    [Fact]
    public void Wizard_RejectsWindowsPathWithUsefulError()
    {
        var model = new NewMigrationViewModel { SvnUrl = @"svn.com\Source Code\" };
        model.NextStepCommand.Execute(null);
        Assert.Equal(1, model.CurrentStep);
        Assert.Contains("forward slashes", model.ValidationMessage);
    }
}
