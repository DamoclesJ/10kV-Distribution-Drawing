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
    public void LeftAndTopToolboxesEnterEaModeAndRightHostHasExclusiveContents()
    {
        XDocument xaml = XDocument.Load(AcceptanceFile("MainWindow.xaml"));
        XElement left = Named(xaml, "LeftToolPalette");
        XElement top = Named(xaml, "TopToolPalette");
        foreach (XElement palette in new[] { left, top })
        {
            XElement entry = Assert.Single(palette.Descendants(Presentation + "Button"),
                button => (string?)button.Attribute("Content") == "带电分析");
            Assert.Equal("OnEnterEnergizationMode", (string?)entry.Attribute("Click"));
        }

        XElement inspector = Named(xaml, "InspectorContent");
        XElement ea = Named(xaml, "EaPanel");
        XElement workRange = Named(xaml, "TicketRangePanel");
        Assert.Same(inspector.Parent, ea.Parent);
        Assert.Same(inspector.Parent, workRange.Parent);
        Assert.Equal("Collapsed", (string?)ea.Attribute("Visibility"));
        Assert.Equal("Collapsed", (string?)workRange.Attribute("Visibility"));
        Assert.Null(inspector.Attribute("Visibility"));
        Assert.DoesNotContain(xaml.Descendants(Presentation + "TabControl"),
            tab => (string?)tab.Attribute(Xaml + "Name") == "DrawingRightPanelTabs");
    }

    [Fact]
    public void SelectionUpdatesBothProjectionsWithoutLeavingEaMode()
    {
        string source = File.ReadAllText(AcceptanceFile("MainWindow.xaml.cs"));
        string selection = Between(source, "private void OnSelectionChanged(",
            "private void CollapseSingleSelectionEditors");
        string apply = Between(source, "private void ApplyDrawingRightPanelMode()",
            "private void OnShowTicketWorkspace(");
        string enter = Between(source, "private void OnEnterEnergizationMode(",
            "private void OnExitEnergizationMode(");

        Assert.Contains("EaPanel.SetSelection(_selectionManager.Selected?.ObjectId)", selection);
        Assert.Contains("_propertyInspector.Apply(", selection);
        Assert.DoesNotContain("_rightPanelMode = DrawingRightPanelMode.Inspector", selection);
        Assert.Contains("SetDrawingRightPanelMode(DrawingRightPanelMode.Energization)", enter);
        Assert.Contains("InspectorContent.Visibility = _rightPanelMode == DrawingRightPanelMode.Inspector", apply);
        Assert.Contains("EaPanel.Visibility = _rightPanelMode == DrawingRightPanelMode.Energization", apply);
        Assert.Contains("TicketRangePanel.Visibility = _rightPanelMode == DrawingRightPanelMode.WorkRange", apply);
    }

    [Fact]
    public void ExitRestoresInspectorWithoutClearingSelectionAndEaKeepsSwitchOperation()
    {
        string source = File.ReadAllText(AcceptanceFile("MainWindow.xaml.cs"));
        string exit = Between(source, "private void OnExitEnergizationMode(",
            "private void SetDrawingRightPanelMode(");
        string set = Between(source, "private void SetDrawingRightPanelMode(",
            "private void ApplyDrawingRightPanelMode(");
        XDocument ea = XDocument.Load(AcceptanceFile("EnergizationPanel.xaml"));

        Assert.Contains("SetDrawingRightPanelMode(DrawingRightPanelMode.Inspector)", exit);
        Assert.DoesNotContain("_selectionManager.Clear()", exit + set);
        Assert.DoesNotContain("_selectionManager.Select(", exit + set);
        Assert.Contains(ea.Descendants(Presentation + "Button"), button =>
            (string?)button.Attribute("Content") == "所选开关分/合" &&
            ((string?)button.Attribute("Command"))?.Contains("SwitchOperationCommand") == true);
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
