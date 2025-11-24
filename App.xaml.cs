using mindvault.Services;
using System.Collections.Generic;

namespace mindvault;

public partial class App : Application
{
    // Temporary storage for generated flashcards across navigation
    public static List<Models.FlashcardItem> GeneratedFlashcards { get; set; } = new();

    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();
    }
}
