using Microsoft.Maui.Controls;

namespace mindvault.Utils;

/// <summary>
/// Helper class for adding accessibility features (tooltips, semantic descriptions) to UI elements
/// </summary>
public static class AccessibilityHelper
{
    /// <summary>
    /// Adds both tooltip and semantic description to an element for full accessibility
    /// </summary>
    public static void SetAccessible(Element element, string description, string? hint = null)
    {
        if (element == null) return;

        // Set tooltip (shows on hover on desktop)
        SemanticProperties.SetDescription(element, description);
        
        // Set hint (additional context for screen readers)
        if (!string.IsNullOrEmpty(hint))
        {
            SemanticProperties.SetHint(element, hint);
        }

#if WINDOWS || MACCATALYST
        // Add tooltip for desktop platforms
        if (element is VisualElement visualElement)
        {
            ToolTipProperties.SetText(visualElement, description);
        }
#endif
    }

    /// <summary>
    /// Sets heading level for screen readers (important for navigation)
    /// </summary>
    public static void SetHeading(Element element, SemanticHeadingLevel level = SemanticHeadingLevel.Level1)
    {
        if (element == null) return;
        SemanticProperties.SetHeadingLevel(element, level);
    }

    /// <summary>
    /// Marks an element as a button for screen readers
    /// </summary>
    public static void SetButton(Element element, string description)
    {
        if (element == null) return;
        SetAccessible(element, description);
    }

    /// <summary>
    /// Marks an interactive element with its current state
    /// </summary>
    public static void SetState(Element element, string state)
    {
        if (element == null) return;
        // This helps screen readers announce the current state
        var currentDesc = SemanticProperties.GetDescription(element);
        if (!string.IsNullOrEmpty(currentDesc))
        {
            SemanticProperties.SetDescription(element, $"{currentDesc}, {state}");
        }
    }

    // Common tooltip text constants for consistency
    public static class CommonTooltips
    {
        public const string Save = "Save changes";
        public const string SaveAndNew = "Save and create new (Ctrl+S)";
        public const string Delete = "Delete item";
        public const string Edit = "Edit item";
        public const string Cancel = "Cancel action";
        public const string Close = "Close";
        public const string Menu = "Open menu";
        public const string Search = "Search";
        public const string Back = "Go back";
        public const string Next = "Next";
        public const string Previous = "Previous";
        public const string Add = "Add new item";
        public const string Remove = "Remove item";
        public const string Settings = "Open settings";
        public const string Help = "Show help";
        public const string Export = "Export data";
        public const string Import = "Import data";
    }
}
