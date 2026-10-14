using RadioE45.ParityCheck;
using RadioE45.ViewModels;

namespace RadioE45.Core.Tests.Parity;

/// <summary>
/// The public ViewModel surface must match parity/vm-surface.txt: a new or changed ViewModel API
/// has to be tracked in parity/features.yaml and the snapshot refreshed with
/// <c>dotnet run --project tools/ParityCheck -- --update-surface</c>.
/// </summary>
public class VmSurfaceSnapshotTests
{
    [Fact]
    public void Surface_matches_snapshot()
    {
        var snapshotPath = Path.Combine(RepoRoot(), ParityChecker.SurfacePath);
        var snapshot = VmSurface.Parse(File.ReadAllText(snapshotPath));
        var current = VmSurface.Generate(typeof(BaseViewModel).Assembly);

        Assert.Equal(snapshot, current);
    }

    [Fact]
    public void Surface_includes_generated_properties_and_commands()
    {
        var current = VmSurface.Generate(typeof(BaseViewModel).Assembly);

        Assert.Contains("OnAirViewModel.IsPlaying : bool { get; set; }", current);
        Assert.Contains(current, l => l.StartsWith("OnAirViewModel.PlayPauseCommand : ", StringComparison.Ordinal));
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "RadioE45.slnx")))
                return dir.FullName;
        }

        throw new InvalidOperationException("RadioE45.slnx not found above the test output directory.");
    }
}
