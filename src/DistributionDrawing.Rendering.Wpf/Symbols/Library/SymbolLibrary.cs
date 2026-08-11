using System.Windows.Media;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library.Definitions;

namespace DistributionDrawing.Rendering.Wpf.Symbols.Library;

public sealed class SymbolLibrary
{
    private readonly Dictionary<SymbolKind, ISymbolDefinition> _definitions = [];

    public SymbolLibrary()
    {
        Register(new PoleSymbolDefinition());
        Register(new LineSymbolDefinition(SymbolKind.OverheadLine));
        Register(new LineSymbolDefinition(SymbolKind.CableLine));
        Register(new LineSymbolDefinition(SymbolKind.GroundingLine));
        Register(new SwitchSymbolDefinition(SymbolKind.CircuitBreaker));
        Register(new SwitchSymbolDefinition(SymbolKind.LoadSwitch));
        Register(new SwitchSymbolDefinition(SymbolKind.IsolationSwitch));
        Register(new SwitchSymbolDefinition(SymbolKind.GroundSwitch));
        Register(new SwitchSymbolDefinition(SymbolKind.DropoutFuse));
        Register(new CableTerminationSymbolDefinition());
    }

    public IReadOnlyCollection<SymbolKind> RegisteredKinds => _definitions.Keys.ToArray();

    public void Register(ISymbolDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[definition.Kind] = definition;
    }

    public IReadOnlyList<SceneElement> Create(
        SymbolKind kind,
        SymbolRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_definitions.TryGetValue(kind, out ISymbolDefinition definition))
        {
            throw new InvalidOperationException(
                $"No symbol definition is registered for '{kind}'.");
        }

        return definition.Create(context);
    }

    public IReadOnlyList<SceneElement> CreatePole(
        Pole pole,
        PoleLayout layout)
    {
        ArgumentNullException.ThrowIfNull(pole);
        ArgumentNullException.ThrowIfNull(layout);

        EnsureIdMatch(pole.Id, layout.PoleId, "Pole");

        return Create(
            ResolvePoleKind(pole),
            new SymbolRenderContext(
                layout.Position,
                layout.WidthMillimeters,
                layout.HeightMillimeters,
                labelOrigin: new DocumentPoint(
                    layout.Position.XMillimeters + layout.LabelOffset.XMillimeters,
                    layout.Position.YMillimeters + layout.LabelOffset.YMillimeters),
                label: pole.PoleNumber,
                thicknessMillimeters: 1));
    }

    public IReadOnlyList<SceneElement> CreateAttachment(
        PoleAttachment attachment,
        Device attachedDevice,
        PoleLayout poleLayout,
        AttachmentLayout layout,
        SymbolVisualState? state = null)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        ArgumentNullException.ThrowIfNull(attachedDevice);
        ArgumentNullException.ThrowIfNull(poleLayout);
        ArgumentNullException.ThrowIfNull(layout);

        EnsureIdMatch(attachment.AttachmentId, layout.AttachmentId, "Pole attachment");
        EnsureIdMatch(attachment.PoleId, poleLayout.PoleId, "Pole attachment");
        EnsureIdMatch(attachment.AttachedDeviceId, attachedDevice.Id, "Attached device");

        SymbolKind kind = ResolveAttachmentKind(attachedDevice);
        SymbolVisualState visualState = state ?? ResolveVisualState(attachedDevice);
        DocumentPoint origin = new(
            poleLayout.Position.XMillimeters + layout.Offset.XMillimeters,
            poleLayout.Position.YMillimeters + layout.Offset.YMillimeters);
        double poleCenterX = poleLayout.Position.XMillimeters + poleLayout.WidthMillimeters / 2;

        var elements = new List<SceneElement>
        {
            new SceneLine(
                new DocumentPoint(
                    poleCenterX,
                    origin.YMillimeters + layout.HeightMillimeters / 2),
                new DocumentPoint(
                    origin.XMillimeters,
                    origin.YMillimeters + layout.HeightMillimeters / 2),
                Colors.Black,
                0.7)
        };

        elements.AddRange(
            Create(
                kind,
                new SymbolRenderContext(
                    origin,
                    layout.WidthMillimeters,
                    layout.HeightMillimeters,
                    labelOrigin: new DocumentPoint(
                        origin.XMillimeters + layout.LabelOffset.XMillimeters,
                        origin.YMillimeters + layout.LabelOffset.YMillimeters),
                    label: ResolveAttachmentLabel(attachedDevice),
                    state: visualState,
                    fill: Colors.White)));

        return elements;
    }

    public IReadOnlyList<SceneElement> CreateOverheadLine(
        OverheadLine overheadLine,
        OverheadLineLayout layout)
    {
        ArgumentNullException.ThrowIfNull(overheadLine);
        ArgumentNullException.ThrowIfNull(layout);
        EnsureIdMatch(overheadLine.ConnectionId, layout.ConnectionId, "Overhead line");

        return Create(
            SymbolKind.OverheadLine,
            new SymbolRenderContext(
                layout.Start,
                1,
                1,
                end: layout.End,
                label: overheadLine.LineModel));
    }

    public IReadOnlyList<SceneElement> CreateCableLine(
        DocumentPoint start,
        DocumentPoint end,
        string? label = null)
    {
        return Create(
            SymbolKind.CableLine,
            new SymbolRenderContext(
                start,
                1,
                1,
                end: end,
                label: label));
    }

    public IReadOnlyList<SceneElement> CreateGroundingLine(
        DocumentPoint start,
        DocumentPoint end,
        string? label = null)
    {
        return Create(
            SymbolKind.GroundingLine,
            new SymbolRenderContext(
                start,
                1,
                1,
                end: end,
                label: label));
    }

    public IReadOnlyList<SceneElement> CreateOverheadLineSegment(
        OverheadLineSegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);

        var elements = Create(
            SymbolKind.OverheadLine,
            new SymbolRenderContext(
                segment.Start,
                1,
                1,
                end: segment.End,
                stroke: segment.Stroke,
                thicknessMillimeters: segment.ThicknessMillimeters)).ToList();

        if (segment.IsContinued)
        {
            DocumentPoint offset = segment.ContinuationOffset ?? new DocumentPoint(4, 0);
            elements.AddRange(
                Create(
                    SymbolKind.OverheadLine,
                    new SymbolRenderContext(
                        segment.End,
                        1,
                        1,
                        end: new DocumentPoint(
                            segment.End.XMillimeters + offset.XMillimeters,
                            segment.End.YMillimeters + offset.YMillimeters),
                        stroke: segment.Stroke,
                        thicknessMillimeters: segment.ThicknessMillimeters)));
        }

        return elements;
    }

    public static SymbolKind ResolveAttachmentKind(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (device is CableTermination)
        {
            return SymbolKind.CableTermination;
        }

        if (device is not SwitchDevice switchDevice)
        {
            throw new InvalidOperationException(
                "Only SwitchDevice or CableTermination can use an attachment symbol.");
        }

        return switchDevice.SwitchKind switch
        {
            SwitchKind.CircuitBreaker => SymbolKind.CircuitBreaker,
            SwitchKind.LoadSwitch => SymbolKind.LoadSwitch,
            SwitchKind.IsolationSwitch => SymbolKind.IsolationSwitch,
            SwitchKind.GroundSwitch => SymbolKind.GroundSwitch,
            SwitchKind.DropoutFuse => SymbolKind.DropoutFuse,
            _ => throw new InvalidOperationException(
                $"No attachment symbol is mapped for switch kind '{switchDevice.SwitchKind}'.")
        };
    }

    public static SymbolKind ResolvePoleKind(Pole pole)
    {
        ArgumentNullException.ThrowIfNull(pole);

        return pole.PoleType switch
        {
            PoleType.Cement => SymbolKind.Pole,
            _ => throw new InvalidOperationException(
                $"No pole symbol is mapped for pole type '{pole.PoleType}'.")
        };
    }

    private static SymbolVisualState ResolveVisualState(Device device)
    {
        return ResolveVisualState(device.SwitchState);
    }

    public static SymbolVisualState ResolveVisualState(SwitchState? state)
    {
        return state switch
        {
            SwitchState.Open => SymbolVisualState.Open,
            SwitchState.Closed => SymbolVisualState.Closed,
            _ => SymbolVisualState.None
        };
    }

    private static string ResolveAttachmentLabel(Device device)
    {
        if (device is SwitchDevice switchDevice)
        {
            return switchDevice.SwitchKind switch
            {
                SwitchKind.CircuitBreaker => "柱上断路器",
                SwitchKind.LoadSwitch => "柱上负荷开关",
                SwitchKind.IsolationSwitch => "柱上隔离开关",
                SwitchKind.GroundSwitch => "接地刀闸",
                SwitchKind.DropoutFuse => "跌落式熔断器",
                _ => device.DisplayName ?? "柱上开关"
            };
        }

        return device is CableTermination
            ? "电缆终端"
            : device.DisplayName ?? "柱上设备";
    }

    private static void EnsureIdMatch(Guid expected, Guid actual, string objectName)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException(
                $"{objectName} and layout IDs must match.");
        }
    }
}
