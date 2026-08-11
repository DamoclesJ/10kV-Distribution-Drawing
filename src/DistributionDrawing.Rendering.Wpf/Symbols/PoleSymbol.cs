using System.Windows.Media;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

public sealed class PoleSymbol
{
    public IReadOnlyList<SceneElement> CreateElements(Pole pole, PoleLayout layout)
    {
        ArgumentNullException.ThrowIfNull(pole);
        ArgumentNullException.ThrowIfNull(layout);

        if (pole.Id != layout.PoleId)
        {
            throw new InvalidOperationException(
                "Pole and pole layout IDs must match.");
        }

        double centerX = layout.Position.XMillimeters + layout.WidthMillimeters / 2;
        double topY = layout.Position.YMillimeters;
        double bottomY = topY + layout.HeightMillimeters;

        return
        [
            new SceneLine(
                new DocumentPoint(centerX, topY),
                new DocumentPoint(centerX, bottomY),
                Colors.Black,
                1),
            new SceneLine(
                new DocumentPoint(centerX - 7, topY + 5),
                new DocumentPoint(centerX + 7, topY + 5),
                Colors.Black,
                0.7),
            new SceneText(
                new DocumentPoint(
                    layout.Position.XMillimeters + layout.LabelOffset.XMillimeters,
                    layout.Position.YMillimeters + layout.LabelOffset.YMillimeters),
                pole.PoleNumber,
                Colors.Black,
                4)
        ];
    }
}
