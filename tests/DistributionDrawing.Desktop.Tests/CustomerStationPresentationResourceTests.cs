using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using DistributionDrawing.Desktop.CustomerStationCreationUi;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class CustomerStationPresentationResourceTests
{
    [Fact]
    public void CreationDialog_UsesUserStationNumberWording()
    {
        RunOnSta(() =>
        {
            var dialog = new CustomerStationCreationDialog();
            try
            {
                string[] labels = Descendants<System.Windows.Controls.TextBlock>(dialog)
                    .Select(item => item.Text)
                    .ToArray();

                Assert.Contains("用户站号1", labels);
                Assert.Contains("用户站号2", labels);
                Assert.DoesNotContain("进线 1 名称", labels);
                Assert.DoesNotContain("进线 2 名称", labels);
            }
            finally
            {
                dialog.Close();
            }
        });
    }

    [Fact]
    public void ToolboxCustomerStationIcon_IsDedicatedBoxStationGeometry()
    {
        RunOnSta(() =>
        {
            var resources = new ResourceDictionary
            {
                Source = new Uri(
                    "/DistributionDrawing.Desktop;component/Themes/DesktopIcons.xaml",
                    UriKind.Relative)
            };
            var customerStation = Assert.IsAssignableFrom<Geometry>(
                resources["Icon.CustomerStation"]);
            var ringCabinet = Assert.IsAssignableFrom<Geometry>(
                resources["Icon.RingCabinet"]);
            PathGeometry flattened = customerStation.GetFlattenedPathGeometry();

            Assert.NotEqual(ringCabinet.ToString(), customerStation.ToString());
            Assert.Equal(2, flattened.Figures.Count);
            PathFigure roof = flattened.Figures[0];
            PathFigure body = flattened.Figures[1];
            Rect roofBounds = new PathGeometry([roof]).Bounds;
            Rect bodyBounds = new PathGeometry([body]).Bounds;

            Assert.Equal(new Point(2, 8), roof.StartPoint);
            Assert.False(roof.IsClosed);
            Assert.True(body.IsClosed);
            Assert.Equal(2, roofBounds.Left);
            Assert.Equal(22, roofBounds.Right);
            Assert.Equal(2, roofBounds.Top);
            Assert.Equal(4, bodyBounds.Left);
            Assert.Equal(20, bodyBounds.Right);
            Assert.True(roofBounds.Left < bodyBounds.Left);
            Assert.True(roofBounds.Right > bodyBounds.Right);
        });
    }

    [Fact]
    public void MainWindowToolPalettes_BindRingCabinetAndCustomerStationIconsCorrectly()
    {
        string mainWindowPath = FindRepositoryFile(
            "src",
            "DistributionDrawing.Desktop",
            "MainWindow.xaml");
        XDocument xaml = XDocument.Load(mainWindowPath);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var bindings = xaml.Descendants(presentation + "ToggleButton")
            .Select(toggle => new
            {
                Label = toggle.Descendants(presentation + "TextBlock")
                    .Select(text => (string?)text.Attribute("Text"))
                    .FirstOrDefault(text => text is not null),
                Icon = toggle.Descendants(presentation + "Path")
                    .Select(path => (string?)path.Attribute("Data"))
                    .FirstOrDefault(data => data is not null)
            })
            .Where(item => item.Label is "环网柜" or "用户站")
            .ToArray();

        Assert.Equal(2, bindings.Count(item => item.Label == "环网柜"));
        Assert.Equal(2, bindings.Count(item => item.Label == "用户站"));
        Assert.All(bindings.Where(item => item.Label == "环网柜"), item =>
            Assert.Equal("{StaticResource Icon.RingCabinet}", item.Icon));
        Assert.All(bindings.Where(item => item.Label == "用户站"), item =>
            Assert.Equal("{StaticResource Icon.CustomerStation}", item.Icon));
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is T match)
            {
                yield return match;
            }

            if (child is DependencyObject dependencyObject)
            {
                foreach (T descendant in Descendants<T>(dependencyObject))
                {
                    yield return descendant;
                }
            }
        }
    }

    private static string FindRepositoryFile(params string[] relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine([directory.FullName, .. relativePath]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate repository file '{Path.Combine(relativePath)}'.");
    }
}
