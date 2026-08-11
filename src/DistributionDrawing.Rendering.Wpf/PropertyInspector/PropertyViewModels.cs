using System.ComponentModel;
using System.Runtime.CompilerServices;
using DistributionDrawing.Rendering.Wpf.Interaction;

namespace DistributionDrawing.Rendering.Wpf.PropertyInspector;

public enum PropertyValueSource
{
    Domain,
    Layout,
    Rendering,
    Derived
}

public sealed record PropertyRowViewModel(
    string PropertyKey,
    string DisplayName,
    string DisplayValue,
    PropertyValueSource Source,
    bool IsReadOnly = true);

public sealed record PropertySectionViewModel(
    string Title,
    IReadOnlyList<PropertyRowViewModel> Properties);

public sealed record PropertyInspectorSnapshot(
    SelectionReference? Selection,
    string ObjectType,
    string ObjectTitle,
    IReadOnlyList<PropertySectionViewModel> Sections);

public sealed class PropertyInspectorViewModel : INotifyPropertyChanged
{
    private SelectionReference? _selection;
    private string _objectType = "未选择对象";
    private string _objectTitle = "请在画布中选择对象";
    private IReadOnlyList<PropertySectionViewModel> _sections = [];

    public SelectionReference? Selection
    {
        get => _selection;
        private set => SetField(ref _selection, value);
    }

    public string ObjectType
    {
        get => _objectType;
        private set => SetField(ref _objectType, value);
    }

    public string ObjectTitle
    {
        get => _objectTitle;
        private set => SetField(ref _objectTitle, value);
    }

    public IReadOnlyList<PropertySectionViewModel> Sections
    {
        get => _sections;
        private set => SetField(ref _sections, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Apply(PropertyInspectorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Selection = snapshot.Selection;
        ObjectType = snapshot.ObjectType;
        ObjectTitle = snapshot.ObjectTitle;
        Sections = snapshot.Sections;
    }

    public void Clear()
    {
        Apply(
            new PropertyInspectorSnapshot(
                null,
                "未选择对象",
                "请在画布中选择对象",
                []));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}
