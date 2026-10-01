using System.IO;
using System.IO.Compression;
using System.Text.Json;
using DistributionDrawing.Application.Energization;
using DistributionDrawing.Desktop.Energization;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Infrastructure.Persistence;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class EnergizationRuntimeTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(),
        $"wp-ea-01b-{Guid.NewGuid():N}.kvdrawing");

    [Fact]
    public void ScenarioUsesSharedHistoryAndStalesOnlyAfterSuccessfulDocumentCommand()
    {
        ProjectRuntimeSession runtime = Create();
        EnergizationScenario scenario = runtime.PersistenceSession.EnergizationScenario;
        runtime.ExecuteEnergizationAnalysis();
        Assert.Equal(EnergizationFreshness.Current, runtime.Energization.Freshness);

        EnergizedSeed seed = new(Guid.NewGuid(), Guid.NewGuid(), EnergizationSide.Bus);
        Assert.True(runtime.ExecuteScenarioCommand(EnergizationScenarioCommand.Add(scenario, seed)));
        Assert.Equal(EnergizationFreshness.Stale, runtime.Energization.Freshness);
        Assert.False(runtime.Energization.CanShowOverlay);
        Assert.True(runtime.CommandStack.IsDirty);
        Assert.Single(runtime.CommandStack.History);
        long stateBeforeNoOp = runtime.CommandStack.CurrentStateId;
        Assert.False(runtime.ExecuteScenarioCommand(EnergizationScenarioCommand.Replace(scenario, seed)));
        Assert.Single(runtime.CommandStack.History);
        Assert.Equal(stateBeforeNoOp, runtime.CommandStack.CurrentStateId);

        runtime.ExecuteEnergizationAnalysis();
        Assert.True(runtime.CommandStack.Undo());
        Assert.Empty(scenario.Seeds);
        Assert.Equal(EnergizationFreshness.Stale, runtime.Energization.Freshness);
        runtime.ExecuteEnergizationAnalysis();
        Assert.True(runtime.CommandStack.Redo());
        Assert.Equal(seed, Assert.Single(scenario.Seeds));
        Assert.Equal(EnergizationFreshness.Stale, runtime.Energization.Freshness);
    }

    [Fact]
    public void OpenRuntimeStartsNotAnalyzedAndDoesNotShareResult()
    {
        var service = new ProjectService();
        ProjectRuntimeSession first = ProjectRuntimeSession.CreateEmpty(
            service.CreateProject(_path, "EA runtime"));
        first.ExecuteEnergizationAnalysis();
        Assert.Equal(EnergizationFreshness.Current, first.Energization.Freshness);

        ProjectRuntimeSession second = ProjectRuntimeSession.Create(
            new ProjectService().LoadProject(_path));
        Assert.Equal(EnergizationFreshness.NotAnalyzed, second.Energization.Freshness);
        Assert.Null(second.Energization.CurrentResult);
        Assert.NotSame(first.Energization, second.Energization);
    }

    [Fact]
    public void AnalysisAndOverlayStateNeverEnterV9ProjectJson()
    {
        var service = new ProjectService();
        ProjectRuntimeSession runtime = ProjectRuntimeSession.CreateEmpty(
            service.CreateProject(_path, "EA runtime"));
        runtime.ExecuteEnergizationAnalysis();
        runtime.Energization.SetOverlayRequested(false);
        service.SaveProject();

        using ZipArchive archive = ZipFile.OpenRead(_path);
        using Stream stream = archive.GetEntry(ProjectFileFormat.DocumentEntryName)!.Open();
        using JsonDocument json = JsonDocument.Parse(stream);
        JsonElement root = json.RootElement;
        Assert.True(root.TryGetProperty("energizationScenario", out _));
        foreach (string property in new[] { "energizationResult", "freshness",
                     "overlayRequested", "uiDiagnostics", "panelState" })
            Assert.False(root.TryGetProperty(property, out _));
    }

    [Fact]
    public void EaAndWorkTicketOverlaysAreMutuallyExclusiveByWorkspaceMode()
    {
        DrawingOverlayVisibility ea = DrawingOverlayVisibility.Resolve(true, true,
            true, true);
        Assert.True(ea.ShowEnergization);
        Assert.False(ea.ShowWorkTicket);

        DrawingOverlayVisibility ticket = DrawingOverlayVisibility.Resolve(true, false,
            true, true);
        Assert.False(ticket.ShowEnergization);
        Assert.True(ticket.ShowWorkTicket);

        DrawingOverlayVisibility stale = DrawingOverlayVisibility.Resolve(true, true,
            false, true);
        Assert.False(stale.ShowEnergization);
        Assert.False(stale.ShowWorkTicket);

        DrawingOverlayVisibility otherWorkspace = DrawingOverlayVisibility.Resolve(false,
            true, true, true);
        Assert.False(otherWorkspace.ShowEnergization);
        Assert.False(otherWorkspace.ShowWorkTicket);
    }

    [Fact]
    public void MainScenePlacesEaBelowHoverAndSelectionLayers()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Acceptance",
            "MainWindow.xaml.cs");
        string source = File.ReadAllText(path);
        string render = source[source.IndexOf("private void RenderCurrentScene()",
            StringComparison.Ordinal)..];
        int ea = render.IndexOf("EnergizationSceneProjector.Project", StringComparison.Ordinal);
        int hover = render.IndexOf("_groundingTargetPicker.CreateAffordance",
            StringComparison.Ordinal);
        int selection = render.IndexOf("SelectionOverlayBuilder.CreateElements",
            StringComparison.Ordinal);
        Assert.True(ea >= 0 && hover > ea && selection > hover);
    }

    private ProjectRuntimeSession Create()
    {
        var service = new ProjectService();
        return ProjectRuntimeSession.CreateEmpty(service.CreateProject(_path, "EA runtime"));
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
