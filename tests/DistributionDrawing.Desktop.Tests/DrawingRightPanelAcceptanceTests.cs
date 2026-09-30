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
    public void InspectorAndEnergizationShareOneExclusiveRightPanelHost()
    {
        XDocument xaml = XDocument.Load(AcceptanceFile("MainWindow.xaml"));
        XElement host = Named(xaml, "DrawingRightPanelTabs");
        XElement[] tabs = host.Elements(Presentation + "TabItem").ToArray();

        Assert.Equal(2, tabs.Length);
        Assert.Equal("属性", (string?)tabs[0].Attribute("Header"));
        Assert.Equal("带电分析", (string?)tabs[1].Attribute("Header"));
        Assert.Contains(tabs[0].Descendants(), element =>
            (string?)element.Attribute(Xaml + "Name") == "InspectorContent");
        Assert.Contains(tabs[1].Descendants(), element =>
            (string?)element.Attribute(Xaml + "Name") == "EaPanel");
        Assert.Null(Named(xaml, "InspectorContent").Attribute("Visibility"));
        Assert.Null(Named(xaml, "EaPanel").Attribute("Visibility"));
        Assert.DoesNotContain(xaml.Descendants(Presentation + "Button"), button =>
            (string?)button.Attribute("Click") == "OnOpenEnergizationPanel");
        Assert.Equal("OnDrawingRightPanelChanged",
            (string?)host.Attribute("SelectionChanged"));
    }

    [Fact]
    public void SelectionRefreshesEaCandidateAndInspectorWithoutChangingPanelMode()
    {
        string source = File.ReadAllText(AcceptanceFile("MainWindow.xaml.cs"));
        string selection = Between(source, "private void OnSelectionChanged(",
            "private void CollapseSingleSelectionEditors");
        string panelChange = Between(source, "private void OnDrawingRightPanelChanged(",
            "private void OnShowTicketWorkspace(");

        Assert.Contains("EaPanel.SetSelection(_selectionManager.Selected?.ObjectId)", selection);
        Assert.Contains("_propertyInspector.Apply(", selection);
        Assert.DoesNotContain("_rightPanelMode =", selection);
        Assert.DoesNotContain("DrawingRightPanelTabs.Selected", selection);
        Assert.DoesNotContain("InspectorContent.Visibility", source);
        Assert.DoesNotContain("EaPanel.Visibility", source);
        Assert.Contains("DrawingRightPanelTabs.SelectedItem", panelChange);
        Assert.Contains("_rightPanelMode =", panelChange);
        Assert.DoesNotContain("_selectionManager.Clear()", panelChange);
        Assert.DoesNotContain("_selectionManager.Select(", panelChange);
    }

    [Fact]
    public void WorkspaceAndRangeTransitionsKeepThePanelContentsSeparate()
    {
        string source = File.ReadAllText(AcceptanceFile("MainWindow.xaml.cs"));
        string range = File.ReadAllText(AcceptanceFile("MainWindow.TicketRange.cs"));
        string showDrawing = Between(source, "private void OnShowDrawingWorkspace(",
            "private void OnDrawingRightPanelChanged(");
        string showTicket = Between(source, "private void OnShowTicketWorkspace(",
            "private void OnTicketOverlayChanged(");
        string selection = Between(source, "private void OnSelectionChanged(",
            "private void CollapseSingleSelectionEditors");
        string render = source[source.IndexOf("private void RenderCurrentScene()",
            StringComparison.Ordinal)..];

        Assert.Contains("DrawingRightPanelTabs.Visibility = TicketRangePanel.Visibility", showDrawing);
        Assert.Contains("DrawingRightPanelTabs.Visibility = Visibility.Visible", showTicket);
        Assert.Contains("DrawingRightPanelTabs.Visibility = Visibility.Visible", selection);
        Assert.DoesNotContain("_rightPanelMode =", showDrawing + showTicket + selection);
        Assert.Contains("DrawingRightPanelTabs.Visibility = Visibility.Collapsed", range);
        Assert.Contains("_rightPanelMode == DrawingRightPanelMode.Energization", render);
        Assert.Contains("DrawingRightPanelTabs.Visibility == Visibility.Visible", render);
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
