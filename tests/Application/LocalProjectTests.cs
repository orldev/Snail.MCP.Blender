using Snail.MCP.Blender.Application.Transfer;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Tests.Application;

/// <summary>The folder the client works in is the project, and its twin on Blender's machine mirrors it: what goes up from a place comes back to the same place.</summary>
public sealed class LocalProjectTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "snail-project-tests", "snail-test");

    private static LocalProject Project(string? directory = null) =>
        new(new ServerConfig { DataDirectory = "/data/snail", ProjectDirectory = directory ?? Root });

    [Fact]
    public void Upload_FromInsideTheProject_LandsInTheTwinAtTheSameRelativePath() =>
        Assert.Equal("snail-test/textures/wood.png", Project().UploadTarget(Path.Combine(Root, "textures", "wood.png")));

    [Fact]
    public void Upload_OfTheProjectFolderItself_LandsAsTheWholeTwin() =>
        Assert.Equal("snail-test", Project().UploadTarget(Root));

    [Fact]
    public void Upload_FromOutsideTheProject_KeepsItsOwnName() =>
        Assert.Equal("hdri.exr", Project().UploadTarget(Path.Combine(Path.GetTempPath(), "elsewhere", "hdri.exr")));

    [Fact]
    public void Download_FromTheTwin_ComesBackToTheSameRelativePlace()
    {
        var project = Project();

        Assert.Equal(Path.Combine(Root, "renders", "turntable"), project.DownloadTarget("snail-test/renders/turntable"));
        Assert.Equal(Path.Combine(Root, "renders"), project.DownloadTarget("snail-test\\renders"));
        Assert.Equal(Root, project.DownloadTarget("snail-test"));
    }

    [Fact]
    public void Download_FromElsewhere_LandsInTheProjectUnderItsOwnName()
    {
        Assert.Equal(Path.Combine(Root, "still.png"), Project().DownloadTarget("C:/renders/still.png"));
        Assert.Equal(Path.Combine(Root, "plates"), Project().DownloadTarget("other-show/plates"));
    }

    /// <summary>A client started in the home folder would scatter renders across it; there is no project there, and downloads go where they always went.</summary>
    [Fact]
    public void TheHomeFolder_IsNoProject_AndDownloadsGoToTheServersOwnFolder()
    {
        var project = Project(ServerPaths.Home);

        Assert.Null(project.Directory);
        Assert.Equal(Path.Combine("/data/snail", "downloads", "still.png"), project.DownloadTarget("snail-test/renders/still.png"));
        Assert.Equal("wood.png", project.UploadTarget(Path.Combine(ServerPaths.Home, "wood.png")));
    }

    [Fact]
    public void RelativeLocalPaths_AreReadFromTheProject() =>
        Assert.Equal(Path.Combine(Root, "textures"), Project().Local("textures"));

    [Fact]
    public void Describe_NamesTheTwinTheWayBlendersMachineWritesIt()
    {
        var state = Project().Describe("C:\\Users\\Def\\.snail-mcp-blender\\files");

        Assert.Equal("snail-test", state["name"]!.ToString());
        Assert.Equal("C:\\Users\\Def\\.snail-mcp-blender\\files\\snail-test", state["twin"]!.ToString());
        Assert.Equal(Root, state["downloads"]!.ToString());
    }
}
