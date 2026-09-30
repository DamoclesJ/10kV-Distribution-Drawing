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
        string windowCode = File.ReadAllText(AcceptanceFile("MainWindow.xaml.cs"));
        string drawingEntry = HandlerBody(windowCode,
            "private void OnOpenTicketRangeFromDrawing",
            "private void OnTicketWorkspaceSelectionChanged");
        Assert.Contains("OnOpenTicketRange(sender, e);", drawingEntry);
        string rangeHandler = HandlerBody(
            File.ReadAllText(AcceptanceFile("MainWindow.TicketRange.cs")),
            "private void OnOpenTicketRange", "private void OnAddTicketBoundarySlot");
        Assert.Contains("DrawingRightPanelTabs.Visibility = Visibility.Collapsed;", rangeHandler);
        Assert.Contains("TicketOverlayToggle.IsEnabled = true;", rangeHandler);
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
    public void WorkRangePanelIsBoundaryOnlyAndSharesTheInspectorHost()
    {
        XDocument mainWindowXaml = LoadXaml("MainWindow.xaml");
        XElement rangePanel = Assert.Single(mainWindowXaml.Descendants(Presentation + "ScrollViewer"), element =>
            (string?)element.Attribute(Xaml + "Name") == "TicketRangePanel");
        XElement rangeHost = Assert.Single(mainWindowXaml.Descendants(Presentation + "Grid"), element =>
            element.Elements().Contains(rangePanel));
        XElement inspector = NamedElement(mainWindowXaml, Presentation + "DockPanel", "InspectorContent");
        XDocument workTicketWorkspaceXaml = LoadXaml("WorkTicketWorkspace.xaml");

        Assert.DoesNotContain("实际工作范围", rangePanel.ToString());
        Assert.DoesNotContain("实际工作设备", rangePanel.ToString());
        Assert.DoesNotContain("添加已有电气区段", rangePanel.ToString());
        Assert.DoesNotContain("新建电气区段", rangePanel.ToString());
        Assert.Contains(rangePanel.Descendants(Presentation + "Button"), button =>
            (string?)button.Attribute("Content") == "确定" &&
            (string?)button.Attribute("Click") == "OnConfirmTicketRange");
        Assert.Contains(rangeHost.Elements(Presentation + "ScrollViewer"), element =>
            (string?)element.Attribute(Xaml + "Name") == "TicketRangePanel");
        XElement rightPanelTabs = NamedElement(mainWindowXaml, Presentation + "TabControl",
            "DrawingRightPanelTabs");
        Assert.Contains(rightPanelTabs, rangeHost.Elements());
        Assert.Contains(inspector, rightPanelTabs.Descendants());
        Assert.Contains("返回图纸修改工作范围", workTicketWorkspaceXaml.ToString());
        Assert.DoesNotContain("工作票范围", workTicketWorkspaceXaml.ToString());
        Assert.DoesNotContain("创建工作票", workTicketWorkspaceXaml.ToString());
        Assert.Contains("IsExpanded=\"False\"", workTicketWorkspaceXaml.ToString());
    }

    [Fact]
    public void OpeningAndLeavingRangePreservesTransientDraftUntilConfirm()
    {
        string ticketRangeCode = File.ReadAllText(AcceptanceFile("MainWindow.TicketRange.cs"));
        string windowCode = File.ReadAllText(AcceptanceFile("MainWindow.xaml.cs"));
        string openHandler = HandlerBody(ticketRangeCode, "private void OnOpenTicketRange", "private void OnAddTicketBoundarySlot");
        string confirmHandler = HandlerBody(ticketRangeCode, "private void OnConfirmTicketRange", "private void DiscardTicketRangeBuffer");
        string selectionHandler = HandlerBody(windowCode, "private void OnSelectionChanged", "private void CollapseSingleSelectionEditors");
        string ticketNavigation = HandlerBody(windowCode, "private void OnShowTicketWorkspace", "private void OnTicketOverlayChanged");
        string projectSwitch = HandlerBody(windowCode, "private void OnActiveDocumentSessionChanging", "private void BindActiveSession");

        Assert.Contains("TicketRangePanel.Visibility = Visibility.Visible;", openHandler);
        Assert.DoesNotContain("OnShowDrawingWorkspace(", openHandler);
        Assert.DoesNotContain("CommitPendingEdits(", openHandler);
        Assert.DoesNotContain("CommandStack", openHandler);
        Assert.Contains("_ticketRangeDraftOwner", openHandler);
        Assert.Contains("session.PersistenceSession.Domain.Id", openHandler);
        Assert.Contains("ApplyRange(", confirmHandler);
        Assert.Contains("OnShowTicketWorkspace(", confirmHandler);
        Assert.Contains("TicketRangePanel.Visibility = Visibility.Collapsed;", selectionHandler);
        Assert.DoesNotContain("DiscardTicketRangeBuffer();", selectionHandler);
        Assert.DoesNotContain("DiscardTicketRangeBuffer();", ticketNavigation);
        string workspaceCode = File.ReadAllText(AcceptanceFile("WorkTicketWorkspace.xaml.cs"));
        Assert.Contains("WorkRangeCanvasActivation.ActivateOrdinaryObject(", windowCode);
        Assert.Contains("_ticketRangeDraftOwner is { } owner", windowCode);
        Assert.Contains("TicketWorkspace.SelectedTicket?.Id", windowCode);
        Assert.Contains("DiscardTicketRangeBuffer();", projectSwitch);
        Assert.Contains("Refresh();", workspaceCode);
        Assert.Contains("CaptureSetup(before)", workspaceCode);
    }

    [Fact]
    public void TicketWorkspaceReadsAndUpdatesTheCurrentSessionWorkTicketRoot()
    {
        string code = File.ReadAllText(AcceptanceFile("WorkTicketWorkspace.xaml.cs"));

        Assert.Contains("_session?.PersistenceSession.WorkTickets.Selected(_ticketId)", code);
        Assert.Contains("_session.PersistenceSession.WorkTickets,", code);
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
