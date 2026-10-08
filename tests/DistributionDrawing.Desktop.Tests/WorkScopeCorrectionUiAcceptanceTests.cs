using System.IO;
using System.Xml.Linq;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class WorkScopeCorrectionUiAcceptanceTests
{
    [Fact]
    public void WorkRangeShowsReadonlyEaDevicesAndOneApplyActionWithoutRegionOrBoundaryReview()
    {
        XDocument xaml = XDocument.Load(FilePath("MainWindow.xaml"));
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement panel = Assert.Single(xaml.Descendants(), element =>
            (string?)element.Attribute(x + "Name") == "TicketRangePanel");
        XElement devices = Assert.Single(panel.Descendants(p + "ItemsControl"), element =>
            (string?)element.Attribute(x + "Name") == "TicketRangeSelectedDevices");
        Assert.Empty(devices.Descendants(p + "CheckBox"));
        Assert.Empty(panel.Descendants(p + "ListBox"));
        Assert.DoesNotContain("TicketRangeRegions", panel.ToString());
        Assert.DoesNotContain("TicketRangeBoundaries", panel.ToString());
        XElement apply = Assert.Single(panel.Descendants(p + "Button"), button =>
            (string?)button.Attribute("Content") == "代入");
        Assert.Equal("OnConfirmTicketRange", (string?)apply.Attribute("Click"));
        Assert.Contains("工作地点", panel.ToString());
    }

    [Fact]
    public void ApplyPassesCurrentEaScenarioToIntegratedPlannerBeforeOneCommandAndNavigation()
    {
        string code = File.ReadAllText(FilePath("MainWindow.TicketRange.cs"));
        int start = code.IndexOf("private void OnConfirmTicketRange", StringComparison.Ordinal);
        int end = code.IndexOf("private static string ConfirmationFailureText", start, StringComparison.Ordinal);
        string apply = code[start..end];
        Assert.Contains("_workScopeHandoffPlanner.PrepareFromAnalysis(", apply);
        Assert.Contains("session.Energization", apply);
        Assert.Contains("session.PersistenceSession.EnergizationScenario", apply);
        Assert.Equal(1, apply.Split("ExecuteCommand(").Length - 1);
        Assert.Contains("new ConfirmWorkScopeCommand(", apply);
        Assert.Contains("ShowTicketWorkspace(commitPendingEdits: false)", apply);
        Assert.DoesNotContain("_reviewedWorkScopeCandidate", code);
        Assert.DoesNotContain("WorkScopeCandidateProjector", code);
        Assert.DoesNotContain("catch (Exception)", code);
        Assert.Contains("prepared.ConfirmationDiagnostic?.Code", apply);
        Assert.Contains("prepared.Projection?.Diagnostics", apply);
        Assert.Contains("UnresolvedSelectedWorkSide", code);
        string workspace = File.ReadAllText(FilePath("WorkTicketWorkspace.xaml.cs"));
        Assert.Contains("ticket.WorkScopeItems.Where(item => item.Kind != WorkScopeItemKind.Equipment)", workspace);
        Assert.Contains("!handoff.IsComplete", workspace);
        Assert.Contains("AnalyzeTicketButton.IsEnabled", workspace);
    }

    private static string FilePath(string name) => Path.Combine(AppContext.BaseDirectory, "Acceptance", name);
}
