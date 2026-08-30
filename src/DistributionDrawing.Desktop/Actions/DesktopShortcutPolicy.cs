using System.Windows.Input;

namespace DistributionDrawing.Desktop.Actions;

public enum DesktopShortcutAction
{
    None,
    Undo,
    Redo,
    Copy,
    Paste,
    SelectAll,
    Delete,
    Cancel
}

public static class DesktopShortcutPolicy
{
    public static DesktopShortcutAction Resolve(
        Key key,
        ModifierKeys modifiers,
        bool textInputFocused,
        bool interactionIdle)
    {
        if (key == Key.Escape)
        {
            return !textInputFocused || !interactionIdle
                ? DesktopShortcutAction.Cancel
                : DesktopShortcutAction.None;
        }

        if (textInputFocused)
        {
            return DesktopShortcutAction.None;
        }

        if (modifiers == ModifierKeys.Control)
        {
            return key switch
            {
                Key.Z => DesktopShortcutAction.Undo,
                Key.Y => DesktopShortcutAction.Redo,
                Key.C => DesktopShortcutAction.Copy,
                Key.V => DesktopShortcutAction.Paste,
                Key.A => DesktopShortcutAction.SelectAll,
                _ => DesktopShortcutAction.None
            };
        }

        return modifiers == ModifierKeys.None && key == Key.Delete
            ? DesktopShortcutAction.Delete
            : DesktopShortcutAction.None;
    }
}
