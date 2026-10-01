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
    public void SelectionRefreshesCandidatesAndHiddenInspectorWithoutChangingEaMode()
    {
        string source = File.ReadAllText(AcceptanceFile("MainWindow.xaml.cs"));
        string selection = Between(source, "private void OnSelectionChanged(",
            "private void CollapseSingleSelectionEditors");
        Assert.Contains("EaPanel.SetSelection(_selectionManager.Selected?.ObjectId)", selection);
        Assert.Contains("_propertyInspector.Apply(", selection);
        Assert.DoesNotContain("_rightPanelMode =", selection);
        Assert.DoesNotContain("InspectorContent.Visibility", selection);
        string mode = Between(source, "private void OnEnterEnergizationMode(", "private void OnShowTicketWorkspace(");
        Assert.Contains("_rightPanelMode = DrawingRightPanelMode.Energization", mode);
        Assert.Contains("_rightPanelMode = DrawingRightPanelMode.Inspector", mode);
        Assert.Contains("OnExitEnergizationMode(sender, e)", mode);
        Assert.Contains("InspectorContent.Visibility = ea ? Visibility.Collapsed : Visibility.Visible", mode);
        Assert.Contains("EaPanel.Visibility = ea ? Visibility.Visible : Visibility.Collapsed", mode);
        Assert.DoesNotContain("_selectionManager.Clear", mode);
        Assert.DoesNotContain("_selectionManager.Select", mode);
    }

    [Fact]
    public void WorkRangeContextHidesTheEntireHostAndEaStylingRequiresEaMode()
    {
        string source = File.ReadAllText(AcceptanceFile("MainWindow.xaml.cs"));
        string range = File.ReadAllText(AcceptanceFile("MainWindow.TicketRange.cs"));
        Assert.Contains("DrawingRightPanelHost.Visibility = Visibility.Collapsed", range);
        string render = source[source.IndexOf("private void RenderCurrentScene()", StringComparison.Ordinal)..];
        Assert.Contains("_rightPanelMode == DrawingRightPanelMode.Energization", render);
        Assert.Contains("DrawingRightPanelHost.Visibility == Visibility.Visible", render);
        Assert.Contains("elements = EnergizationSceneStyler.Build", render);
        Assert.DoesNotContain("elements.AddRange(EnergizationSceneStyler", render);
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
