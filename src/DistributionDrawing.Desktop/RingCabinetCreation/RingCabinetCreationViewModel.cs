using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;

namespace DistributionDrawing.Desktop.RingCabinetCreation;

public sealed class RingCabinetCreationViewModel : INotifyPropertyChanged
{
    private string _displayName = string.Empty;

    public RingCabinetCreationViewModel()
    {
        Intervals.CollectionChanged += (_, _) =>
        {
            UpdateSequences();
            OnPropertyChanged(nameof(IntervalCount));
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (_displayName == value)
            {
                return;
            }

            _displayName = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<RingCabinetIntervalCreationRowViewModel> Intervals { get; } = [];

    public int IntervalCount => Intervals.Count;

    public void AddInterval()
    {
        Intervals.Add(new RingCabinetIntervalCreationRowViewModel());
    }

    public void RemoveInterval(RingCabinetIntervalCreationRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        Intervals.Remove(row);
    }

    public void MoveUp(RingCabinetIntervalCreationRowViewModel row)
    {
        Move(row, -1);
    }

    public void MoveDown(RingCabinetIntervalCreationRowViewModel row)
    {
        Move(row, 1);
    }

    public bool TryCreateConfiguration(
        out RingCabinetCreationConfiguration? configuration,
        out string errorMessage)
    {
        configuration = null;
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            errorMessage = "请输入环网柜名称。";
            return false;
        }

        if (Intervals.Count == 0)
        {
            errorMessage = "请至少添加一个间隔。";
            return false;
        }

        var bayIndexes = new HashSet<int>();
        foreach (RingCabinetIntervalCreationRowViewModel row in Intervals)
        {
            if (!int.TryParse(
                    row.BayIndexText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int bayIndex) ||
                bayIndex < 1)
            {
                errorMessage = $"请输入第 {row.Sequence} 个间隔的正整数业务编号。";
                return false;
            }

            if (!bayIndexes.Add(bayIndex))
            {
                errorMessage = $"间隔业务编号 {bayIndex} 重复。";
                return false;
            }

            if (row.Function is not BayFunction function ||
                !Enum.IsDefined(function) ||
                function is BayFunction.Unknown or BayFunction.PT)
            {
                errorMessage = $"请选择第 {row.Sequence} 个间隔的电气功能。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(row.DisplayName))
            {
                errorMessage = $"请输入第 {row.Sequence} 个间隔的名称。";
                return false;
            }

            if (!Enum.IsDefined(row.IntervalKind))
            {
                errorMessage = $"第 {row.Sequence} 个间隔的类型无效。";
                return false;
            }

            if (row.IntervalKind == IntervalKind.IntegratedFeederInterval)
            {
                if (row.GroundingStructureKind is not GroundingStructureKind structure ||
                    !Enum.IsDefined(structure))
                {
                    errorMessage = $"请选择第 {row.Sequence} 个融合间隔的接地结构。";
                    return false;
                }
            }
            else if (row.GroundingStructureKind is not null)
            {
                errorMessage = $"第 {row.Sequence} 个负荷开关间隔不能设置接地结构。";
                return false;
            }
        }

        bool loadSwitchOnly = Intervals.All(
            row => row.IntervalKind == IntervalKind.LoadSwitchInterval);
        if (loadSwitchOnly && Intervals.Count is < 3 or > 6)
        {
            errorMessage = "纯负荷开关柜必须包含 3、4、5 或 6 个间隔。";
            return false;
        }

        bool integratedFeederOnly = Intervals.All(
            row => row.IntervalKind == IntervalKind.IntegratedFeederInterval);
        if (integratedFeederOnly && Intervals.Count is not (4 or 6))
        {
            errorMessage = "纯一二次融合柜必须包含 4 或 6 个间隔。";
            return false;
        }

        configuration = new RingCabinetCreationConfiguration(
            DisplayName.Trim(),
            Intervals.Select(row => new RingCabinetIntervalCreationConfiguration(
                int.Parse(row.BayIndexText, NumberStyles.Integer, CultureInfo.InvariantCulture),
                row.Function!.Value,
                row.DisplayName.Trim(),
                row.IntervalKind,
                row.IntervalKind == IntervalKind.IntegratedFeederInterval
                    ? row.GroundingStructureKind
                    : null)));
        errorMessage = string.Empty;
        return true;
    }

    private void Move(RingCabinetIntervalCreationRowViewModel row, int offset)
    {
        ArgumentNullException.ThrowIfNull(row);
        int oldIndex = Intervals.IndexOf(row);
        int newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= Intervals.Count)
        {
            return;
        }

        Intervals.Move(oldIndex, newIndex);
        UpdateSequences();
    }

    private void UpdateSequences()
    {
        for (int index = 0; index < Intervals.Count; index++)
        {
            Intervals[index].Sequence = index + 1;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class RingCabinetIntervalCreationRowViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyList<IntervalKind> SupportedIntervalKinds =
        Array.AsReadOnly(Enum.GetValues<IntervalKind>());
    private static readonly IReadOnlyList<GroundingStructureKind> SupportedGroundingStructures =
        Array.AsReadOnly(Enum.GetValues<GroundingStructureKind>());
    private static readonly IReadOnlyList<BayFunction> SupportedFunctions = Array.AsReadOnly(
        Enum.GetValues<BayFunction>()
            .Where(function => function is not BayFunction.Unknown and not BayFunction.PT)
            .ToArray());

    private int _sequence;
    private string _bayIndexText = string.Empty;
    private BayFunction? _function;
    private string _displayName = string.Empty;
    private IntervalKind _intervalKind = IntervalKind.LoadSwitchInterval;
    private GroundingStructureKind? _groundingStructureKind;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Sequence
    {
        get => _sequence;
        internal set
        {
            if (_sequence == value)
            {
                return;
            }

            _sequence = value;
            OnPropertyChanged();
        }
    }

    public string BayIndexText
    {
        get => _bayIndexText;
        set
        {
            if (_bayIndexText == value)
            {
                return;
            }

            _bayIndexText = value;
            OnPropertyChanged();
        }
    }

    public BayFunction? Function
    {
        get => _function;
        set
        {
            if (_function == value)
            {
                return;
            }

            _function = value;
            OnPropertyChanged();
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (_displayName == value)
            {
                return;
            }

            _displayName = value;
            OnPropertyChanged();
        }
    }

    public IntervalKind IntervalKind
    {
        get => _intervalKind;
        set
        {
            if (_intervalKind == value)
            {
                return;
            }

            _intervalKind = value;
            if (!IsIntegratedFeeder)
            {
                GroundingStructureKind = null;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsIntegratedFeeder));
        }
    }

    public GroundingStructureKind? GroundingStructureKind
    {
        get => _groundingStructureKind;
        set
        {
            if (_groundingStructureKind == value)
            {
                return;
            }

            _groundingStructureKind = value;
            OnPropertyChanged();
        }
    }

    public bool IsIntegratedFeeder => IntervalKind == IntervalKind.IntegratedFeederInterval;

    public IReadOnlyList<IntervalKind> IntervalKinds => SupportedIntervalKinds;

    public IReadOnlyList<GroundingStructureKind> GroundingStructureKinds =>
        SupportedGroundingStructures;

    public IReadOnlyList<BayFunction> Functions => SupportedFunctions;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
