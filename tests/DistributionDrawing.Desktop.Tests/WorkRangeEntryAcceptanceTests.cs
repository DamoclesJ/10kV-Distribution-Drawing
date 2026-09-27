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

        Assert.Equal("OnOpenTicketRange", (string?)leftEntry.Attribute("Click"));
        Assert.Equal("OnOpenTicketRange", (string?)topEntry.Attribute("Click"));
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
    public void WorkRangePanelKeepsElectricalRangeInsideTheUnifiedSetup()
    {
        XDocument mainWindow = LoadXaml("MainWindow.xaml");
        XElement rangePanel = Assert.Single(mainWindow.Descendants(Presentation + "ScrollViewer"), element =>
            (string?)element.Attribute(Xaml + "Name") == "TicketRangePanel");
        XDocument ticketWorkspace = LoadXaml("WorkTicketWorkspace.xaml");

        Assert.Contains("添加已有电气区段", rangePanel.ToString());
        Assert.Contains("新建电气区段", rangePanel.ToString());
        Assert.Contains(rangePanel.Descendants(Presentation + "Button"), button =>
            (string?)button.Attribute("Click") == "OnBeginAddWorkScope");
        Assert.Contains(ticketWorkspace.ToString(), "返回图纸修改工作范围");
        Assert.DoesNotContain("工作票范围", ticketWorkspace.ToString());
    }

    [Fact]
    public void OpenAndCancelHandlersAvoidTicketWriteCalls()
    {
        string ticketRangeCode = File.ReadAllText(AcceptanceFile("MainWindow.TicketRange.cs"));
        string windowCode = File.ReadAllText(AcceptanceFile("MainWindow.xaml.cs"));
        string openHandler = HandlerBody(ticketRangeCode, "private void OnOpenTicketRange", "private void OnAddTicketBoundarySlot");
        string cancelHandler = HandlerBody(ticketRangeCode, "OnCancelTicketRange", "private void DiscardTicketRangeBuffer");
        string electricalRangeHandler = HandlerBody(windowCode, "private void OnBeginAddWorkScope", "private void OnDrawRingCabinetComposition");

        Assert.Contains("TicketRangePanel.Visibility = Visibility.Visible;", openHandler);
        Assert.DoesNotContain("OnShowDrawingWorkspace(", openHandler);
        Assert.DoesNotContain("CommitPendingEdits(", openHandler);
        Assert.DoesNotContain("CommandStack", openHandler);
        Assert.Contains("BeginRangeEdit();", openHandler);
        Assert.Contains("DiscardTicketRangeBuffer();", cancelHandler);
        Assert.DoesNotContain("ApplyRange(", cancelHandler);
        Assert.DoesNotContain("CommitPendingEdits(", cancelHandler);
        string workspaceCode = File.ReadAllText(AcceptanceFile("WorkTicketWorkspace.xaml.cs"));
        Assert.Contains("if (!_suspendCommandStackRefresh) Refresh();", workspaceCode);
        Assert.Contains("CaptureSetup(ticket)", workspaceCode);
        Assert.DoesNotContain("DiscardTicketRangeBuffer();", electricalRangeHandler);
        Assert.DoesNotContain("TicketRangePanel.Visibility = Visibility.Collapsed;", electricalRangeHandler);
    }

    [Fact]
    public void RangeNavigationPreservesPendingTicketEditsUntilConfirmOrReturn()
    {
        var navigation = new WorkTicketRangeNavigationState();
        int commits = 0;
        int refreshes = 0;

        navigation.RangeOpenedFromTicketWorkspace();
        Assert.False(navigation.ReturnToTicketWorkspace(() => commits++, () => refreshes++));
        Assert.Equal(0, commits);
        Assert.Equal(0, refreshes);
        Assert.True(navigation.ReturnToTicketWorkspace(() => commits++, () => refreshes++));
        Assert.Equal(1, commits);
        Assert.Equal(1, refreshes);

        navigation.RangeOpenedFromTicketWorkspace();
        navigation.RangeConfirmed();
        Assert.True(navigation.ReturnToTicketWorkspace(() => commits++, () => refreshes++));
        Assert.Equal(2, commits);
        Assert.Equal(2, refreshes);
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
