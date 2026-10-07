using System.IO;
using System.Xml.Linq;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class DrawingRightPanelAcceptanceTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void ToolboxEntersEaModeAndInspectorHostContainsNoPropertyEaTabs()
    {
        XDocument xaml = XDocument.Load(AcceptanceFile("MainWindow.xaml"));
        XElement host = Named(xaml, "DrawingRightPanelHost");
        Assert.Equal(Presentation + "Grid", host.Name);
        Assert.Empty(host.Descendants(Presentation + "TabItem"));
        Assert.Contains(Named(xaml, "InspectorContent"), host.Elements());
        Assert.Contains(Named(xaml, "EaPanel"), host.Elements());
        Assert.Contains(Named(xaml, "TicketRangePanel"), host.Elements());
        Assert.Equal("Collapsed", (string?)Named(xaml, "EaPanel").Attribute("Visibility"));
        foreach (string palette in new[] { "LeftToolPalette", "TopToolPalette" })
        {
            XElement entry = Assert.Single(Named(xaml, palette).Descendants(Presentation + "Button"),
                element => (string?)element.Attribute("Content") == "带电分析");
            Assert.Equal("OnEnterEnergizationMode", (string?)entry.Attribute("Click"));
        }
        Assert.Null(Named(xaml, "EaPanel").Attribute("ExitRequested"));
    }

    [Fact]
    public void SelectionRefreshesInspectorWhileCandidateRefreshTracksEaChanges()
    {
        string source = File.ReadAllText(AcceptanceFile("MainWindow.xaml.cs"));
        string selection = Between(source, "private void OnSelectionChanged(",
            "private void CollapseSingleSelectionEditors");
        Assert.Contains("EaPanel.SetSelection(_selectionManager.Selected?.ObjectId)", selection);
        Assert.Contains("_propertyInspector.Apply(", selection);
        Assert.DoesNotMatch(@"_rightPanelMode\s*=(?!=)", selection);
        Assert.DoesNotContain("DrawingRightPanelMode.WorkRange", selection);
        Assert.DoesNotContain("InspectorContent.Visibility", selection);
        string canvasSelection = Between(source, "private void OnDrawingSurfaceMouseUp(",
            "private void OnDrawingSurfaceMouseLeave(");
        Assert.DoesNotContain("WorkRangeCanvasActivation.ActivateOrdinaryObject(", canvasSelection);
        Assert.DoesNotContain("_rightPanelMode", canvasSelection);
        string eaRefresh = Between(source, "private void OnEnergizationVisualStateChanged(",
            "private void OnEnergizationDisplayToggle(");
        Assert.DoesNotContain("RefreshWorkScopeCandidateReview", eaRefresh);
        string range = File.ReadAllText(AcceptanceFile("MainWindow.TicketRange.cs"));
        string confirm = Between(range, "private void OnConfirmTicketRange(",
            "private static string ConfirmationFailureText(");
        Assert.Contains("_workScopeHandoffPlanner.Prepare(", confirm);
        Assert.Contains("ReviewedCandidateMismatch", confirm);
        string mode = Between(source, "private void OnEnterEnergizationMode(", "private void OnShowTicketWorkspace(");
        Assert.Contains("SetDrawingRightPanelMode(DrawingRightPanelMode.Energization)", mode);
        Assert.Contains("SetDrawingRightPanelMode(DrawingRightPanelMode.Inspector)", mode);
        Assert.Contains("OnExitEnergizationMode(sender, e)", mode);
        Assert.DoesNotContain("_selectionManager.Clear", mode);
        Assert.DoesNotContain("_selectionManager.Select", mode);
        string apply = Between(source, "private void ApplyDrawingContextPanel()", "private void RefreshEnergizationModeEntry()");
        Assert.Contains("_rightPanelMode == DrawingRightPanelMode.Inspector", apply);
        Assert.Contains("_rightPanelMode == DrawingRightPanelMode.Energization", apply);
        Assert.Contains("_rightPanelMode == DrawingRightPanelMode.WorkRange", apply);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(apply, @"\.Visibility\s*=").Count);
    }

    [Fact]
    public void WorkRangeUsesTheUnifiedModeAndTicketWorkspaceRestoresDrawingMode()
    {
        string source = File.ReadAllText(AcceptanceFile("MainWindow.xaml.cs"));
        string range = File.ReadAllText(AcceptanceFile("MainWindow.TicketRange.cs"));
        Assert.Contains("SetDrawingRightPanelMode(DrawingRightPanelMode.WorkRange)", range);
        Assert.DoesNotContain("DrawingRightPanelHost.Visibility", range);
        Assert.DoesNotContain("TicketRangePanel.Visibility =", range);
        string ticket = Between(source, "private void OnShowTicketWorkspace(", "private void OnTicketOverlayChanged(");
        Assert.Contains("ApplyDrawingContextPanel()", ticket);
        Assert.DoesNotContain("_rightPanelMode =", ticket);
        string drawing = Between(source, "private void OnShowDrawingWorkspace(", "private void OnEnterEnergizationMode(");
        Assert.Contains("ApplyDrawingContextPanel()", drawing);
        string render = source[source.IndexOf("private void RenderCurrentScene()", StringComparison.Ordinal)..];
        Assert.Contains("_rightPanelMode == DrawingRightPanelMode.Energization", render);
        Assert.Contains("elements = EnergizationSceneStyler.Build", render);
        Assert.Contains("out IReadOnlyList<EnergizationVisualizationDiagnostic> diagnostics", render);
        Assert.Contains("EaPanel.SetVisualizationDiagnostics", render);
        Assert.DoesNotContain("elements.AddRange(EnergizationSceneStyler", render);
    }

    [Fact]
    public void AnalysisExitsConfigurationAndDisplayIsIndependentFromRightPanelMode()
    {
        string main = File.ReadAllText(AcceptanceFile("MainWindow.xaml.cs"));
        string panel = File.ReadAllText(AcceptanceFile("EnergizationPanel.xaml.cs"));
        XDocument panelXaml = XDocument.Load(AcceptanceFile("EnergizationPanel.xaml"));

        Assert.Contains("EaPanel.ExitRequested += OnExitEnergizationMode", main);
        string start = Between(panel, "private void OnExecuteAnalysis(", "private void OnExit(");
        Assert.True(start.IndexOf("ExecuteEnergizationAnalysis", StringComparison.Ordinal) <
            start.IndexOf("ExitRequested?.Invoke", StringComparison.Ordinal));
        Assert.Equal("退出", (string?)Named(panelXaml, "ExitButton").Attribute("Content"));
        Assert.Equal("开始分析", (string?)Named(panelXaml, "AnalyzeButton").Attribute("Content"));

        string exit = Between(main, "private void OnExitEnergizationMode(",
            "private void OnEnergizationDisplayToggle(");
        Assert.Contains("DrawingRightPanelMode.Inspector", exit);
        Assert.DoesNotContain("Scenario", exit);
        Assert.DoesNotContain("Energization.Invalidate", exit);

        string toggle = Between(main, "private void OnEnergizationDisplayToggle(",
            "private void OnShowTicketWorkspace(");
        Assert.Contains("CurrentResult is null", toggle);
        Assert.Contains("SetOverlayRequested", toggle);
        Assert.DoesNotContain("ExecuteEnergizationAnalysis", toggle);

        string render = main[main.IndexOf("private void RenderCurrentScene()",
            StringComparison.Ordinal)..];
        Assert.Contains("energization?.OverlayRequested == true", render);
        Assert.DoesNotContain("energization.CanShowOverlay", render);
        XElement displayToggle = Named(XDocument.Load(AcceptanceFile("MainWindow.xaml")),
            "EnergizationDisplayToggle");
        Assert.Equal("OnEnergizationDisplayToggle", (string?)displayToggle.Attribute("Click"));
    }

    private static XElement Named(XDocument xaml, string name) =>
        Assert.Single(xaml.Descendants(), element =>
            (string?)element.Attribute(Xaml + "Name") == name);

    private static string Between(string source, string start, string end)
    {
        int first = source.IndexOf(start, StringComparison.Ordinal);
        int last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal);
        Assert.True(first >= 0 && last > first);
        return source[first..last];
    }

    private static string AcceptanceFile(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Acceptance", name);
}
