using System.IO;
using System.Xml.Linq;
using DistributionDrawing.Application.WorkScopes;
using DistributionDrawing.Desktop.Services;
using DistributionDrawing.Desktop.ViewModels;
using DistributionDrawing.Desktop.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
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

        string confirm = HandlerBody(range, "private void OnConfirmTicketRange",
            "private static string ConfirmationFailureText");
        Assert.Contains("TicketWorkspace.SelectTicket(plan.TicketId)", range);
        Assert.Contains("ShowTicketWorkspace(commitPendingEdits: false)", range);
        Assert.Contains("WorkTicketWorkspace.FormatHandoffDiagnostic(", confirm);
        Assert.Contains("TicketWorkspace.SetHandoffNotice(plan.TicketId, plan.AfterTicket, notice)", confirm);
        Assert.Contains("UnresolvedSelectedWorkSide =>", workspace);
        Assert.Contains("EA 判定的不带电侧无法唯一对应工作票侧别", workspace);
        Assert.Contains("DescribeSwitchOrDevice(drawing, device)", workspace);
        Assert.Contains("HandoffStatus.Text = ticket is null ? \"\" : HandoffNotice(drawing, ticket, handoff)",
            workspace);
        Assert.Contains("FormatHandoffDiagnostic(drawing, projection)", workspace);
        Assert.Contains("HandoffStatus", workspaceXaml);
        Assert.Contains("x:Name=\"HandoffStatus\"", workspaceXaml);
        Assert.DoesNotContain("WtaBoundarySetRejected", workspaceXaml);
        Assert.DoesNotContain("UnresolvedPoleDirection", workspaceXaml);
    }

    [Fact]
    public void UnresolvedSelectedWorkSideFormatsNamedBusinessDiagnosticForTicketUi()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "handoff diagnostic");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "测试柜",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open, "东线"),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open, "备用")]));
        drawing.AddDevice(cabinet);
        SwitchDevice selected = cabinet.Intervals[0].SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.LoadSwitch);
        WorkScope unknownSideScope = WorkScope.Create(Guid.NewGuid(),
            [new WorkScopeRegion([selected.FirstTerminalId], [])],
            [new WorkScopeBoundary(selected.Id, BoundarySide.Unknown)]);

        WorkScopeIsolationBoundaryProjection projection = new WorkScopeIsolationBoundaryProjector()
            .Project(drawing, unknownSideScope);
        string message = WorkTicketWorkspace.FormatHandoffDiagnostic(drawing, projection);

        Assert.Equal(WorkScopeBoundaryProjectionDiagnosticCode.UnresolvedSelectedWorkSide,
            Assert.Single(projection.Diagnostics).Code);
        Assert.Contains("工作范围已确认", message);
        Assert.Contains("工作票隔离信息未完整生成", message);
        Assert.Contains("未运行工作票分析", message);
        Assert.Contains("测试柜", message);
        Assert.Contains("东线", message);
        Assert.Contains("EA 判定的不带电侧无法唯一对应工作票侧别", message);
        Assert.DoesNotContain(nameof(WorkScopeBoundaryProjectionDiagnosticCode.UnresolvedSelectedWorkSide), message);
        Assert.DoesNotContain(nameof(WorkScopeBoundaryProjectionDiagnosticCode), message);
        Assert.DoesNotContain(nameof(WorkTicketWorkspace), message);
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
