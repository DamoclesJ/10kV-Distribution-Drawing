using System.ComponentModel;
using System.Runtime.CompilerServices;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;

namespace DistributionDrawing.Desktop.RingCabinetCreation;

public sealed class RingCabinetCreationViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyList<RingCabinetTemplateType> CabinetTypes =
    [
        RingCabinetTemplateType.Conventional,
        RingCabinetTemplateType.PrimarySecondaryIntegrated
    ];
    private static readonly IReadOnlyList<GroundingStructureKind> GroundingStructures =
        Array.AsReadOnly(Enum.GetValues<GroundingStructureKind>());
    private readonly RingCabinetCreationTemplateFactory _templateFactory;
    private string _displayName = string.Empty;
    private string _lineName = string.Empty;
    private RingCabinetTemplateType _cabinetType = RingCabinetTemplateType.Conventional;
    private int _businessIntervalCount = 3;
    private GroundingStructureKind _integratedGroundingStructureKind =
        GroundingStructureKind.UpperIsolationGrounding;
    private bool _includePTInterval;
    private RingCabinetPTPlacement _ptPlacement = RingCabinetPTPlacement.Right;
    private string _intervalCountText = "3";

    public RingCabinetCreationViewModel(
        RingCabinetCreationTemplateFactory? templateFactory = null)
    {
        _templateFactory = templateFactory ?? new RingCabinetCreationTemplateFactory();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value);
    }

    public string LineName
    {
        get => _lineName;
        set => SetField(ref _lineName, value);
    }

    public IReadOnlyList<RingCabinetTemplateType> SupportedCabinetTypes => CabinetTypes;

    public RingCabinetTemplateType CabinetType
    {
        get => _cabinetType;
        set
        {
            if (!SetField(ref _cabinetType, value))
            {
                return;
            }

            if (IsPrimarySecondaryIntegrated && BusinessIntervalCount == 3)
            {
                BusinessIntervalCount = 4;
            }

            OnPropertyChanged(nameof(IsPrimarySecondaryIntegrated));
            OnPropertyChanged(nameof(GeneratedIntervalNames));
        }
    }

    public IReadOnlyList<int> CommonIntervalCounts => [4, 6];

    public int BusinessIntervalCount
    {
        get => _businessIntervalCount;
        set
        {
            if (SetField(ref _businessIntervalCount, value))
            {
                _intervalCountText = value.ToString();
                OnPropertyChanged(nameof(IntervalCountText));
                OnPropertyChanged(nameof(GeneratedIntervalNames));
            }
        }
    }

    public string IntervalCountText
    {
        get => _intervalCountText;
        set
        {
            if (SetField(ref _intervalCountText, value) &&
                int.TryParse(value, out int count))
            {
                _businessIntervalCount = count;
                OnPropertyChanged(nameof(BusinessIntervalCount));
                OnPropertyChanged(nameof(GeneratedIntervalNames));
            }
        }
    }

    public IReadOnlyList<GroundingStructureKind> SupportedGroundingStructures =>
        GroundingStructures;

    public GroundingStructureKind IntegratedGroundingStructureKind
    {
        get => _integratedGroundingStructureKind;
        set => SetField(ref _integratedGroundingStructureKind, value);
    }

    public bool IsPrimarySecondaryIntegrated =>
        CabinetType == RingCabinetTemplateType.PrimarySecondaryIntegrated;

    public bool IncludePTInterval
    {
        get => _includePTInterval;
        set
        {
            if (SetField(ref _includePTInterval, value))
            {
                OnPropertyChanged(nameof(IsPTPlacementEnabled));
                OnPropertyChanged(nameof(GeneratedIntervalNames));
            }
        }
    }

    public bool IsPTPlacementEnabled => IncludePTInterval;

    public RingCabinetPTPlacement PTPlacement
    {
        get => _ptPlacement;
        set
        {
            if (SetField(ref _ptPlacement, value))
            {
                OnPropertyChanged(nameof(IsPTLeft));
                OnPropertyChanged(nameof(IsPTRight));
                OnPropertyChanged(nameof(GeneratedIntervalNames));
            }
        }
    }

    public bool IsPTLeft
    {
        get => PTPlacement == RingCabinetPTPlacement.Left;
        set
        {
            if (value) PTPlacement = RingCabinetPTPlacement.Left;
        }
    }

    public bool IsPTRight
    {
        get => PTPlacement == RingCabinetPTPlacement.Right;
        set
        {
            if (value) PTPlacement = RingCabinetPTPlacement.Right;
        }
    }

    public string GeneratedIntervalNames
    {
        get
        {
            int count = int.TryParse(IntervalCountText, out int parsed) &&
                        parsed is >= RingCabinetCreationTemplateFactory.MinimumIntervalCount and
                            <= RingCabinetCreationTemplateFactory.MaximumIntervalCount
                ? parsed
                : 0;
            int ptIndex = PTPlacement == RingCabinetPTPlacement.Left ? 1 : count;
            return string.Join("、", Enumerable.Range(1, count)
                .Select(index => IncludePTInterval && index == ptIndex
                    ? "PT"
                    : $"负{index}"));
        }
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

        if (string.IsNullOrWhiteSpace(LineName))
        {
            errorMessage = "请输入线路名称。";
            return false;
        }

        if (!int.TryParse(IntervalCountText, out int intervalCount))
        {
            errorMessage = "请输入有效的间隔数量。";
            return false;
        }

        try
        {
            RingCabinetTemplate template = _templateFactory.Create(
                CabinetType,
                intervalCount,
                IntegratedGroundingStructureKind,
                IncludePTInterval,
                PTPlacement);
            configuration = new RingCabinetCreationConfiguration(
                DisplayName.Trim(),
                template,
                LineName.Trim());
            errorMessage = string.Empty;
            return true;
        }
        catch (ArgumentException exception)
        {
            errorMessage = exception.Message;
            return false;
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
