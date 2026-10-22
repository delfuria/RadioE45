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
        WritePages();
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

    // --- tv (docs/ANDROID-TV-ROADMAP.md §5) --------------------------------------------------

    [Theory]
    [InlineData("limited", "")]
    [InlineData("n/a", "")]
    [InlineData("maybe", "x")]
    public void Invalid_tv_status_or_missing_tv_notes_is_an_error(string tv, string tvNotes)
    {
        WriteFeatures(desktop: "todo", tv: tv, tvNotes: tvNotes);

        Assert.Contains(Run(), f => f.Severity == Severity.Error && f.Check == "manifest" && f.Message.Contains("tv"));
    }

    [Fact]
    public void Page_without_tv_path_is_an_error()
    {
        Write("parity/pages.yaml", """
            - page: Shell
              maui: RadioE45/AppShell.xaml
              uno: n/a
              reason: test
              tv: n/a
              tvReason: test
              viewmodels: [OnAirViewModel]
            - page: OnAir
              maui: RadioE45/Views/OnAirPage.xaml
              uno: RadioE45.Uno/Views/OnAirPage.xaml
              viewmodels: [OnAirViewModel]
            """);

        Assert.Contains(Run(), f => f.Severity == Severity.Error && f.Message.Contains("no 'tv' path"));
    }

    [Fact]
    public void Tv_not_applicable_page_needs_a_reason()
    {
        Write("parity/pages.yaml", File.ReadAllText(Path.Combine(_root, "parity/pages.yaml")).Replace("tvReason: test", "tvReason: \"\""));

        Assert.Contains(Run(), f => f.Severity == Severity.Error && f.Message.Contains("tvReason"));
    }

    [Fact]
    public void Unmapped_tv_page_is_an_error()
    {
        Write("RadioE45/Views/Tv/TvOrphanPage.xaml", "<ContentPage />");

        Assert.Contains(Run(), f => f.Severity == Severity.Error && f.Message.Contains("TvOrphanPage.xaml"));
    }

    [Fact]
    public void Missing_tv_page_with_done_feature_is_an_error()
    {
        WritePages(tvOnAir: "RadioE45/Views/Tv/TvNowPlayingPage.xaml");
        WriteFeatures(desktop: "todo", tv: "done");

        Assert.Contains(Run(), f => f.Severity == Severity.Error && f.Check == "pages" && f.Message.Contains("TV page"));
    }

    [Fact]
    public void Tv_platform_feature_without_viewmodel_does_not_need_a_tv_page()
    {
        WritePages(tvOnAir: "RadioE45/Views/Tv/TvNowPlayingPage.xaml");
        Write(ParityChecker.FeaturesPath, """
            - id: shell.app
              title: App
              since: "0.44"
              page: Shell
              status: { maui: done, desktop: n/a, web: n/a, tv: n/a }
              notes: test
              tvNotes: test
            - id: tv.launcher
              title: Launcher
              since: "0.49"
              page: OnAir
              status: { maui: n/a, desktop: n/a, web: n/a, tv: done }
              notes: test
            """);

        Assert.DoesNotContain(Run(), f => f.Severity == Severity.Error);
    }

    [Fact]
    public void Tv_not_applicable_page_with_done_feature_is_an_error()
    {
        WriteFeatures(desktop: "todo", tv: "done");

        Assert.Contains(Run(), f => f.Severity == Severity.Error && f.Check == "pages" && f.Message.Contains("tv: n/a"));
    }

    [Fact]
    public void Tv_page_binding_the_same_members_is_aligned()
    {
        WriteFeatures(desktop: "done", tv: "done");
        WriteAlignedUnoPage();
        WritePages(tvOnAir: "RadioE45/Views/Tv/TvNowPlayingPage.xaml");
        Write("RadioE45/Views/Tv/TvNowPlayingPage.xaml", """
            <ContentPage>
              <Label Text="{Binding NowPlaying.Title}" />
              <Label Text="{loc:Translate Tab_OnAir}" />
              <Button Command="{Binding PlayPauseCommand}" IsVisible="{Binding IsBusy}" />
            </ContentPage>
            """);

        Assert.Empty(Run());
    }

    [Fact]
    public void Tv_page_missing_a_binding_is_an_error_unless_declared()
    {
        WriteFeatures(desktop: "done", tv: "done");
        WriteAlignedUnoPage();
        WritePages(tvOnAir: "RadioE45/Views/Tv/TvNowPlayingPage.xaml");
        Write("RadioE45/Views/Tv/TvNowPlayingPage.xaml", """<ContentPage><Label Text="{Binding NowPlaying.Title}" /><Label Text="{loc:Translate Tab_OnAir}" /></ContentPage>""");

        var error = Assert.Single(Run(), f => f.Severity == Severity.Error);
        Assert.Equal("bindings", error.Check);
        Assert.Contains("TvNowPlayingPage.xaml", error.Message);
        Assert.Contains("PlayPauseCommand", error.Message);

        WriteFeatures(desktop: "done", tv: "todo", tvNotes: "");
        Assert.DoesNotContain(Run(), f => f.Severity == Severity.Error);

        WriteFeatures(desktop: "done", tv: "done");
        WritePages(tvOnAir: "RadioE45/Views/Tv/TvNowPlayingPage.xaml", ignoreTv: "PlayPauseCommand");
        Write("RadioE45/Views/Tv/TvNowPlayingPage.xaml", """<ContentPage><Label Text="{Binding NowPlaying.Title}" /><Label Text="{loc:Translate Tab_OnAir}" /><Label Text="{Binding IsBusy}" /></ContentPage>""");
        Assert.Empty(Run());
    }

    [Fact]
    public void Tv_todo_is_one_summary_line_without_include_tv()
    {
        WriteFeatures(desktop: "done", tv: "todo", tvNotes: "");
        WriteAlignedUnoPage();

        var debt = Assert.Single(Run());
        Assert.Equal(Severity.Debt, debt.Severity);
        Assert.Contains("tv: 1", debt.Message);
    }

    [Fact]
    public void Include_tv_lists_the_todo_features()
    {
        WriteFeatures(desktop: "done", tv: "todo", tvNotes: "");
        WriteAlignedUnoPage();

        var debt = Assert.Single(Run(includeTv: true));
        Assert.Equal(Severity.Debt, debt.Severity);
        Assert.Contains("onair.player (tv)", debt.Message);
    }

    [Fact]
    public void Strict_ignores_tv_todo_but_strict_include_tv_does_not()
    {
        WriteFeatures(desktop: "done", tv: "todo", tvNotes: "");
        WriteAlignedUnoPage();

        Assert.DoesNotContain(Run(strict: true), f => f.Severity == Severity.Error);
        Assert.Contains(Run(strict: true, includeTv: true), f => f.Severity == Severity.Error && f.Check == "debt" && f.Message.Contains("onair.player"));
    }

    private IReadOnlyList<Finding> Run(bool strict = false, bool includeTv = false) =>
        new ParityChecker(_root, typeof(BaseViewModel).Assembly).Run(strict, includeTv);

    private void WritePages(string tvOnAir = "n/a", string? ignoreTv = null)
    {
        var lines = new List<string>
        {
            "- page: Shell",
            "  maui: RadioE45/AppShell.xaml",
            "  uno: n/a",
            "  reason: test",
            "  tv: n/a",
            "  tvReason: test",
            "  viewmodels: [OnAirViewModel]",
            "- page: OnAir",
            "  maui: RadioE45/Views/OnAirPage.xaml",
            "  uno: RadioE45.Uno/Views/OnAirPage.xaml",
            $"  tv: {tvOnAir}",
        };
        if (tvOnAir == "n/a") lines.Add("  tvReason: test");
        lines.Add("  viewmodels: [OnAirViewModel]");
        if (ignoreTv is not null)
        {
            lines.Add("  ignoreTv:");
            lines.Add($"    - name: {ignoreTv}");
            lines.Add("      reason: test");
        }

        Write("parity/pages.yaml", string.Join("\n", lines));
    }

    private void WriteAlignedUnoPage() => WriteUnoPage("""
        <Page>
          <TextBlock Text="{x:Bind ViewModel.NowPlaying.Title, Mode=OneWay}" />
          <TextBlock Text="{loc:Translate Tab_OnAir}" />
          <Button Command="{x:Bind ViewModel.PlayPauseCommand}" Visibility="{x:Bind ViewModel.IsBusy}" />
        </Page>
        """);

    private void WriteFeatures(string desktop, string notes = "", string tv = "n/a", string tvNotes = "test")
    {
        Write(ParityChecker.FeaturesPath, $$"""
            - id: shell.app
              title: App
              since: "0.44"
              page: Shell
              status: { maui: done, desktop: n/a, web: n/a, tv: n/a }
              notes: test
              tvNotes: test
            - id: onair.player
              title: Player
              since: "0.44"
              page: OnAir
              viewmodel: OnAirViewModel
              status: { maui: done, desktop: {{desktop}}, web: {{desktop}}, tv: {{tv}} }
              notes: "{{notes}}"
              tvNotes: "{{tvNotes}}"
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
