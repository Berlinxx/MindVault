# Quick Reference: Adding Tooltips and Accessibility to XAML Pages

## Basic Pattern

### Button with Tooltip and Accessibility
```xaml
<Button Text="Save"
        Clicked="OnSaveClicked"
        ToolTipProperties.Text="Save changes (Ctrl+S)"
        SemanticProperties.Description="Save button"
        SemanticProperties.Hint="Saves all changes and closes the editor" />
```

### Icon Button (Border with Tap Gesture)
```xaml
<Border WidthRequest="34" HeightRequest="34"
        ToolTipProperties.Text="Delete item"
        SemanticProperties.Description="Delete button"
        SemanticProperties.Hint="Permanently removes this item">
    <Label Text="&#xF2ED;" FontFamily="FAS"/>
    <Border.GestureRecognizers>
        <TapGestureRecognizer Tapped="OnDeleteTapped"/>
    </Border.GestureRecognizers>
</Border>
```

### Page Title
```xaml
<Label Text="Page Title"
       FontSize="20"
       FontAttributes="Bold"
       SemanticProperties.HeadingLevel="Level1"
       SemanticProperties.Description="Page title" />
```

### Section Heading
```xaml
<Label Text="Section Name"
       FontSize="16"
       FontAttributes="Bold"
       SemanticProperties.HeadingLevel="Level2"
       SemanticProperties.Description="Section heading" />
```

### Text Entry Field
```xaml
<Entry Placeholder="Enter username"
       SemanticProperties.Description="Username text field"
       SemanticProperties.Hint="Type your username here" />
```

### Editor Field
```xaml
<Editor Placeholder="Type your content"
        SemanticProperties.Description="Content text field"
        SemanticProperties.Hint="Enter the main content for your post" />
```

### Collection View
```xaml
<CollectionView ItemsSource="{Binding Items}"
                SemanticProperties.Description="Item list"
                SemanticProperties.Hint="Swipe to browse items">
    <!-- ItemTemplate -->
</CollectionView>
```

### Progress Indicator
```xaml
<Border SemanticProperties.Description="Progress indicator">
    <ProgressBar Progress="{Binding CurrentProgress}" />
</Border>
```

## Properties Reference

### ToolTipProperties.Text
- **Platform**: Desktop (Windows, macOS) only
- **When to use**: All interactive elements
- **Format**: Short, clear description. Include keyboard shortcuts in parentheses
- **Examples**:
  - "Save changes"
  - "Delete item (Del)"
  - "Close window (Esc)"
  - "Next page (?)"

### SemanticProperties.Description
- **Platform**: All platforms
- **When to use**: All interactive elements and important UI elements
- **Format**: Brief noun phrase describing the element
- **Examples**:
  - "Save button"
  - "Username text field"
  - "Profile picture"
  - "Navigation menu"

### SemanticProperties.Hint
- **Platform**: All platforms
- **When to use**: When additional context would help understand the action
- **Format**: Action-oriented phrase describing what happens
- **Examples**:
  - "Saves all changes and closes the window"
  - "Opens the settings menu"
  - "Deletes the selected items permanently"
  - "Navigates to the next page"

### SemanticProperties.HeadingLevel
- **Platform**: All platforms
- **Values**: 
  - `Level1`: Main page titles
  - `Level2`: Section headings
  - `Level3-Level6`: Subsection headings (rarely used)
- **When to use**: All headings and titles
- **Examples**:
  - Page titles: `HeadingLevel="Level1"`
  - Section headings: `HeadingLevel="Level2"`

## Writing Guidelines

### Tooltip Text
? **DO**:
- Keep it short (under 50 characters when possible)
- Use action verbs
- Include keyboard shortcuts in parentheses
- Be specific about the action

? **DON'T**:
- Write long sentences
- Use vague terms like "click here"
- Repeat information already visible
- Use technical jargon

**Examples**:
- ? "Save changes (Ctrl+S)"
- ? "This button allows you to save"
- ? "Delete selected items"
- ? "Click to delete"

### Semantic Descriptions
? **DO**:
- Use noun phrases
- Be concise but clear
- Indicate the element type
- Use consistent terminology

? **DON'T**:
- Use full sentences
- Include instructions
- Duplicate the tooltip
- Be overly technical

**Examples**:
- ? "Save button"
- ? "Button that saves your work"
- ? "Room code text field"
- ? "Enter code here"

### Semantic Hints
? **DO**:
- Explain the outcome
- Use complete phrases
- Be specific about what happens
- Include relevant warnings

? **DON'T**:
- Repeat the description
- Be vague
- Use too many words
- Include obvious information

**Examples**:
- ? "Saves all changes and returns to the main page"
- ? "Saves"
- ? "Permanently deletes all selected items"
- ? "Deletes items"

## Common Patterns

### Navigation Buttons
```xaml
<!-- Back/Close -->
<Border ToolTipProperties.Text="Close and return"
        SemanticProperties.Description="Close button"
        SemanticProperties.Hint="Closes this page and returns to previous page">

<!-- Next/Forward -->
<Button ToolTipProperties.Text="Next page"
        SemanticProperties.Description="Next button"
        SemanticProperties.Hint="Proceeds to the next step" />
```

### Action Buttons
```xaml
<!-- Create -->
<Button ToolTipProperties.Text="Create new item"
        SemanticProperties.Description="Create button"
        SemanticProperties.Hint="Creates a new item and opens the editor" />

<!-- Delete -->
<Border ToolTipProperties.Text="Delete item"
        SemanticProperties.Description="Delete button"
        SemanticProperties.Hint="Permanently removes this item from the list">
```

### Form Fields
```xaml
<!-- Text Input -->
<Entry ToolTipProperties.Text="Enter your name"
       SemanticProperties.Description="Name text field"
       SemanticProperties.Hint="Type your full name" />

<!-- Dropdown/Picker -->
<Picker ToolTipProperties.Text="Select category"
        SemanticProperties.Description="Category picker"
        SemanticProperties.Hint="Choose a category from the list" />
```

### Toggle/Checkbox
```xaml
<Switch ToolTipProperties.Text="Enable notifications"
        SemanticProperties.Description="Notifications toggle, currently {Binding IsChecked}"
        SemanticProperties.Hint="Toggles notification settings on or off" />
```

## Keyboard Shortcuts

Common shortcuts to document in tooltips:

### Windows/Linux
- `Ctrl+S` - Save
- `Ctrl+N` - New
- `Ctrl+O` - Open
- `Ctrl+W` - Close
- `Ctrl+Enter` - Submit/Confirm
- `Esc` - Cancel/Close
- `Del` - Delete
- `F1` - Help

### macOS
- `Cmd+S` - Save
- `Cmd+N` - New
- `Cmd+O` - Open
- `Cmd+W` - Close
- `Cmd+Enter` - Submit/Confirm
- `Esc` - Cancel/Close
- `Del` - Delete

### Cross-Platform
- `Space` - Flip/Toggle
- `Enter` - Confirm
- `Tab` - Next field
- `Shift+Tab` - Previous field
- Arrow keys - Navigate

## Testing Checklist

- [ ] All buttons have tooltips
- [ ] All icon buttons have semantic descriptions
- [ ] All page titles have `HeadingLevel="Level1"`
- [ ] Section headings have `HeadingLevel="Level2"`
- [ ] All text fields have semantic descriptions
- [ ] Complex actions have hints explaining outcomes
- [ ] Keyboard shortcuts are documented in tooltips
- [ ] Tooltips are under 50 characters when possible
- [ ] Semantic descriptions are noun phrases
- [ ] Hints explain what happens, not what the button is
- [ ] Consistent terminology across similar elements
- [ ] Build succeeds with no warnings

## Additional Resources

- [Microsoft MAUI Semantic Properties](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/views/semantic-properties)
- [MAUI Tooltip Documentation](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/tooltips)
- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)
- Project's ACCESSIBILITY_GUIDE.md for complete examples
