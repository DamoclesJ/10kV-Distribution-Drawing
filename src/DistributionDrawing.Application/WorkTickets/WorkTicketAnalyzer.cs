using System.Security.Cryptography;
using System.Text;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Devices.SwitchAssemblies;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.WorkTickets;

public interface IWorkTicketRulePack
{
    string Version => "first-kind-v0.1";
    string? BoundaryIssue(DrawingDocument drawing, IsolationBoundary boundary) => null;
    IEnumerable<MeasureFact> Switching(DrawingDocument drawing, IsolationBoundary boundary);
    IEnumerable<MeasureFact> BoundaryDecorations(WorkTicketSession ticket, IsolationBoundary boundary);
    IEnumerable<MeasureFact> WorkGroup(DrawingDocument drawing, WorkTicketSession ticket);
    IEnumerable<RetainedLivePart> RetainedLive(DrawingDocument drawing, WorkTicketSession ticket);
    string CoordinatedOutage(DrawingDocument drawing, WorkTicketSession ticket);
}

public interface IRiskRulePack
{
    string Version => "first-kind-risk-v0.1";
    IEnumerable<RiskItem> Risks(DrawingDocument drawing, WorkTicketSession ticket);
}

public sealed class FirstKindRulePack : IWorkTicketRulePack, IRiskRulePack
{
    public string Version => "first-kind-v0.1";

    public string? BoundaryIssue(DrawingDocument drawing, IsolationBoundary boundary)
    {
        SwitchDevice? device = drawing.Devices.OfType<SwitchDevice>().SingleOrDefault(item => item.Id == boundary.DeviceId);
        if (device is null) return $"隔离边界设备 {boundary.DeviceId} 不存在";
        if (boundary.Side == BoundarySide.Unknown) return $"{device.DisplayName} 缺少边界侧别";
        if (boundary.Side is not (BoundarySide.Bus or BoundarySide.Line))
            return $"{device.DisplayName} 的 {boundary.Side} 侧缺少可核实的电气方向";
        RingCabinet? cabinet = drawing.Devices.OfType<RingCabinet>().SingleOrDefault(item =>
            item.Intervals.Any(interval => interval.SwitchDevices.Any(sw => sw.Id == device.Id)));
        RingCabinetInterval? interval = cabinet?.Intervals.Single(item =>
            item.SwitchDevices.Any(sw => sw.Id == device.Id));
        if (interval is null)
            return $"{device.DisplayName} 缺少能够核实母线/线路方向的间隔结构";
        if (device.SwitchKind == SwitchKind.GroundSwitch)
            return $"{device.DisplayName} 是接地刀闸，不能作为隔离边界开关";

        Guid? buswardNode;
        Guid? linewardNode;
        if (interval.IntervalKind == IntervalKind.LoadSwitchInterval && device.SwitchKind == SwitchKind.LoadSwitch)
        {
            buswardNode = cabinet!.MainBusNodeId;
            linewardNode = interval.CircuitNodeId;
        }
        else if (interval.IntervalKind == IntervalKind.IntegratedFeederInterval &&
                 interval.IntermediateNodeId is Guid intermediate)
        {
            (buswardNode, linewardNode) = (interval.GroundingStructureKind, device.SwitchKind) switch
            {
                (GroundingStructureKind.LowerLowerGrounding, SwitchKind.IsolationSwitch) =>
                    (intermediate, interval.CircuitNodeId),
                (GroundingStructureKind.LowerLowerGrounding, SwitchKind.CircuitBreaker) =>
                    (cabinet!.MainBusNodeId, intermediate),
                (GroundingStructureKind.UpperIsolationGrounding or GroundingStructureKind.UpperLowerGrounding,
                    SwitchKind.IsolationSwitch) => (cabinet!.MainBusNodeId, intermediate),
                (GroundingStructureKind.UpperIsolationGrounding or GroundingStructureKind.UpperLowerGrounding,
                    SwitchKind.CircuitBreaker) => (intermediate, interval.CircuitNodeId),
                _ => ((Guid?)null, (Guid?)null)
            };
        }
        else (buswardNode, linewardNode) = (null, null);
        Terminal? first = drawing.Terminals.SingleOrDefault(item => item.Id == device.FirstTerminalId);
        Terminal? second = drawing.Terminals.SingleOrDefault(item => item.Id == device.SecondTerminalId);
        if (buswardNode is null || linewardNode is null ||
            first?.ElectricalNodeId != buswardNode || second?.ElectricalNodeId != linewardNode)
            return $"{device.DisplayName} 的电气节点与间隔结构不一致，无法核实边界侧别";

        if (boundary.ConnectionId is Guid connectionId)
        {
            Connection? connection = drawing.Connections.SingleOrDefault(item => item.Id == connectionId);
            Guid? lineTerminal = interval.CableTerminalId;
            if (connection is null || lineTerminal is null || !connection.UsesTerminal(lineTerminal.Value))
                return $"{device.DisplayName} 的连接 {connectionId} 无法证明所选线路侧";
            if (boundary.Side != BoundarySide.Line)
                return $"{device.DisplayName} 的连接与所选母线侧不一致";
        }
        if (boundary.TerminalId is Guid terminalId)
        {
            if (!device.OwnsTerminal(terminalId)) return $"{device.DisplayName} 的边界端子不属于该设备";
            BoundarySide actual = terminalId == device.FirstTerminalId ? BoundarySide.Bus : BoundarySide.Line;
            if (actual != boundary.Side) return $"{device.DisplayName} 的边界端子与 {boundary.Side} 侧不一致";
        }
        return null;
    }
    public IEnumerable<MeasureFact> BoundaryDecorations(WorkTicketSession ticket, IsolationBoundary boundary) =>
        ticket.UserFacts.Where(item => item.Kind == "RedCloth61" && item.Confirmed &&
            item.References.Any(reference => reference.Kind == TicketReferenceKind.Device &&
                reference.Id == boundary.DeviceId))
            .Select(item => new MeasureFact(Guid.NewGuid(), "6.1", MeasureKind.RedCloth,
                "red-cloth-boundary", item.References, item.Text,
                Origin: FactOrigin.UserAdded));

    public IEnumerable<MeasureFact> Switching(DrawingDocument drawing, IsolationBoundary boundary)
    {
        SwitchDevice? switchDevice = drawing.Devices.OfType<SwitchDevice>()
            .SingleOrDefault(device => device.Id == boundary.DeviceId);
        if (switchDevice is null)
            throw new InvalidOperationException($"隔离边界设备 {boundary.DeviceId} 不是现有开关设备。");
        if (switchDevice.SwitchKind == SwitchKind.GroundSwitch) yield break;

        string? boundaryIssue = BoundaryIssue(drawing, boundary);
        bool groundedLineSide = boundary.Side == BoundarySide.Line && boundaryIssue is null;

        RingCabinetInterval? interval = drawing.Devices.OfType<RingCabinet>()
            .SelectMany(cabinet => cabinet.Intervals)
            .SingleOrDefault(item => item.SwitchDevices.Any(device => device.Id == switchDevice.Id));
        if (interval is null)
        {
            yield return Create("6.1", MeasureKind.OpenSwitch, "open", switchDevice);
            if (switchDevice.SwitchKind == SwitchKind.DropoutFuse)
                yield return Create("6.1", MeasureKind.RemoveFuseTube, "remove-fuse-tube", switchDevice,
                    FactOrigin.SystemRecommendation);
            yield return Create("6.1", MeasureKind.Sign, "lock-open", switchDevice);
            yield break;
        }

        SwitchDevice? Find(SwitchKind kind) => interval.SwitchDevices.SingleOrDefault(item => item.SwitchKind == kind);
        SwitchDevice? breaker = Find(SwitchKind.CircuitBreaker);
        SwitchDevice? isolator = Find(SwitchKind.IsolationSwitch);
        SwitchDevice? load = Find(SwitchKind.LoadSwitch);
        SwitchDevice? ground = Find(SwitchKind.GroundSwitch);
        if (load is not null)
        {
            yield return Create("6.1", MeasureKind.OpenSwitch, "open", load);
            if (ground is not null && groundedLineSide)
                yield return Create("6.1", MeasureKind.CloseGroundSwitch, "close-ground", ground);
            yield return Create("6.1", MeasureKind.Sign, "lock-open", load);
            yield break;
        }
        if (breaker is not null && isolator is not null && ground is not null)
        {
            yield return Create("6.1", MeasureKind.OpenSwitch, "open", breaker);
            yield return Create("6.1", MeasureKind.OpenSwitch, "open", isolator);
            if (groundedLineSide)
                yield return Create("6.1", MeasureKind.CloseGroundSwitch, "close-ground", ground);
            if (groundedLineSide && interval.GroundingStructureKind == GroundingStructureKind.UpperIsolationGrounding)
            {
                yield return Create("6.1", MeasureKind.CloseBreaker, "close-breaker-ground-path", breaker);
                yield return Create("6.1", MeasureKind.Sign, "lock-open", isolator);
                yield return Create("6.1", MeasureKind.Sign, "lock-closed", breaker);
            }
            else
            {
                yield return Create("6.1", MeasureKind.Sign, "lock-open", isolator);
            }
            yield break;
        }
        throw new InvalidOperationException($"间隔 {interval.IntervalId} 的结构不在当前规则包支持范围。");
    }

    public IEnumerable<MeasureFact> WorkGroup(DrawingDocument drawing, WorkTicketSession ticket)
    {
        foreach (UserTicketFact fact in ticket.UserFacts.Where(item => item.Confirmed &&
                     item.Kind is "SimpleOperation" or "OtherReversible"))
            yield return new MeasureFact(Guid.NewGuid(), "6.3", MeasureKind.Other, "manual-operation",
                fact.References, fact.Text, Reversible: !string.IsNullOrWhiteSpace(fact.RestorationText),
                Origin: FactOrigin.UserAdded, RestorationText: fact.RestorationText);
        foreach (Guid pointId in ticket.GroundingPointIds)
        {
            GroundingPoint point = drawing.GroundingPoints.SingleOrDefault(item => item.GroundingPointId == pointId)
                ?? throw new InvalidOperationException($"工作接地点 {pointId} 已不存在。");
            TicketReference[] refs = [new(TicketReferenceKind.GroundingPoint, pointId),
                new(point.Target.Kind == GroundingTargetKind.GroundingAccessPoint
                    ? TicketReferenceKind.GroundingAccessPoint : TicketReferenceKind.Terminal,
                    point.Target.TargetId)];
            yield return new MeasureFact(Guid.NewGuid(), "6.3", MeasureKind.Check, "check-isolation", refs, point.Location);
            yield return new MeasureFact(Guid.NewGuid(), "6.3", MeasureKind.VerifyDead, "verify-dead", refs, point.Location);
            yield return new MeasureFact(Guid.NewGuid(), "6.3", MeasureKind.InstallGround, "install-ground", refs,
                point.Location, point.Number, Reversible: true);
        }
        foreach (UserTicketFact fact in ticket.UserFacts.Where(item => item.Confirmed &&
                     item.Kind is "Barrier" or "Sign" or "RedCloth63"))
        {
            MeasureKind kind = fact.Kind switch
            {
                "Barrier" => MeasureKind.Barrier, "Sign" => MeasureKind.Sign, _ => MeasureKind.RedCloth
            };
            yield return new MeasureFact(Guid.NewGuid(), "6.3", kind,
                fact.Kind == "RedCloth63" ? "RedCloth" : fact.Kind, fact.References,
                fact.Text, Reversible: true, Origin: FactOrigin.UserAdded);
        }
    }

    public IEnumerable<RetainedLivePart> RetainedLive(DrawingDocument drawing, WorkTicketSession ticket)
    {
        HashSet<Guid> reportedAdjacentIntervals = [];
        foreach (UserTicketFact fact in ticket.UserFacts.Where(item => item.Kind == "RetainedLive"))
            yield return new RetainedLivePart(fact.Text, fact.References,
                fact.Confirmed ? FactOrigin.UserAdded : FactOrigin.SystemRecommendation);
        // A chosen isolation boundary alone cannot prove that its far side remains energized.
        // Report that as a recommendation requiring site confirmation, not a model fact.
        foreach (IsolationBoundary boundary in ticket.IsolationBoundaries)
        {
            SwitchDevice device = drawing.Devices.OfType<SwitchDevice>().Single(item => item.Id == boundary.DeviceId);
            Terminal? terminal = boundary.TerminalId is Guid terminalId
                ? drawing.Terminals.SingleOrDefault(item => item.Id == terminalId) : null;
            ElectricalNode? node = terminal?.ElectricalNodeId is Guid nodeId
                ? drawing.ElectricalNodes.SingleOrDefault(item => item.Id == nodeId) : null;
            if (node?.ElectricalState == ElectricalState.Energized)
            {
                yield return new RetainedLivePart($"{device.DisplayName}{boundary.Side}侧保留带电",
                    [new TicketReference(TicketReferenceKind.Device, device.Id),
                        new TicketReference(TicketReferenceKind.Terminal, terminal!.Id)], FactOrigin.ModelFact);
            }
            else if (!ticket.UserFacts.Any(item => item.Kind == "RetainedLive" && item.Confirmed &&
                         item.References.Any(reference => reference.Kind == TicketReferenceKind.Device &&
                             reference.Id == device.Id)))
                yield return new RetainedLivePart($"请核实 {device.DisplayName} {boundary.Side} 侧是否保留带电",
                    [new TicketReference(TicketReferenceKind.Device, device.Id)], FactOrigin.SystemRecommendation);
            RingCabinet? cabinet = drawing.Devices.OfType<RingCabinet>()
                .SingleOrDefault(item => item.Intervals.Any(interval =>
                    interval.SwitchDevices.Any(sw => sw.Id == device.Id)));
            if (cabinet is null) continue;
            RingCabinetInterval[] ordered = cabinet.Intervals.OrderBy(item => item.Sequence).ToArray();
            int position = Array.FindIndex(ordered, item =>
                item.SwitchDevices.Any(sw => sw.Id == device.Id));
            foreach (int neighborIndex in new[] { position - 1, position + 1 })
            {
                if (neighborIndex < 0 || neighborIndex >= ordered.Length) continue;
                RingCabinetInterval neighbor = ordered[neighborIndex];
                if (!reportedAdjacentIntervals.Add(neighbor.IntervalId)) continue;
                bool explicitlyEnergized = drawing.ElectricalNodes.Any(node =>
                    node.OwnerId == neighbor.IntervalId && node.ElectricalState == ElectricalState.Energized);
                if (explicitlyEnergized)
                    yield return new RetainedLivePart($"相邻{neighbor.DisplayName}保留带电",
                        [new TicketReference(TicketReferenceKind.RingInterval, neighbor.IntervalId)],
                        FactOrigin.ModelFact);
                else if (neighbor.SwitchAssembly.Evaluate().OperationalState == OperationalState.Running)
                    yield return new RetainedLivePart($"相邻{neighbor.DisplayName}处于运行结构；请核实实际带电",
                        [new TicketReference(TicketReferenceKind.RingInterval, neighbor.IntervalId)],
                        FactOrigin.SystemRecommendation);
            }
        }
    }

    public IEnumerable<RiskItem> Risks(DrawingDocument drawing, WorkTicketSession ticket) =>
        ticket.UserFacts.Where(item => item.Kind == "Risk")
            .Select(item => new RiskItem(item.Text, null, FactOrigin.UserAdded));

    public string CoordinatedOutage(DrawingDocument drawing, WorkTicketSession ticket) => "无";

    private static MeasureFact Create(string section, MeasureKind kind, string key, SwitchDevice device,
        FactOrigin origin = FactOrigin.ModelFact) => new(Guid.NewGuid(), section, kind, key,
        [new TicketReference(TicketReferenceKind.Device, device.Id)], Origin: origin);
}

public interface IWorkTicketPhraseLibrary
{
    string Version => "first-kind-phrases-v0.1";
    string Switching(MeasureFact measure, DrawingDocument drawing);
    string WorkGroup(MeasureFact measure, DrawingDocument drawing);
    string Restoration(RestorationMeasure measure, DrawingDocument drawing);
}

public sealed class FirstKindPhraseLibrary : IWorkTicketPhraseLibrary
{
    public string Version => "first-kind-phrases-v0.1";
    public string Switching(MeasureFact measure, DrawingDocument drawing)
    {
        SwitchDevice device = drawing.Devices.OfType<SwitchDevice>()
            .Single(item => item.Id == measure.References.First(reference =>
                reference.Kind == TicketReferenceKind.Device).Id);
        string name = device.DispatchNumber ?? device.DisplayName ?? device.Id.ToString();
        return measure.PhraseKey switch
        {
            "open" => $"拉开{name}{Noun(device.SwitchKind)}",
            "close-ground" => $"合上{name}接地刀闸",
            "close-breaker-ground-path" => $"合上{name}开关（建立接地通路）",
            "remove-fuse-tube" => $"取下{name}保险熔管（现场确认）",
            "lock-open" => $"在{name}操作处悬挂“禁止合闸，有人工作！”标示牌并加锁（具备条件时）",
            "lock-closed" => $"在{name}操作处悬挂“禁止分闸！”标示牌并加锁（具备条件时）",
            "red-cloth-boundary" => $"在{measure.Location}设置红布幔",
            _ => throw new InvalidOperationException($"未知措辞键 {measure.PhraseKey}。")
        };
    }

    public string WorkGroup(MeasureFact measure, DrawingDocument drawing) => measure.PhraseKey switch
    {
        "check-isolation" => $"检查{measure.Location}相关隔离措施已完成",
        "verify-dead" => $"在{measure.Location}验明确无电压",
        "install-ground" => $"在{measure.Location}装设{measure.Number ?? "待填写编号"}工作接地线",
        "Barrier" => $"设置围栏：{measure.Location}",
        "Sign" => $"悬挂标示牌：{measure.Location}",
        "RedCloth" => $"设置红布幔：{measure.Location}",
        "manual-operation" => measure.Location ?? throw new InvalidOperationException("Manual operation text is required."),
        _ => throw new InvalidOperationException($"未知措辞键 {measure.PhraseKey}。")
    };

    public string Restoration(RestorationMeasure measure, DrawingDocument drawing) => measure.PhraseKey switch
    {
        "install-ground" => $"拆除{measure.Location}{measure.Number ?? "待填写编号"}工作接地线",
        "Barrier" => $"拆除围栏：{measure.Location}",
        "Sign" => $"取下标示牌：{measure.Location}",
        "RedCloth" => $"撤除红布幔：{measure.Location}",
        "manual-operation" => measure.RestorationText ?? throw new InvalidOperationException(
            "Manual restoration text is required."),
        _ => throw new InvalidOperationException($"措施 {measure.PhraseKey} 不可自动反向生成。")
    };

    private static string Noun(SwitchKind kind) => kind switch
    {
        SwitchKind.IsolationSwitch => "刀闸",
        SwitchKind.GroundSwitch => "接地刀闸",
        SwitchKind.DropoutFuse => "保险",
        _ => "开关"
    };
}

public sealed class WorkTicketAnalyzer(
    IWorkTicketRulePack? rules = null, IWorkTicketPhraseLibrary? phrases = null,
    IRiskRulePack? riskRules = null)
{
    private readonly IWorkTicketRulePack _rules = rules ?? new FirstKindRulePack();
    private readonly IWorkTicketPhraseLibrary _phrases = phrases ?? new FirstKindPhraseLibrary();
    private readonly IRiskRulePack _riskRules = riskRules ?? new FirstKindRulePack();
    private string GenerationRuleVersion => $"{_rules.Version}|{_riskRules.Version}";

    public WorkTicketSession Analyze(DrawingDocument drawing, WorkTicketSession ticket)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(ticket);
        if (string.IsNullOrWhiteSpace(ticket.Task.Content) || string.IsNullOrWhiteSpace(ticket.Task.WorkObject))
            throw new InvalidOperationException("先填写工作内容和工作对象。");
        if (ticket.IsolationBoundaries.Count == 0 ||
            (ticket.WorkScopeIds.Count == 0 && ticket.WorkScopeItems.Count == 0))
            throw new InvalidOperationException("先设置停电/隔离边界及实际工作范围。");
        if (ticket.UserFacts.Any(item => item.Kind == "OtherReversible" && item.Confirmed &&
                string.IsNullOrWhiteSpace(item.RestorationText)))
            throw new InvalidOperationException("可恢复措施需要填写对应的现场恢复文字。");
        foreach (Guid scopeId in ticket.WorkScopeIds)
            if (!drawing.WorkScopes.Any(scope => scope.WorkScopeId == scopeId))
                throw new InvalidOperationException($"工作范围 {scopeId} 已不存在。");
        foreach (WorkScopeItem item in ticket.WorkScopeItems)
        {
            if (item.TargetId == Guid.Empty || !Enum.IsDefined(item.Kind))
                throw new InvalidOperationException("实际工作范围引用无效。");
            bool exists = item.Kind switch
            {
                WorkScopeItemKind.Equipment => drawing.Devices.Any(device => device.Id == item.TargetId),
                WorkScopeItemKind.ElectricalRange => drawing.WorkScopes.Any(scope => scope.WorkScopeId == item.TargetId),
                _ => false
            };
            if (!exists) throw new InvalidOperationException($"工作范围目标 {item.TargetId} 已不存在。");
        }
        foreach (Guid deviceId in ticket.EquipmentScopeIds)
            if (!drawing.Devices.Any(device => device.Id == deviceId))
                throw new InvalidOperationException($"工作设备 {deviceId} 已不存在。");

        MeasureFact[] switching = ticket.IsolationBoundaries
            .SelectMany(boundary => _rules.Switching(drawing, boundary)
                .Concat(_rules.BoundaryDecorations(ticket, boundary)))
            .Select((item, index) => item with { Id = StableMeasureId(ticket.Id, item, index) }).ToArray();
        MeasureFact[] workGroup = _rules.WorkGroup(drawing, ticket)
            .Select((item, index) => item with { Id = StableMeasureId(ticket.Id, item, index) }).ToArray();
        RetainedLivePart[] live = _rules.RetainedLive(drawing, ticket).ToArray();
        RiskItem[] risks = _riskRules.Risks(drawing, ticket).ToArray();
        RestorationMeasure[] restoration = workGroup.Where(item => item.Reversible)
            .Reverse().Select(item => new RestorationMeasure(item.Id, item.PhraseKey,
                item.References, item.Location, item.Number, item.Origin,
                item.RestorationText)).ToArray();
        string[] issues = ticket.IsolationBoundaries
            .Select(boundary => _rules.BoundaryIssue(drawing, boundary))
            .OfType<string>()
            .Concat(live.Where(item => item.Origin == FactOrigin.SystemRecommendation)
                .Select(item => item.Description))
            .Concat(live.Where(item => item.Origin is FactOrigin.ModelFact or FactOrigin.UserAdded &&
                !ticket.UserFacts.Any(fact => fact.Kind == "RedCloth61" && fact.Confirmed &&
                    fact.References.Any(reference => item.References.Contains(reference))))
                .Select(item => $"建议在{item.Description}邻近处设置红布幔；请确认具体位置"))
            .Concat(ticket.Draft?.Sections.FirstOrDefault(section => section.Code == "6.3")?
                .Items.Any(item => item.IsUserEdited) == true
                ? ["6.3 存在人工改写；请逐项核对 16.1 的编号、数量、位置和目标"] : [])
            .ToArray();
        WorkTicketAnalysis analysis = new(switching, workGroup, live, risks, restoration, issues);

        SectionDraft Make(string code,
            IEnumerable<(string Text, IReadOnlyList<TicketReference> Refs, Guid? MeasureId, FactOrigin Source)> values,
            SectionCompletion completion) => new(code, values.Select(value => new DraftItem(Guid.NewGuid(), value.Text,
                value.Text, DraftOrigin.RuleDerived, value.Refs, value.MeasureId)
                { Source = value.Source }).ToArray(), completion);
        SectionDraft[] sections =
        [
            Make("6.1", switching.Select(item => (_phrases.Switching(item, drawing), item.References,
                    (Guid?)item.Id, item.Origin)),
                switching.Length == 0 || ticket.IsolationBoundaries.Any(boundary =>
                    _rules.BoundaryIssue(drawing, boundary) is not null)
                    ? SectionCompletion.NeedsInput : SectionCompletion.NeedsConfirmation),
            Make("6.2", [(_rules.CoordinatedOutage(drawing, ticket), Array.Empty<TicketReference>(),
                null, FactOrigin.SystemRecommendation)], SectionCompletion.NeedsConfirmation),
            Make("6.3", workGroup.Select(item => (_phrases.WorkGroup(item, drawing), item.References,
                    (Guid?)item.Id, item.Origin)),
                workGroup.Length == 0 ? SectionCompletion.NeedsInput :
                workGroup.Any(item => item.Number is null && item.Kind == MeasureKind.InstallGround)
                    ? SectionCompletion.NeedsInput : SectionCompletion.NeedsConfirmation),
            Make("6.4", live.Select(item => (item.Description, item.References, (Guid?)null, item.Origin)),
                live.Length == 0 || live.Any(item => item.Origin == FactOrigin.SystemRecommendation)
                    ? SectionCompletion.NeedsConfirmation : SectionCompletion.Generated),
            Make("6.5", risks.Select(item => (item.Description,
                (IReadOnlyList<TicketReference>)Array.Empty<TicketReference>(), (Guid?)null, item.Origin)),
                risks.Length == 0 ? SectionCompletion.NeedsInput :
                ticket.UserFacts.Any(item => item.Kind == "Risk" && !item.Confirmed)
                    ? SectionCompletion.NeedsConfirmation : SectionCompletion.Generated),
            Make("16.1", restoration.Select(item => (_phrases.Restoration(item, drawing), item.References,
                    (Guid?)item.SourceMeasureId, item.Origin)),
                restoration.Length == 0 || restoration.Any(item =>
                    item.PhraseKey == "install-ground" && item.Number is null)
                    ? SectionCompletion.NeedsInput : SectionCompletion.NeedsConfirmation)
        ];
        // Preserve an edited text as a separate current value when analyzing again.
        if (ticket.Draft is not null)
        {
            List<string> orphanedIssues = [];
            sections = sections.Select(section =>
            {
                SectionDraft previous = ticket.Draft.Sections.Single(old => old.Code == section.Code);
                if (previous.Completion == SectionCompletion.Completed &&
                    ticket.AnalyzedFingerprint == Fingerprint(drawing, ticket) &&
                    ticket.RulePackVersion == GenerationRuleVersion &&
                    ticket.PhraseLibraryVersion == _phrases.Version &&
                    previous.Items.All(item => item.Origin == DraftOrigin.Confirmed) &&
                    previous.Items.Select(item => item.GeneratedText).SequenceEqual(
                        section.Items.Select(item => item.GeneratedText)))
                    return previous;
                HashSet<Guid> matched = [];
                DraftItem[] merged = section.Items.Select(item =>
                {
                    DraftItem? old = previous.Items.FirstOrDefault(candidate =>
                        candidate.SourceMeasureId == item.SourceMeasureId &&
                        candidate.SourceMeasureId is not null && matched.Add(candidate.Id));
                    old ??= previous.Items.FirstOrDefault(candidate =>
                        item.SourceMeasureId is null && candidate.SourceMeasureId is null &&
                        candidate.GeneratedText == item.GeneratedText && candidate.Source == item.Source &&
                        candidate.RelatedModelRefs.SequenceEqual(item.RelatedModelRefs) &&
                        matched.Add(candidate.Id));
                    return old?.IsUserEdited == true
                        ? item with { Id = old.Id, CurrentText = old.CurrentText,
                            Origin = old.Origin, IsUserEdited = true }
                        : old is not null ? item with { Id = old.Id } : item;
                }).ToArray();
                DraftItem[] unmatchedEdits = previous.Items.Where(item =>
                    item.IsUserEdited && !matched.Contains(item.Id)).Select(item =>
                    item.GeneratedText.Length > 0 || item.SourceMeasureId is not null
                        ? item with { Source = FactOrigin.UserAdded, Origin = DraftOrigin.UserEdited }
                        : item).ToArray();
                if (unmatchedEdits.Any(item => item.GeneratedText.Length > 0 ||
                    item.SourceMeasureId is not null))
                    orphanedIssues.Add($"{section.Code} 的来源事实已变化；保留人工改写内容，请核实后确认");
                merged = [..merged, ..unmatchedEdits];
                bool manual = merged.Any(item => item.IsUserEdited);
                SectionCompletion completion = manual ? SectionCompletion.NeedsConfirmation : section.Completion;
                if (section.Code == "16.1" && ticket.Draft.Section("6.3").Items.Any(item =>
                        item.IsUserEdited))
                    completion = SectionCompletion.Stale;
                return section with { Items = merged, Completion = completion };
            }).ToArray();
            if (orphanedIssues.Count > 0)
                analysis = analysis with { Issues = [..analysis.Issues, ..orphanedIssues] };
        }
        return ticket with { Analysis = analysis, Draft = new WorkTicketDraft(sections),
            AnalyzedFingerprint = Fingerprint(drawing, ticket), RulePackVersion = GenerationRuleVersion,
            PhraseLibraryVersion = _phrases.Version };
    }

    public WorkTicketSession RefreshStale(DrawingDocument drawing, WorkTicketSession ticket) =>
        IsStale(drawing, ticket)
            ? ticket.Invalidate() : ticket;

    public bool IsStale(DrawingDocument drawing, WorkTicketSession ticket) =>
        ticket.AnalyzedFingerprint is not null &&
        (ticket.AnalyzedFingerprint != Fingerprint(drawing, ticket) ||
         ticket.RulePackVersion != GenerationRuleVersion || ticket.PhraseLibraryVersion != _phrases.Version);

    private static Guid StableMeasureId(Guid ticketId, MeasureFact measure, int index)
    {
        string source = $"{ticketId}|{measure.Section}|{index}|{measure.PhraseKey}|" +
            string.Join(';', measure.References.Select(reference => $"{reference.Kind}:{reference.Id}"));
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(source)).AsSpan(0, 16));
    }

    public static string Fingerprint(DrawingDocument drawing, WorkTicketSession ticket)
    {
        StringBuilder value = new();
        value.Append(ticket.Task).Append('|');
        foreach (IsolationBoundary item in ticket.IsolationBoundaries) value.Append(item).Append('|');
        foreach (Guid id in ticket.WorkScopeIds) value.Append(id).Append('|');
        foreach (WorkScopeItem item in ticket.WorkScopeItems) value.Append(item.Kind).Append(':').Append(item.TargetId).Append('|');
        foreach (Guid id in ticket.GroundingPointIds) value.Append(id).Append('|');
        foreach (UserTicketFact item in ticket.UserFacts)
        {
            value.Append(item.Kind).Append(item.Text).Append(item.Confirmed).Append(item.RestorationText);
            foreach (TicketReference reference in item.References) value.Append(reference);
        }
        foreach (Device item in drawing.Devices.OrderBy(item => item.Id))
        {
            value.Append(item.Id).Append(item.DisplayName).Append(item.SwitchState);
            if (item is SwitchDevice sw) value.Append(sw.SwitchKind).Append(sw.DispatchNumber);
            if (item is RingCabinet cabinet)
                foreach (RingCabinetInterval interval in cabinet.Intervals.OrderBy(item => item.Sequence))
                    value.Append(interval.IntervalId).Append(interval.IntervalKind).Append(interval.GroundingStructureKind)
                        .Append(interval.Sequence).Append(interval.DisplayName);
        }
        foreach (var item in drawing.Connections.OrderBy(item => item.Id))
            value.Append(item.Id).Append(item.StartTerminalId).Append(item.EndTerminalId);
        foreach (ElectricalNode item in drawing.ElectricalNodes.OrderBy(item => item.Id))
            value.Append(item.Id).Append(item.ElectricalState);
        foreach (Terminal item in drawing.Terminals.OrderBy(item => item.Id))
            value.Append(item.Id).Append(item.OwnerId).Append(item.Role).Append(item.ElectricalNodeId);
        foreach (GroundingAccessPoint item in drawing.GroundingAccessPoints.OrderBy(item => item.GroundingAccessPointId))
            value.Append(item.GroundingAccessPointId).Append(item.ConnectionId).Append(item.PoleId)
                .Append(item.LineSide).Append(item.PlacementSide).Append(item.AdjacentEndpoint);
        foreach (OverheadLine item in drawing.OverheadLines.OrderBy(item => item.ConnectionId))
            value.Append(item.ConnectionId).Append(item.ContinuationState).Append(item.ContinuationTerminalId);
        foreach (WorkScope item in drawing.WorkScopes.OrderBy(item => item.WorkScopeId))
            value.Append(item.WorkScopeId).Append(item.StartBoundary).Append(item.EndBoundary).Append(item.Description);
        foreach (GroundingPoint item in drawing.GroundingPoints.OrderBy(item => item.GroundingPointId))
            value.Append(item.GroundingPointId).Append(item.Target).Append(item.Location).Append(item.Number);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString())));
    }
}
