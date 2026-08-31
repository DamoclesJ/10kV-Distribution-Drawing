using System.Globalization;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;

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

        if (selection.GroundingPoint is not null)
        {
            return ProjectGroundingPoint(selection);
        }

        if (selection.WorkScope is not null)
        {
            return ProjectWorkScope(selection);
        }

        if (selection.Terminal is not null)
        {
            return ProjectTerminal(selection);
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

        if (selection.CableSegment is not null)
        {
            return ProjectCableSegment(selection);
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
                EditableDomainRow(
                    PropertyCommandFactory.RingCabinetDisplayNamePropertyKey,
                    "环网柜名称",
                    cabinet.DisplayName),
                EditableDomainRow(
                    PropertyCommandFactory.RingCabinetLineNamePropertyKey,
                    "线路名称",
                    cabinet.LineName),
                DomainRow("CompositionKind", "组成类型", cabinet.CompositionKind),
                DomainRow("IntervalCount", "间隔数量", cabinet.Intervals.Count)),
            LayoutSection(selection.RingCabinetLayout)
        };
        return Snapshot(selection, "环网柜", cabinet.DisplayName ?? "环网柜", sections);
    }

    private static PropertyInspectorSnapshot ProjectGroundingPoint(ResolvedSelection selection)
    {
        GroundingPoint groundingPoint = selection.GroundingPoint!;
        return Snapshot(
            selection,
            "工作地线",
            groundingPoint.Number ?? groundingPoint.Location,
            [
                Section(
                    "专业属性",
                    DomainRow("Location", "位置说明", groundingPoint.Location),
                    DomainRow("Number", "编号", groundingPoint.Number),
                    DomainRow("Note", "备注", groundingPoint.Note))
            ]);
    }

    private static PropertyInspectorSnapshot ProjectWorkScope(ResolvedSelection selection)
    {
        WorkScope workScope = selection.WorkScope!;
        var rows = new List<PropertyRowViewModel>
        {
            DomainRow("Description", "说明", workScope.Description),
            DomainRow("StartBoundary.Side", "起始侧别", workScope.StartBoundary.Side),
            DomainRow("EndBoundary.Side", "终止侧别", workScope.EndBoundary.Side),
            DomainRow("GroundingPointCount", "关联工作地线", $"{workScope.GroundingPointIds.Count} 个")
        };
        return Snapshot(
            selection,
            "工作范围",
            workScope.Description,
            [new PropertySectionViewModel("专业属性", rows)]);
    }

    private static PropertyInspectorSnapshot ProjectTerminal(ResolvedSelection selection)
    {
        Terminal terminal = selection.Terminal!;
        return Snapshot(
            selection,
            "端子",
            terminal.Role,
            [
                Section(
                    "端子信息",
                    DomainRow("OwnerType", "所有者类型", terminal.OwnerType),
                    DomainRow("Role", "角色", terminal.Role),
                    DomainRow("VoltageLevel", "电压等级", terminal.VoltageLevel),
                    DomainRow("IsExternal", "外部端子", terminal.IsExternal))
            ]);
    }

    private static PropertyInspectorSnapshot ProjectInterval(ResolvedSelection selection)
    {
        RingCabinetInterval interval = selection.RingCabinetInterval!;
        var rows = new List<PropertyRowViewModel>
        {
            DomainRow("Sequence", "序号", interval.Sequence),
            DomainRow("BayIndex", "业务位置", interval.BayIndex),
            DomainRow("BusinessNumber", "业务编号", interval.BusinessNumber),
            EditableDomainRow(
                PropertyCommandFactory.IntervalDisplayNamePropertyKey,
                "名称",
                interval.DisplayName),
            DomainRow("IntervalKind", "间隔类型", interval.IntervalKind),
            DomainRow("GroundingStructureKind", "接地结构", interval.GroundingStructureKind),
            DomainRow("SwitchCount", "开关数量", interval.SwitchDevices.Count)
        };
        foreach (SwitchDevice switchDevice in interval.SwitchDevices)
        {
            string? number = interval.GetSwitchBusinessNumber(switchDevice.Id);
            rows.Add(DomainRow(
                $"Switch.{switchDevice.Id}",
                switchDevice.DisplayName ?? switchDevice.SwitchKind.ToString(),
                number ?? "未定义"));
        }
        var sections = new List<PropertySectionViewModel>
        {
            new("专业属性", rows),
            LayoutSection(selection.RingCabinetIntervalLayout)
        };
        return Snapshot(selection, "环网柜间隔", interval.DisplayName, sections);
    }

    private static PropertyInspectorSnapshot ProjectSwitch(ResolvedSelection selection)
    {
        SwitchDevice switchDevice = selection.SwitchDevice!;
        string switchBusinessNumber = selection.RingCabinetInterval?.GetSwitchBusinessNumber(
            switchDevice.Id) ?? "未定义";
        var sections = new List<PropertySectionViewModel>
        {
            Section(
                "专业属性",
                DomainRow("DisplayName", "名称", switchDevice.DisplayName),
                DomainRow("SwitchKind", "开关类型", switchDevice.SwitchKind),
                DomainRow("BusinessNumber", "业务编号", switchBusinessNumber),
                DomainRow(
                    "SwitchState",
                    "机械状态",
                    switchDevice.SwitchState == SwitchState.Closed ? "合" : "分"),
                DomainRow("DispatchNumber", "调度编号", switchDevice.DispatchNumber)),
            LayoutSection(selection.PoleAttachment is not null
                ? selection.AttachmentLayout
                : selection.RingCabinetIntervalLayout)
        };
        return Snapshot(
            selection,
            selection.PoleAttachment is null ? "开关设备" : "柱上开关",
            switchDevice.DisplayName ?? "开关设备",
            sections);
    }

    private static PropertyInspectorSnapshot ProjectPole(ResolvedSelection selection)
    {
        Pole pole = selection.Pole!;
        var sections = new List<PropertySectionViewModel>
        {
            Section(
                "基本信息",
                DomainRow("PoleNumber", "杆号", pole.PoleNumber),
                DomainRow("DisplayName", "名称", pole.DisplayName),
                DomainRow("PoleType", "杆型", pole.PoleType)),
            LayoutSection(selection.PoleLayout)
        };
        return Snapshot(selection, "杆塔", pole.DisplayName ?? pole.PoleNumber, sections);
    }

    private static PropertyInspectorSnapshot ProjectOverheadLine(ResolvedSelection selection)
    {
        OverheadLine line = selection.OverheadLine!;
        var domainRows = new List<PropertyRowViewModel>
        {
            DomainRow("LineModel", "线路型号", line.LineModel),
            DomainRow("LengthMeters", "长度", line.LengthMeters is double length ? $"{length:0.###} m" : "未设置"),
            DomainRow("IsContinued", "是否延续", line.IsContinued),
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
            LayoutSection(selection.OverheadLineLayout)
        };
        return Snapshot(selection, "架空线路", line.LineModel, sections);
    }

    private static PropertyInspectorSnapshot ProjectCableSegment(ResolvedSelection selection)
    {
        CableSegment cable = selection.CableSegment!;
        return Snapshot(
            selection,
            "电缆",
            cable.Name,
            [
                Section(
                    "电缆属性",
                    EditableDomainRow(
                        "CableSegment.CableType",
                        "电缆型号",
                        cable.CableType),
                    EditableDomainRow(
                        "CableSegment.Length",
                        "长度（m）",
                        $"{cable.Length:0.###}"))
            ]);
    }

    private static PropertyInspectorSnapshot ProjectAttachment(ResolvedSelection selection)
    {
        PoleAttachment attachment = selection.PoleAttachment!;
        Device? attachedDevice = selection.AttachedDevice;
        var domainRows = new List<PropertyRowViewModel>
        {
            DomainRow(
                "AttachmentKind",
                "安装类型",
                attachedDevice switch
                {
                    CableTermination => "电缆终端",
                    SwitchDevice => "柱上开关",
                    _ => "杆塔附件"
                })
        };

        if (attachedDevice is CableTermination cableTermination)
        {
            domainRows.AddRange(
            [
                DomainRow("DisplayName", "名称", cableTermination.DisplayName)
            ]);
        }

        var sections = new List<PropertySectionViewModel>
        {
            new("基本信息", domainRows),
            LayoutSection(selection.AttachmentLayout)
        };
        string title = attachedDevice?.DisplayName ??
            (attachedDevice is CableTermination ? "电缆终端" : "杆塔附属关系");
        return Snapshot(selection, "杆塔附属关系", title, sections);
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
                LayoutRow("LabelOffset", "标签偏移", FormatPoint(attachment.LabelOffset)),
                LayoutRow("Rotation", "旋转", $"{attachment.RotationQuarterTurns * 90}°")),
            OverheadLineLayout line => Section(
                "布局",
                LayoutRow("Start", "起点", FormatPoint(line.Start)),
                LayoutRow("End", "终点", FormatPoint(line.End)),
                LayoutRow("IsContinued", "延续图形", line.IsContinued)),
            _ => new PropertySectionViewModel("布局", [])
        };
    }

    private static PropertyRowViewModel DomainRow(string key, string name, object? value) =>
        new(key, name, FormatValue(value), PropertyValueSource.Domain);

    private static PropertyRowViewModel EditableDomainRow(string key, string name, object? value) =>
        new(key, name, FormatValue(value), PropertyValueSource.Domain, false);

    private static PropertyRowViewModel LayoutRow(string key, string name, object? value) =>
        new(key, name, FormatValue(value), PropertyValueSource.Layout);

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

}
