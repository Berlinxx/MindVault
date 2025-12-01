# Accessibility & Tooltips Implementation Guide

## Overview
This guide explains how to add tooltips and accessibility features to all pages in the MindVault application.

## Quick Reference

### Basic Tooltip
```xaml
<Button Text="Save" 
        ToolTipProperties.Text="Save your changes"
        SemanticProperties.Description="Save button"
        SemanticProperties.Hint="Saves all current changes"/>
```

### Button with Keyboard Shortcut
```xaml
<Button Text="Save" 
        ToolTipProperties.Text="Save changes (Ctrl+S)"
        SemanticProperties.Description="Save button, keyboard shortcut Ctrl S"/>
```

### Icon Button
```xaml
<Border ToolTipProperties.Text="Delete item"
        SemanticProperties.Description="Delete button"
        SemanticProperties.Hint="Removes this item permanently">
    <Label Text="&#xF2ED;" FontFamily="FAS"/>
    <Border.GestureRecognizers>
        <TapGestureRecognizer Tapped="OnDeleteTapped"/>
    </Border.GestureRecognizers>
</Border>
```

### Heading for Screen Readers
```xaml
<Label Text="Page Title" 
       SemanticProperties.HeadingLevel="Level1"
       SemanticProperties.Description="Page heading"/>
```

## Complete Tooltip List by Page

### ? ReviewerEditorPage (COMPLETED)
- ? Edit title button: "Edit title (Ctrl+E)"
- ? Keyboard shortcuts button: "Keyboard shortcuts (Ctrl+H)"
- ? Save and exit button: "Save and exit (Ctrl+Enter)"
- ? Delete card button: "Delete card"
- ? Save card button: "Save card (Ctrl+S)"
- ? Add image to question: "Add image to question"
- ? Add image to answer: "Add image to answer"
- ? Add new question button: "Add new question (Ctrl+N)"

### ? ReviewersPage (COMPLETED)
- ? Search button: "Search reviewers"
- ? Sort dropdown: "Change sort order"
- ? Delete icon: "Delete reviewer"
- ? Export icon: "Export reviewer to file"
- ? Edit icon: "Edit questions"
- ? View course button: "Start reviewing this course"
- ? Create reviewer button: "Create a new reviewer deck"
- ? Import button: "Import reviewer from .txt file"

### ? HomePage (COMPLETED)
- ? Mind Vault title: HeadingLevel="Level1"
- ? Create reviewer button: "Create a new reviewer deck"
- ? Browse reviewer button: "View all your reviewer decks"
- ? Multiplayer mode button: "Play with friends over local network"

### ? CourseReviewPage (COMPLETED)
- ? Deck title: HeadingLevel="Level1"
- ? Settings button: "Deck settings"
- ? Close button: "Exit review session"
- ? Progress bar: SemanticProperties.Description
- ? Skip button: "Skip this card"
- ? Speak button: "Read text aloud"
- ? Fail button: "Mark as incorrect"
- ? Flip button: "Flip card (Space)"
- ? Pass button: "Mark as correct"

### ? SetProfilePage (COMPLETED)
- ? Avatar preview: SemanticProperties.Description
- ? Avatar picker: "Swipe to browse and select your profile avatar"
- ? Gender section: HeadingLevel="Level2"
- ? Female gender: "Select female"
- ? Male gender: "Select male"
- ? Other gender: "Select other/prefer not to say"
- ? Username field: "Enter a username between 4 and 15 characters"
- ? Save button: "Save profile settings"

### ? TitleReviewerPage (COMPLETED)
- ? Page title: HeadingLevel="Level1"
- ? Section heading: HeadingLevel="Level2"
- ? Title entry field: "Enter a name for your reviewer deck"
- ? Create button: "Create new reviewer with this title"

### ? AddFlashcardsPage (COMPLETED)
- ? Page title: HeadingLevel="Level1"
- ? Close button: "Close and return to previous page"
- ? AI section: HeadingLevel="Level2"
- ? Summarize button: "Use AI to generate flashcards (Windows only)"
- ? Manual section: HeadingLevel="Level2"
- ? Type flashcards button: "Manually add flashcards with text and images"
- ? Import button: "Import flashcards from .txt file"
- ? Paste section: HeadingLevel="Level2"
- ? Paste editor: Semantic description for screen readers
- ? Create flashcards button: "Create flashcards from pasted text"

### ? SummarizeContentPage (COMPLETED)
- ? Page title: HeadingLevel="Level1"
- ? Close button: "Close and return to previous page"
- ? Content editor: "Type or paste the content"
- ? Upload file button: "Upload document file (.pdf, .docx, .pptx, .txt)"
- ? Install Python button: "Install Python and Llama AI dependencies"
- ? Generate button: "Generate flashcards from content using AI"
- ? Processing dialog: HeadingLevel="Level2"

### ? ReviewMistakesPage (TODO - Not Accessible in Current Build)
```xaml
- Retry button: "Review missed cards again"
- View all button: "See all incorrect answers"
- Continue button: "Continue to next section"
```

### ?? SessionSummaryPage (TODO)
```xaml
- Cards reviewed: SemanticProperties.Description="Number of cards reviewed"
- Accuracy percentage: SemanticProperties.Description="Your accuracy rate"
- Continue button: "Return to home"
```

### ? ReviewerSettingsPage (COMPLETED)
- ? Page title: HeadingLevel="Level1"
- ? Close buttons: "Close settings and return"
- ? Mind Vault Default mode: "Select Mind Vault default algorithm"
- ? Exam Cram mode: "Select Exam Cram Mode"
- ? Questions per round chips: "10/20/30/40 questions per round"
- ? Reset progress button: "Reset all learning progress for this deck"

### ? ProfileSettingsPage (COMPLETED)
- ? Page title: HeadingLevel="Level1"
- ? Avatar preview: SemanticProperties.Description
- ? Avatar picker: "Swipe to browse and select your profile avatar"
- ? Gender section: HeadingLevel="Level2"
- ? Gender icons: Tooltips and semantic descriptions
- ? Username field: "Enter your username"
- ? Save button: "Save profile changes"

### ? MultiplayerPage (COMPLETED)
- ? Page title: HeadingLevel="Level1"
- ? Section heading: HeadingLevel="Level2"
- ? Host button: "Host a multiplayer game session"
- ? Room code field: "Enter the 5-letter room code from the host"
- ? Join button: "Join room with code"

### ? ImportPage (COMPLETED)
- ? Page title: HeadingLevel="Level1"
- ? Section heading: HeadingLevel="Level2"
- ? Flashcard preview: SemanticProperties.Description
- ? Import button: "Import this reviewer deck"

### ? ExportPage (COMPLETED)
- ? Page title: HeadingLevel="Level1"
- ? Section heading: HeadingLevel="Level2"
- ? Flashcard preview: SemanticProperties.Description
- ? Export button: "Export reviewer deck to file"

### ? OnboardingPage (COMPLETED)
- ? Skip button: "Skip onboarding"
- ? Carousel: "Swipe left or right to navigate between tutorial slides"
- ? Next button: "Next slide"
- ? Let's Go button: Partially completed (accessibility added)

## Accessibility Best Practices

### 1. **Semantic Descriptions** (Screen Readers)
Always add `SemanticProperties.Description` to interactive elements:
```xaml
<Button SemanticProperties.Description="Save button"/>
```

### 2. **Semantic Hints** (Additional Context)
Use hints for complex actions:
```xaml
<Button SemanticProperties.Hint="Saves all changes and closes the editor"/>
```

### 3. **Headings** (Navigation)
Mark section titles as headings:
```xaml
<Label Text="Section Title" 
       SemanticProperties.HeadingLevel="Level2"/>
```

### 4. **Tooltips** (Desktop)
Add tooltips for all interactive elements:
```xaml
<Border ToolTipProperties.Text="Helpful description"/>
```

### 5. **Keyboard Shortcuts**
Always document keyboard shortcuts in tooltips:
```xaml
ToolTipProperties.Text="Save changes (Ctrl+S)"
```

### 6. **State Announcements**
For toggle buttons, announce state:
```xaml
SemanticProperties.Description="Sound effects, currently enabled"
```

## Implementation Priority

### Phase 1 (Critical - User Workflows)
1. ? ReviewerEditorPage
2. ? ReviewersPage  
3. ?? CourseReviewPage
4. ?? AddFlashcardsPage
5. ?? HomePage

### Phase 2 (Important - Settings & Management)
6. ?? ReviewerSettingsPage (complete)
7. ?? ProfileSettingsPage
8. ?? SetProfilePage
9. ?? TitleReviewerPage

### Phase 3 (Supporting - Import/Export & Features)
10. ?? SummarizeContentPage
11. ?? ImportPage
12. ?? ExportPage
13. ?? SessionSummaryPage
14. ?? ReviewMistakesPage

### Phase 4 (Optional - Multiplayer)
15. ?? MultiplayerPage
16. ?? HostLobbyPage
17. ?? PlayerLobbyPage
18. ?? HostJudgePage
19. ?? PlayerBuzzPage
20. ?? GameOverPage

## Testing Accessibility

### Windows Narrator
1. Press `Win + Ctrl + Enter` to start Narrator
2. Navigate with `Tab` key
3. Listen for proper descriptions

### Android TalkBack
1. Settings > Accessibility > TalkBack
2. Enable TalkBack
3. Navigate by swiping

### iOS VoiceOver
1. Settings > Accessibility > VoiceOver
2. Enable VoiceOver
3. Navigate by swiping

### Keyboard Navigation
1. Test all Tab stops
2. Verify Enter/Space activate buttons
3. Verify Escape cancels actions

## Common Patterns

### Modal/Popup
```xaml
<Border SemanticProperties.Description="Dialog"
        SemanticProperties.Hint="Press Escape to close">
    <VerticalStackLayout>
        <Label Text="Title" 
               SemanticProperties.HeadingLevel="Level2"/>
        <Button Text="OK" 
                ToolTipProperties.Text="Confirm action"
                SemanticProperties.Description="OK button"/>
    </VerticalStackLayout>
</Border>
```

### Form Field
```xaml
<VerticalStackLayout>
    <Label Text="Username" 
           SemanticProperties.Description="Username label"/>
    <Entry Placeholder="Enter username"
           SemanticProperties.Description="Username text field"
           SemanticProperties.Hint="Type your username here"/>
</VerticalStackLayout>
```

### Icon-Only Button
```xaml
<Border ToolTipProperties.Text="Delete"
        SemanticProperties.Description="Delete button"
        SemanticProperties.Hint="Permanently removes this item">
    <Label Text="&#xF2ED;" FontFamily="FontAwesome"/>
</Border>
```

## Resources
- [Microsoft MAUI Accessibility](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/views/semantic-properties)
- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)
- [Microsoft Inclusive Design](https://www.microsoft.com/design/inclusive/)

## Status
Last Updated: 2024
Total Pages: 25
Completed: 14 (56%)
In Progress: 11 (44%)
