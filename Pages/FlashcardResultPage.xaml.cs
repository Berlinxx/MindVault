namespace mindvault.Pages;

// Revert to code-only page due to persistent XAML parse errors.
public class FlashcardResultPage : ContentPage
{
    readonly CollectionView _cards;
    public FlashcardResultPage()
    {
        Title = "Generated Flashcards";
        _cards = new CollectionView { Margin = 10 };
        _cards.ItemTemplate = new DataTemplate(() =>
        {
            var frame = new Frame { Margin = new Thickness(0,5), Padding = 15, CornerRadius = 10, BackgroundColor = Colors.White, BorderColor = Colors.LightGray };
            var stack = new VerticalStackLayout { Spacing = 8 };
            var qLabel = new Label { FontAttributes = FontAttributes.Bold, FontSize = 16, TextColor = Colors.Black };
            qLabel.SetBinding(Label.TextProperty, "Question");
            var sep = new BoxView { HeightRequest = 1, Color = Color.FromArgb("#E0E0E0") };
            var aLabel = new Label { FontAttributes = FontAttributes.Italic, FontSize = 14, TextColor = Color.FromArgb("#512BD4") };
            aLabel.SetBinding(Label.TextProperty, "Answer");
            stack.Add(qLabel); stack.Add(sep); stack.Add(aLabel);
            frame.Content = stack;
            return frame;
        });
        var doneBtn = new Button { Text = "Done / Save", Margin = 20, BackgroundColor = Color.FromArgb("#512BD4"), TextColor = Colors.White };
        doneBtn.Clicked += OnDoneClicked;
        Content = new Grid
        {
            RowDefinitions = new RowDefinitionCollection { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) }
        };
        ((Grid)Content).Add(_cards);
        ((Grid)Content).Add(doneBtn);
        Grid.SetRow(_cards,0);
        Grid.SetRow(doneBtn,1);
        BackgroundColor = Color.FromArgb("#F0F0F0");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (App.GeneratedFlashcards is not null)
            _cards.ItemsSource = App.GeneratedFlashcards;
    }

    private async void OnDoneClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("///ReviewerEditorPage");
    }
}
