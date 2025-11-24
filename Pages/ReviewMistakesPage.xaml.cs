using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using mindvault.Utils;

namespace mindvault.Pages;

public partial class ReviewMistakesPage : ContentPage
{
    public ObservableCollection<MistakeItem> Items { get; } = new();

    public ReviewMistakesPage(IReadOnlyList<(string Q,string A)> mistakes)
    {
        InitializeComponent();
        foreach (var m in mistakes)
            Items.Add(new MistakeItem { Q = m.Q, A = m.A });
        BindingContext = this;
    }

    async void OnContinue(object? sender, System.EventArgs e)
    {
        await Navigation.PopAsync();
    }

    public class MistakeItem
    {
        public string Q { get; set; } = string.Empty;
        public string A { get; set; } = string.Empty;
    }
}
