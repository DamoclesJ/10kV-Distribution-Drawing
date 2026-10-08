using System.IO;
using System.Xml.Linq;
using DistributionDrawing.Desktop.Services;
using DistributionDrawing.Desktop.ViewModels;
using DistributionDrawing.Desktop.WorkTickets;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class WorkRangeEntryAcceptanceTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void LeftAndTopToolPalettesUseTheSameWorkRangeHandler()
    {
        XDocument mainWindow = LoadXaml("MainWindow.xaml");
        XElement left = NamedElement(mainWindow, Presentation + "Border", "LeftToolPalette");
        XElement top = NamedElement(mainWindow, Presentation + "Border", "TopToolPalette");
        XElement leftEntry = Assert.Single(left.Descendants(Presentation + "Button"),
            button => (string?)button.Attribute("Content") == "工作范围");
        XElement topEntry = Assert.Single(top.Descendants(Presentation + "Button"),
            button => (string?)button.Attribute("Content") == "工作范围");

        Assert.Equal("OnOpenTicketRangeFromDrawing", (string?)leftEntry.Attribute("Click"));
        Assert.Equal("OnOpenTicketRangeFromDrawing", (string?)topEntry.Attribute("Click"));
        XDocument eaPanel = LoadXaml("EnergizationPanel.xaml");
        XElement eaEntry = Assert.Single(eaPanel.Descendants(Presentation + "Button"),
            button => (string?)button.Attribute("Content") == "工作范围");
        Assert.Equal("OnRequestWorkRange", (string?)eaEntry.Attribute("Click"));
        string windowCode = File.ReadAllText(AcceptanceFile("MainWindow.xaml.cs"));
        string drawingEntry = HandlerBody(windowCode,
            "private void OnOpenTicketRangeFromDrawing",
            "private void OnTicketWorkspaceSelectionChanged");
        Assert.Contains("OnOpenTicketRange(sender, e);", drawingEntry);
        Assert.Contains("EaPanel.WorkRangeRequested +=", windowCode);
        Assert.Contains("OnOpenTicketRange(this, new RoutedEventArgs())", windowCode);
        string rangeHandler = HandlerBody(
            File.ReadAllText(AcceptanceFile("MainWindow.TicketRange.cs")),
            "private void OnOpenTicketRange", "private void RefreshWorkRange");
        Assert.Contains("SetDrawingRightPanelMode(DrawingRightPanelMode.WorkRange);", rangeHandler);
        Assert.Contains("RefreshWorkRange(session);", rangeHandler);
        Assert.DoesNotContain("DrawingRightPanelHost.Visibility", rangeHandler);
        Assert.Contains("TicketOverlayToggle.IsEnabled = _rightPanelMode != DrawingRightPanelMode.Energization;",
            File.ReadAllText(AcceptanceFile("MainWindow.xaml.cs")));
        Assert.True(HasVisibilitySetter(left, "Visible"));
        Assert.True(HasVisibilitySetter(top, "Collapsed"));
        Assert.True(HasVisibilityTrigger(left, "IsLeftToolPalette", "False", "Collapsed"));
        Assert.True(HasVisibilityTrigger(top, "IsTopToolPalette", "True", "Visible"));
        Assert.DoesNotContain("工作票范围", mainWindow.ToString());
        Assert.DoesNotContain("线路范围", mainWindow.ToString());
        Assert.DoesNotContain("选中开关设为边界", mainWindow.ToString());
        Assert.DoesNotContain("AddWorkScopeCommand", mainWindow.ToString());
        Assert.DoesNotContain("工作票范围", File.ReadAllText(AcceptanceFile("MainWindow.xaml.cs")));
    }

    [Fact]
    public void MainWindowViewModelDefaultsToLeftAndCanSwitchToTopPalette()
    {
        var viewModel = new MainWindowViewModel(new DesktopShellService(),
            DesktopCommandRuntimeTests.CreateActions(() => null));

        Assert.True(viewModel.IsLeftToolPalette);
        Assert.False(viewModel.IsTopToolPalette);
        viewModel.SetToolPalettePlacement(ToolPalettePlacement.Top);
        Assert.False(viewModel.IsLeftToolPalette);
        Assert.True(viewModel.IsTopToolPalette);
    }

    [Fact]
    public void WorkRangePanelShowsEaSelectionAndSharesTheInspectorHost()
    {
        XDocument mainWindowXaml = LoadXaml("MainWindow.xaml");
        XElement rangePanel = Assert.Single(mainWindowXaml.Descendants(Presentation + "ScrollViewer"), element =>
            (string?)element.Attribute(Xaml + "Name") == "TicketRangePanel");
        XElement rangeHost = Assert.Single(mainWindowXaml.Descendants(Presentation + "Grid"), element =>
            (string?)element.Attribute(Xaml + "Name") == "DrawingRightPanelHost");
        XElement inspector = NamedElement(mainWindowXaml, Presentation + "DockPanel", "InspectorContent");
        XDocument workTicketWorkspaceXaml = LoadXaml("WorkTicketWorkspace.xaml");

        Assert.DoesNotContain("实际工作范围", rangePanel.ToString());
        Assert.DoesNotContain("实际工作设备", rangePanel.ToString());
        Assert.DoesNotContain("添加已有电气区段", rangePanel.ToString());
        Assert.DoesNotContain("新建电气区段", rangePanel.ToString());
        Assert.Contains(rangePanel.Descendants(Presentation + "Button"), button =>
            (string?)button.Attribute("Content") == "代入" &&
            (string?)button.Attribute("Click") == "OnConfirmTicketRange");
        Assert.Contains(rangePanel.Descendants(), element =>
            (string?)element.Attribute(Xaml + "Name") == "TicketRangeSelectedDevices");
        Assert.Contains(rangePanel.Descendants(), element =>
            (string?)element.Attribute(Xaml + "Name") == "TicketRangeAnalysisStatus");
        Assert.Contains("已选设备（来自带电分析）", rangePanel.ToString());
        Assert.DoesNotContain("TicketRangeRegions", rangePanel.ToString());
        Assert.DoesNotContain("TicketRangeBoundaries", rangePanel.ToString());
        Assert.DoesNotContain("Boundary A", rangePanel.ToString());
        Assert.DoesNotContain("Boundary B", rangePanel.ToString());
        Assert.Contains(rangeHost.Elements(Presentation + "ScrollViewer"), element =>
            (string?)element.Attribute(Xaml + "Name") == "TicketRangePanel");
        Assert.Contains(rangePanel, rangeHost.Elements());
        Assert.Contains(inspector, rangeHost.Elements());
        XElement eaPanel = Assert.Single(mainWindowXaml.Descendants(), element =>
            (string?)element.Attribute(Xaml + "Name") == "EaPanel");
        Assert.Contains(eaPanel, rangeHost.Elements());
        Assert.Contains("返回图纸代入工作范围", workTicketWorkspaceXaml.ToString());
        Assert.DoesNotContain("工作票范围", workTicketWorkspaceXaml.ToString());
        Assert.DoesNotContain("创建工作票", workTicketWorkspaceXaml.ToString());
        Assert.Contains("IsExpanded=\"False\"", workTicketWorkspaceXaml.ToString());
    }

    [Fact]
    public void EaInverseApplyUsesIntegratedHandoffAndDoesNotExposeLegacyPicking()
    {
        string ticketRangeCode = File.ReadAllText(AcceptanceFile("MainWindow.TicketRange.cs"));
        string windowCode = File.ReadAllText(AcceptanceFile("MainWindow.xaml.cs"));
        string openHandler = HandlerBody(ticketRangeCode, "private void OnOpenTicketRange", "private void RefreshWorkRange");
        string confirmHandler = HandlerBody(ticketRangeCode, "private void OnConfirmTicketRange", "private static string ConfirmationFailureText");
        string selectionHandler = HandlerBody(windowCode, "private void OnSelectionChanged", "private void CollapseSingleSelectionEditors");
        string projectSwitch = HandlerBody(windowCode, "private void OnActiveDocumentSessionChanging", "private void BindActiveSession");

        Assert.Contains("SetDrawingRightPanelMode(DrawingRightPanelMode.WorkRange);", openHandler);
        Assert.Contains("RefreshWorkRange(session);", openHandler);
        Assert.Contains("_workScopeHandoffPlanner.PrepareFromAnalysis(", confirmHandler);
        Assert.Contains("new ConfirmWorkScopeCommand(", confirmHandler);
        Assert.Contains("session.CommandStack.ExecuteCommand", confirmHandler);
        Assert.Contains("ShowTicketWorkspace(commitPendingEdits: false)", confirmHandler);
        Assert.Contains("TicketRangeTaskContent.Text.Trim()", confirmHandler);
        Assert.Contains("TicketRangeTaskObject.Text.Trim()", confirmHandler);
        Assert.DoesNotContain("ApplyRange(", confirmHandler);
        Assert.Contains("ShowTicketWorkspace(commitPendingEdits: false)", confirmHandler);
        Assert.DoesNotContain("DrawingRightPanelMode.WorkRange", selectionHandler);
        string workspaceCode = File.ReadAllText(AcceptanceFile("WorkTicketWorkspace.xaml.cs"));
        Assert.DoesNotContain("WorkRangeCanvasActivation.ActivateOrdinaryObject(", windowCode);
        Assert.DoesNotContain("TicketRangePickerState", windowCode + ticketRangeCode);
        Assert.DoesNotContain("ApplyRange(", workspaceCode);
        Assert.Contains("TicketWorkspace.SelectedTicket?.Id", confirmHandler);
        Assert.Contains("DiscardTicketRangeBuffer();", projectSwitch);
        Assert.Contains("Refresh();", workspaceCode);
        Assert.Contains("FormatWorkScopeSummary", workspaceCode);
        Assert.Contains("AnalyzeConfirmedWorkScopeHandoff", workspaceCode);
    }

    [Fact]
    public void TicketWorkspaceReadsAndUpdatesTheCurrentSessionWorkTicketRoot()
    {
        string code = File.ReadAllText(AcceptanceFile("WorkTicketWorkspace.xaml.cs"));

        Assert.Contains("_session?.PersistenceSession.WorkTickets.Selected(_ticketId)", code);
        Assert.Contains("_session.PersistenceSession.WorkTickets,", code);
    }

    [Fact]
    public void HandoffDiagnosticsUseBusinessLanguageAndNavigateToTicket()
    {
        string range = File.ReadAllText(AcceptanceFile("MainWindow.TicketRange.cs"));
        string workspace = File.ReadAllText(AcceptanceFile("WorkTicketWorkspace.xaml.cs"));
        string workspaceXaml = File.ReadAllText(AcceptanceFile("WorkTicketWorkspace.xaml"));

        Assert.Contains("杆上开关边界方向无法由现有工作票模型确定", range);
        Assert.Contains("客户站进线隔离边界暂不能由现有工作票分析模型完整表示", range);
        Assert.Contains("已选设备的隔离信息组合不满足现有工作票分析模型的约束", range);
        Assert.Contains("TicketWorkspace.SelectTicket(plan.TicketId)", range);
        Assert.Contains("ShowTicketWorkspace(commitPendingEdits: false)", range);
        Assert.Contains("HandoffStatus", workspaceXaml);
        Assert.Contains("工作范围已代入，但当前工作票分析模型无法完整表示已选设备的隔离信息", workspace);
        Assert.DoesNotContain("WtaBoundarySetRejected", workspaceXaml);
        Assert.DoesNotContain("UnresolvedPoleDirection", workspaceXaml);
    }

    private static XDocument LoadXaml(string fileName) => XDocument.Load(AcceptanceFile(fileName));

    private static XElement NamedElement(XDocument document, XName elementName, string name) =>
        Assert.Single(document.Descendants(elementName),
            element => (string?)element.Attribute(Xaml + "Name") == name);

    private static bool HasVisibilityTrigger(XElement element, string binding, string value, string setterValue) =>
        element.Descendants(Presentation + "DataTrigger").Any(trigger =>
            (string?)trigger.Attribute("Binding") == $"{{Binding {binding}}}" &&
            (string?)trigger.Attribute("Value") == value &&
            trigger.Elements(Presentation + "Setter").Any(setter =>
                (string?)setter.Attribute("Property") == "Visibility" &&
                (string?)setter.Attribute("Value") == setterValue));

    private static bool HasVisibilitySetter(XElement element, string value) =>
        element.Descendants(Presentation + "Setter").Any(setter =>
            (string?)setter.Attribute("Property") == "Visibility" &&
            (string?)setter.Attribute("Value") == value);

    private static string AcceptanceFile(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Acceptance", fileName);

    private static string HandlerBody(string source, string handlerName, string nextMethodName)
    {
        int start = source.IndexOf(handlerName, StringComparison.Ordinal);
        int end = source.IndexOf(nextMethodName, start + handlerName.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Could not locate {handlerName} handler.");
        return source[start..end];
    }
}
