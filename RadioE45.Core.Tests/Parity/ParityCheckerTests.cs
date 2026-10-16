using RadioE45.ParityCheck;
using RadioE45.ViewModels;

namespace RadioE45.Core.Tests.Parity;

/// <summary>
/// ParityChecker on a throw-away repository layout in the temp folder (not a git repository, so a
/// surface change always counts as "features.yaml not updated").
/// </summary>
public sealed class ParityCheckerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "parity-tests-" + Guid.NewGuid().ToString("N"));

    public ParityCheckerTests()
    {
        Write("RadioE45/AppShell.xaml", "<Shell />");
        Write("RadioE45/Views/OnAirPage.xaml", """
            <ContentPage>
              <Label Text="{Binding NowPlaying.Title}" />
              <Label Text="{loc:Translate Tab_OnAir}" />
              <Button Command="{Binding PlayPauseCommand}" IsVisible="{Binding IsBusy, Converter={toolkit:InvertedBoolConverter}}" />
            </ContentPage>
            """);
        Write("parity/pages.yaml", """
            - page: Shell
              maui: RadioE45/AppShell.xaml
              uno: n/a
              reason: test
              viewmodels: [OnAirViewModel]
            - page: OnAir
              maui: RadioE45/Views/OnAirPage.xaml
              uno: RadioE45.Uno/Views/OnAirPage.xaml
              viewmodels: [OnAirViewModel]
            """);
        WriteFeatures(desktop: "todo");
        Write(ParityChecker.SurfacePath, VmSurface.ToText(VmSurface.Generate(typeof(BaseViewModel).Assembly)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Missing_uno_page_with_todo_features_is_declared_debt()
    {
        var findings = Run();

        Assert.DoesNotContain(findings, f => f.Severity == Severity.Error);
        Assert.Contains(findings, f => f.Severity == Severity.Debt && f.Check == "pages");
    }

    [Fact]
    public void Missing_uno_page_with_done_feature_is_an_error()
    {
        WriteFeatures(desktop: "done");

        Assert.Contains(Run(), f => f.Severity == Severity.Error && f.Check == "pages");
    }

    [Fact]
    public void Unmapped_maui_page_is_an_error()
    {
        Write("RadioE45/Views/NewPage.xaml", "<ContentPage />");

        Assert.Contains(Run(), f => f.Severity == Severity.Error && f.Message.Contains("NewPage.xaml"));
    }

    [Fact]
    public void Uno_page_binding_the_same_members_through_xbind_is_aligned()
    {
        WriteFeatures(desktop: "done");
        WriteUnoPage("""
            <Page>
              <TextBlock Text="{x:Bind ViewModel.NowPlaying.Title, Mode=OneWay}" />
              <TextBlock Text="{loc:Translate Tab_OnAir}" />
              <Button Command="{x:Bind ViewModel.PlayPauseCommand}" Visibility="{x:Bind ViewModel.IsBusy}" />
            </Page>
            """);

        Assert.Empty(Run());
    }

    [Fact]
    public void Xbind_function_arguments_count_as_bindings()
    {
        WriteFeatures(desktop: "done");
        WriteUnoPage("""
            <Page>
              <TextBlock Text="{x:Bind conv:Fn.Pick(ViewModel.NowPlaying.Title, 'a, b', '▶'), Mode=OneWay}" />
              <TextBlock Text="{loc:Translate Key=Tab_OnAir}" />
              <Button Command="{x:Bind ViewModel.PlayPauseCommand}" Visibility="{x:Bind conv:Fn.Not(conv:Fn.Id(ViewModel.IsBusy))}" />
            </Page>
            """);

        Assert.Empty(Run());
    }

    [Fact]
    public void Xbind_through_a_second_named_viewmodel_counts_as_binding()
    {
        WriteFeatures(desktop: "done");
        WriteUnoPage("""
            <Page>
              <TextBlock Text="{x:Bind OnAirViewModel.NowPlaying.Title, Mode=OneWay}" />
              <TextBlock Text="{loc:Translate Key=Tab_OnAir}" />
              <Button Command="{x:Bind ViewModel.PlayPauseCommand}" Visibility="{x:Bind conv:Fn.Not(OnAirViewModel.IsBusy)}" />
            </Page>
            """);

        Assert.Empty(Run());
    }

    [Fact]
    public void Missing_binding_without_declared_debt_is_an_error()
    {
        WriteFeatures(desktop: "done");
        WriteUnoPage("""<Page><TextBlock Text="{loc:Translate Tab_OnAir}" /><TextBlock Text="{x:Bind ViewModel.IsBusy}" /></Page>""");

        var error = Assert.Single(Run(), f => f.Severity == Severity.Error);
        Assert.Equal("bindings", error.Check);
        Assert.Contains("NowPlaying.Title", error.Message);
        Assert.Contains("PlayPauseCommand", error.Message);
    }

    [Fact]
    public void Missing_binding_with_a_todo_feature_is_debt()
    {
        WriteUnoPage("""<Page><TextBlock Text="{loc:Translate Tab_OnAir}" /></Page>""");

        var findings = Run();

        Assert.DoesNotContain(findings, f => f.Severity == Severity.Error);
        Assert.Contains(findings, f => f.Severity == Severity.Debt && f.Check == "bindings");
    }

    [Fact]
    public void Ignored_members_and_missing_strings_are_handled()
    {
        WriteFeatures(desktop: "done");
        Write("parity/pages.yaml", File.ReadAllText(Path.Combine(_root, "parity/pages.yaml")) + """

              ignore:
                - name: PlayPauseCommand
                  reason: test
            """);
        WriteUnoPage("""<Page><TextBlock Text="{x:Bind ViewModel.NowPlaying.Title}" /><TextBlock Text="{x:Bind ViewModel.IsBusy}" /></Page>""");

        var error = Assert.Single(Run(), f => f.Severity == Severity.Error);
        Assert.Equal("strings", error.Check);
        Assert.Contains("Tab_OnAir", error.Message);
    }

    [Fact]
    public void Code_behind_members_are_compared()
    {
        WriteFeatures(desktop: "done");
        Write("RadioE45/Views/OnAirPage.xaml.cs", "class P { void A() => _viewModel.RestartAsync(); }");
        WriteUnoPage("""
            <Page>
              <TextBlock Text="{x:Bind ViewModel.NowPlaying.Title}" /><TextBlock Text="{loc:Translate Tab_OnAir}" />
              <Button Command="{x:Bind ViewModel.PlayPauseCommand}" Visibility="{x:Bind ViewModel.IsBusy}" />
            </Page>
            """);

        var error = Assert.Single(Run(), f => f.Severity == Severity.Error);
        Assert.Contains("RestartAsync", error.Message);

        Write("RadioE45.Uno/Views/OnAirPage.xaml.cs", "class P { void A() => ViewModel.RestartAsync(); }");
        Assert.Empty(Run());
    }

    [Fact]
    public void Surface_change_without_features_update_is_an_error()
    {
        Write(ParityChecker.SurfacePath, "OnAirViewModel : type\nOnAirViewModel.Removed : bool { get; }\n");

        var error = Assert.Single(Run(), f => f.Severity == Severity.Error);
        Assert.Equal("vm-surface", error.Check);
        Assert.Contains("- OnAirViewModel.Removed", error.Message);
    }

    [Theory]
    [InlineData("limited", "")]
    [InlineData("n/a", "")]
    [InlineData("maybe", "x")]
    public void Invalid_status_or_missing_notes_is_an_error(string desktop, string notes)
    {
        WriteFeatures(desktop, notes);

        Assert.Contains(Run(), f => f.Severity == Severity.Error && f.Check == "manifest");
    }

    [Fact]
    public void Strict_turns_todo_into_errors()
    {
        Assert.Contains(Run(strict: true), f => f.Severity == Severity.Error && f.Check == "debt");
    }

    private IReadOnlyList<Finding> Run(bool strict = false) =>
        new ParityChecker(_root, typeof(BaseViewModel).Assembly).Run(strict);

    private void WriteFeatures(string desktop, string notes = "")
    {
        Write(ParityChecker.FeaturesPath, $$"""
            - id: shell.app
              title: App
              since: "0.44"
              page: Shell
              status: { maui: done, desktop: n/a, web: n/a }
              notes: test
            - id: onair.player
              title: Player
              since: "0.44"
              page: OnAir
              viewmodel: OnAirViewModel
              status: { maui: done, desktop: {{desktop}}, web: {{desktop}} }
              notes: "{{notes}}"
            """);
    }

    private void WriteUnoPage(string xaml) => Write("RadioE45.Uno/Views/OnAirPage.xaml", xaml);

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
