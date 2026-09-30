using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;

namespace DistributionDrawing.Application.Energization;

public sealed record EnergizationBoundaryCandidate(
    Guid DeviceId,
    string DeviceName,
    SwitchKind DeviceKind,
    string OwnerName,
    EnergizationSide Side,
    bool IsResolvable,
    Guid? TerminalId,
    EnergizationDiagnosticCode Diagnostic)
{
    public string DeviceType => EnergizationUiService.DeviceKindText(DeviceKind);
}

public sealed record EnergizationDiagnosticDisplay(
    Guid? SeedId,
    string Message);

public sealed record EnergizationSeedDisplay(
    Guid SeedId,
    string DeviceName,
    string DeviceType,
    string OwnerName,
    EnergizationSide Side,
    bool IsResolvable,
    EnergizationDiagnosticCode Diagnostic);

public sealed class EnergizationUiService
{
    private readonly EnergizationBoundaryPolicy _policy = new();
    private readonly EnergizationAnalyzer _analyzer = new();

    public IReadOnlyList<EnergizationBoundaryCandidate> Candidates(
        DrawingDocument drawing, Guid selectedId)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        var cabinets = drawing.Devices.OfType<RingCabinet>().ToArray();
        IEnumerable<SwitchDevice> devices = drawing.Devices.OfType<SwitchDevice>()
            .Where(device => device.Id == selectedId);
        if (!devices.Any())
        {
            devices = cabinets
                .Where(cabinet => cabinet.Id == selectedId)
                .SelectMany(cabinet => cabinet.Intervals)
                .Concat(cabinets.SelectMany(cabinet => cabinet.Intervals)
                    .Where(interval => interval.IntervalId == selectedId))
                .SelectMany(interval => interval.SwitchDevices)
                .Concat(drawing.PoleAttachments
                    .Where(attachment => attachment.AttachmentId == selectedId ||
                        attachment.PoleId == selectedId)
                    .Select(attachment => drawing.Devices.OfType<SwitchDevice>()
                        .SingleOrDefault(device => device.Id == attachment.AttachedDeviceId))
                    .OfType<SwitchDevice>());
        }

        var candidates = new List<EnergizationBoundaryCandidate>();
        foreach (SwitchDevice device in devices.DistinctBy(device => device.Id))
        {
            string owner = OwnerName(drawing, cabinets, device);
            EnergizationSide[] sides = device.InstallationType switch
            {
                SwitchInstallationType.CabinetInterval =>
                    [EnergizationSide.Bus, EnergizationSide.Line],
                SwitchInstallationType.Pole =>
                    [EnergizationSide.SmallerNumber, EnergizationSide.LargerNumber],
                _ => []
            };
            foreach (EnergizationSide side in sides)
            {
                var seed = new EnergizedSeed(Guid.NewGuid(), device.Id, side);
                bool resolved = _policy.TryResolve(drawing, seed,
                    out Guid terminalId, out EnergizationDiagnosticCode issue);
                if (issue == EnergizationDiagnosticCode.UnsupportedBoundary) continue;
                candidates.Add(new EnergizationBoundaryCandidate(device.Id,
                    string.IsNullOrWhiteSpace(device.DisplayName)
                        ? DeviceKindText(device.SwitchKind) : device.DisplayName,
                    device.SwitchKind, owner, side, resolved,
                    resolved ? terminalId : null, issue));
            }
        }
        return candidates;
    }

    public EnergizationResult Analyze(DrawingDocument drawing, EnergizationScenario scenario) =>
        _analyzer.Analyze(drawing, scenario);

    public EnergizationSeedDisplay DescribeSeed(DrawingDocument drawing, EnergizedSeed seed)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(seed);
        SwitchDevice? device = drawing.Devices.OfType<SwitchDevice>()
            .FirstOrDefault(item => item.Id == seed.BoundaryDeviceId);
        bool resolved = _policy.TryResolve(drawing, seed, out _,
            out EnergizationDiagnosticCode diagnostic);
        return new EnergizationSeedDisplay(seed.Id,
            device is null ? "已删除设备" :
                string.IsNullOrWhiteSpace(device.DisplayName)
                    ? DeviceKindText(device.SwitchKind) : device.DisplayName,
            device is null ? "未知" : DeviceKindText(device.SwitchKind),
            device is null ? "" : OwnerName(drawing,
                drawing.Devices.OfType<RingCabinet>().ToArray(), device),
            seed.Side, resolved, diagnostic);
    }

    public bool CanConfirmSources(DrawingDocument drawing, EnergizationScenario scenario) =>
        scenario.Seeds.Count > 0 &&
        scenario.Seeds.All(seed => DescribeSeed(drawing, seed).IsResolvable);

    public IReadOnlyList<EnergizationDiagnosticDisplay> Diagnostics(EnergizationResult result) =>
        result.Diagnostics.Select(item => new EnergizationDiagnosticDisplay(
            item.SeedId, string.IsNullOrWhiteSpace(item.Detail)
                ? DiagnosticText(item.Code)
                : $"{DiagnosticText(item.Code)}：{item.Detail}")).ToArray();

    public static string DiagnosticText(EnergizationDiagnosticCode code) => code switch
    {
        EnergizationDiagnosticCode.SourceSetUnconfirmed => "电源全集尚未确认",
        EnergizationDiagnosticCode.EmptyScenario => "未设置电源点",
        EnergizationDiagnosticCode.MissingBoundaryDevice => "边界设备已不存在",
        EnergizationDiagnosticCode.UnsupportedBoundary => "设备不能作为电源边界",
        EnergizationDiagnosticCode.InvalidSide => "电气侧不合法",
        EnergizationDiagnosticCode.UnresolvedSide => "无法判定大/小号侧，请检查杆号与线路连接",
        EnergizationDiagnosticCode.MissingTerminal => "边界端子已不存在",
        EnergizationDiagnosticCode.InvalidTopology => "图纸电气拓扑不完整",
        _ => ""
    };

    private static string OwnerName(DrawingDocument drawing,
        IReadOnlyList<RingCabinet> cabinets, SwitchDevice device)
    {
        foreach (RingCabinet cabinet in cabinets)
        foreach (RingCabinetInterval interval in cabinet.Intervals)
            if (interval.SwitchDevices.Any(item => item.Id == device.Id))
                return $"{(string.IsNullOrWhiteSpace(cabinet.DisplayName) ? "环网柜" : cabinet.DisplayName)} / " +
                    $"{(string.IsNullOrWhiteSpace(interval.DisplayName) ? $"间隔 {interval.BayIndex}" : interval.DisplayName)}";
        PoleAttachment? attachment = drawing.PoleAttachments.FirstOrDefault(item =>
            item.AttachedDeviceId == device.Id);
        Pole? pole = attachment is null ? null : drawing.Devices.OfType<Pole>()
            .FirstOrDefault(item => item.Id == attachment.PoleId);
        return pole?.PoleNumber ?? "柱上设备";
    }

    public static string DeviceKindText(SwitchKind kind) => kind switch
    {
        SwitchKind.LoadSwitch => "负荷开关",
        SwitchKind.CircuitBreaker => "断路器",
        SwitchKind.IsolationSwitch => "隔离开关",
        SwitchKind.DropoutFuse => "跌落式熔断器",
        SwitchKind.GroundSwitch => "接地开关",
        _ => "开关设备"
    };
}
