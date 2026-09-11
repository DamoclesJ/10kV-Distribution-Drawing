using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
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
}
