using System.Globalization;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.PropertyInspector;

public sealed class PropertyProjector
{
    public PropertyInspectorSnapshot Project(ResolvedSelection? selection)
    {
        if (selection is null)
        {
            return new PropertyInspectorSnapshot(
                null,
                "未选择对象",
                "请在画布中选择对象",
                []);
        }

        if (selection.RingCabinet is not null && selection.RingCabinetInterval is null)
        {
            return ProjectRingCabinet(selection);
        }

        if (selection.RingCabinetInterval is not null && selection.SwitchDevice is null)
        {
            return ProjectInterval(selection);
        }

        if (selection.SwitchDevice is not null)
        {
            return ProjectSwitch(selection);
        }

        if (selection.Pole is not null)
        {
            return ProjectPole(selection);
        }

        if (selection.OverheadLine is not null)
        {
            return ProjectOverheadLine(selection);
        }

        if (selection.PoleAttachment is not null)
        {
            return ProjectAttachment(selection);
        }

        return new PropertyInspectorSnapshot(
            selection.Reference,
            "未支持对象",
            selection.Reference.ObjectId.ToString(),
            []);
    }

    private static PropertyInspectorSnapshot ProjectRingCabinet(ResolvedSelection selection)
    {
        RingCabinet cabinet = selection.RingCabinet!;
        var sections = new List<PropertySectionViewModel>
        {
            Section(
                "基本信息",
                DomainRow("Id", "标识", cabinet.Id),
                DomainRow("DisplayName", "名称", cabinet.DisplayName),
                DomainRow("CompositionKind", "组成类型", cabinet.CompositionKind),
                DomainRow("MainBusNodeId", "主母线节点", cabinet.MainBusNodeId),
                DomainRow("IntervalCount", "间隔数量", cabinet.Intervals.Count)),
            LayoutSection(selection.RingCabinetLayout),
            RenderingSection(selection, SymbolKind.RingCabinet)
        };
        return Snapshot(selection, "环网柜", cabinet.DisplayName ?? "环网柜", sections);
    }

    private static PropertyInspectorSnapshot ProjectInterval(ResolvedSelection selection)
    {
        RingCabinetInterval interval = selection.RingCabinetInterval!;
        var rows = new List<PropertyRowViewModel>
        {
            DomainRow("IntervalId", "标识", interval.IntervalId),
            DomainRow("ParentCabinetId", "所属环网柜", interval.ParentCabinetId),
            DomainRow("Sequence", "序号", interval.Sequence),
            DomainRow("DisplayName", "名称", interval.DisplayName),
            DomainRow("IntervalKind", "间隔类型", interval.IntervalKind),
            DomainRow("GroundingStructureKind", "接地结构", interval.GroundingStructureKind),
            DomainRow("ExternalTerminalId", "外部端子", interval.ExternalTerminalId),
            DomainRow("SwitchCount", "开关数量", interval.SwitchDevices.Count)
        };
        var sections = new List<PropertySectionViewModel>
        {
            new("专业属性", rows),
            LayoutSection(selection.RingCabinetIntervalLayout),
            RenderingSection(selection, SymbolKind.RingCabinetInterval)
        };
        return Snapshot(selection, "环网柜间隔", interval.DisplayName, sections);
    }

    private static PropertyInspectorSnapshot ProjectSwitch(ResolvedSelection selection)
    {
        SwitchDevice switchDevice = selection.SwitchDevice!;
        var sections = new List<PropertySectionViewModel>
        {
            Section(
                "专业属性",
                DomainRow("Id", "标识", switchDevice.Id),
                DomainRow("DisplayName", "名称", switchDevice.DisplayName),
                DomainRow("SwitchKind", "开关类型", switchDevice.SwitchKind),
                DomainRow("SwitchState", "机械状态", switchDevice.SwitchState),
                DomainRow("DispatchNumber", "调度编号", switchDevice.DispatchNumber),
                DomainRow("TerminalIds", "端子", string.Join(", ", switchDevice.TerminalIds))),
            LayoutSection(selection.RingCabinetIntervalLayout),
            RenderingSection(
                selection,
                SymbolLibrary.ResolveSwitchKind(switchDevice),
                SymbolLibrary.ResolveVisualState(switchDevice.SwitchState))
        };
        return Snapshot(selection, "开关设备", switchDevice.DisplayName ?? "开关设备", sections);
    }

    private static PropertyInspectorSnapshot ProjectPole(ResolvedSelection selection)
    {
        Pole pole = selection.Pole!;
        var sections = new List<PropertySectionViewModel>
        {
            Section(
                "基本信息",
                DomainRow("Id", "标识", pole.Id),
                DomainRow("PoleNumber", "杆号", pole.PoleNumber),
                DomainRow("DisplayName", "名称", pole.DisplayName),
                DomainRow("PoleType", "杆型", pole.PoleType),
                DomainRow("AnchorCount", "架空锚点数量", pole.OverheadAnchorTerminalIds.Count)),
            LayoutSection(selection.PoleLayout),
            RenderingSection(selection, SymbolKind.Pole)
        };
        return Snapshot(selection, "杆塔", pole.DisplayName ?? pole.PoleNumber, sections);
    }

    private static PropertyInspectorSnapshot ProjectOverheadLine(ResolvedSelection selection)
    {
        OverheadLine line = selection.OverheadLine!;
        var domainRows = new List<PropertyRowViewModel>
        {
            DomainRow("ConnectionId", "连接标识", line.ConnectionId),
            DomainRow("LineModel", "线路型号", line.LineModel),
            DomainRow("LengthMeters", "长度", line.LengthMeters is double length ? $"{length:0.###} m" : "未设置"),
            DomainRow("SupportPoleIds", "支撑杆塔", string.Join(" → ", line.SupportPoleIds)),
            DomainRow("IsContinued", "是否延续", line.IsContinued),
            DomainRow("ContinuationState", "延续状态", line.ContinuationState),
            DomainRow("ContinuationDescription", "延续说明", line.ContinuationDescription)
        };
        if (selection.Connection is not null)
        {
            domainRows.Add(DomainRow("ConnectionName", "连接名称", selection.Connection.DisplayName));
            domainRows.Add(DomainRow("VoltageLevel", "电压等级", selection.Connection.VoltageLevel));
        }

        var sections = new List<PropertySectionViewModel>
        {
            new("专业属性", domainRows),
            LayoutSection(selection.OverheadLineLayout),
            RenderingSection(selection, SymbolKind.OverheadLine)
        };
        return Snapshot(selection, "架空线路", line.LineModel, sections);
    }

    private static PropertyInspectorSnapshot ProjectAttachment(ResolvedSelection selection)
    {
        PoleAttachment attachment = selection.PoleAttachment!;
        var sections = new List<PropertySectionViewModel>
        {
            Section(
                "基本信息",
                DomainRow("AttachmentId", "标识", attachment.AttachmentId),
                DomainRow("PoleId", "所属杆塔", attachment.PoleId),
                DomainRow("AttachedDeviceId", "附属设备", attachment.AttachedDeviceId)),
            LayoutSection(selection.AttachmentLayout),
            RenderingSection(selection, SymbolKind.Pole)
        };
        return Snapshot(selection, "杆塔附属关系", attachment.AttachmentId.ToString(), sections);
    }

    private static PropertyInspectorSnapshot Snapshot(
        ResolvedSelection selection,
        string objectType,
        string objectTitle,
        IEnumerable<PropertySectionViewModel> sections)
    {
        return new PropertyInspectorSnapshot(
            selection.Reference,
            objectType,
            objectTitle,
            sections.Where(section => section.Properties.Count > 0).ToArray());
    }

    private static PropertySectionViewModel Section(
        string title,
        params PropertyRowViewModel[] rows)
    {
        return new PropertySectionViewModel(title, rows);
    }

    private static PropertySectionViewModel LayoutSection(object? layout)
    {
        return layout switch
        {
            RingCabinetLayout cabinet => Section(
                "布局",
                LayoutRow("Position", "位置", FormatPoint(cabinet.Position)),
                LayoutRow("Size", "尺寸", $"{cabinet.WidthMillimeters:0.###} × {cabinet.HeightMillimeters:0.###} mm"),
                LayoutRow("MainBusY", "主母线 Y", $"{cabinet.MainBusYMillimeters:0.###} mm"),
                LayoutRow("LabelOffset", "标签偏移", FormatPoint(cabinet.LabelOffset))),
            RingCabinetIntervalLayout interval => Section(
                "布局",
                LayoutRow("RelativePosition", "相对位置", FormatPoint(interval.RelativePosition)),
                LayoutRow("Size", "尺寸", $"{interval.WidthMillimeters:0.###} × {interval.HeightMillimeters:0.###} mm"),
                LayoutRow("SequenceLabelOffset", "序号偏移", FormatPoint(interval.SequenceLabelOffset)),
                LayoutRow("NameLabelOffset", "名称偏移", FormatPoint(interval.NameLabelOffset))),
            PoleLayout pole => Section(
                "布局",
                LayoutRow("Position", "位置", FormatPoint(pole.Position)),
                LayoutRow("Size", "尺寸", $"{pole.WidthMillimeters:0.###} × {pole.HeightMillimeters:0.###} mm"),
                LayoutRow("LabelOffset", "标签偏移", FormatPoint(pole.LabelOffset))),
            AttachmentLayout attachment => Section(
                "布局",
                LayoutRow("Offset", "相对偏移", FormatPoint(attachment.Offset)),
                LayoutRow("Size", "尺寸", $"{attachment.WidthMillimeters:0.###} × {attachment.HeightMillimeters:0.###} mm"),
                LayoutRow("LabelOffset", "标签偏移", FormatPoint(attachment.LabelOffset))),
            OverheadLineLayout line => Section(
                "布局",
                LayoutRow("Start", "起点", FormatPoint(line.Start)),
                LayoutRow("End", "终点", FormatPoint(line.End)),
                LayoutRow("IsContinued", "延续图形", line.IsContinued)),
            _ => new PropertySectionViewModel("布局", [])
        };
    }

    private static PropertySectionViewModel RenderingSection(
        ResolvedSelection selection,
        SymbolKind symbolKind,
        SymbolVisualState state = SymbolVisualState.None)
    {
        var rows = new List<PropertyRowViewModel>
        {
            RenderingRow("SymbolKind", "图元类型", symbolKind),
            RenderingRow("SymbolVisualState", "显示状态", state)
        };
        if (selection.HitTestEntry is { } hit)
        {
            rows.Add(RenderingRow("HitBounds", "命中范围", FormatRect(hit.Bounds)));
            rows.Add(RenderingRow("HitPriority", "命中优先级", hit.Priority));
        }

        return new PropertySectionViewModel("显示信息", rows);
    }

    private static PropertyRowViewModel DomainRow(string key, string name, object? value) =>
        new(key, name, FormatValue(value), PropertyValueSource.Domain);

    private static PropertyRowViewModel LayoutRow(string key, string name, object? value) =>
        new(key, name, FormatValue(value), PropertyValueSource.Layout);

    private static PropertyRowViewModel RenderingRow(string key, string name, object? value) =>
        new(key, name, FormatValue(value), PropertyValueSource.Rendering);

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "未设置",
            Guid id => id.ToString(),
            bool boolean => boolean ? "是" : "否",
            Enum enumValue => enumValue.ToString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "未设置"
        };
    }

    private static string FormatPoint(DistributionDrawing.Rendering.Wpf.Scene.DocumentPoint point) =>
        $"({point.XMillimeters:0.###}, {point.YMillimeters:0.###}) mm";

    private static string FormatRect(DistributionDrawing.Rendering.Wpf.Scene.DocumentRect rect) =>
        $"({rect.XMillimeters:0.###}, {rect.YMillimeters:0.###}) / " +
        $"{rect.WidthMillimeters:0.###} × {rect.HeightMillimeters:0.###} mm";
}
