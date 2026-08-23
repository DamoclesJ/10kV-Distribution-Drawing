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

            int[] supported = SupportedBusinessIntervalCounts.ToArray();
            if (!supported.Contains(BusinessIntervalCount))
            {
                BusinessIntervalCount = supported[0];
            }

            if (!IsPrimarySecondaryIntegrated)
            {
                IncludePTInterval = false;
            }

            OnPropertyChanged(nameof(SupportedBusinessIntervalCounts));
            OnPropertyChanged(nameof(IsPrimarySecondaryIntegrated));
            OnPropertyChanged(nameof(GeneratedIntervalNames));
        }
    }

    public IReadOnlyList<int> SupportedBusinessIntervalCounts =>
        CabinetType == RingCabinetTemplateType.Conventional
            ? [3, 4, 5, 6]
            : [4, 6];

    public int BusinessIntervalCount
    {
        get => _businessIntervalCount;
        set
        {
            if (SetField(ref _businessIntervalCount, value))
            {
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
                OnPropertyChanged(nameof(GeneratedIntervalNames));
            }
        }
    }

    public string GeneratedIntervalNames
    {
        get
        {
            IEnumerable<string> names = Enumerable.Range(1, BusinessIntervalCount)
                .Select(index => $"负{index}");
            if (IncludePTInterval)
            {
                names = names.Append("PT");
            }

            return string.Join("、", names);
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

        try
        {
            RingCabinetTemplate template = _templateFactory.Create(
                CabinetType,
                BusinessIntervalCount,
                IntegratedGroundingStructureKind,
                IncludePTInterval);
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
