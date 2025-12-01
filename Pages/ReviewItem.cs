using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using System.IO;
using System.Collections.Generic;

namespace mindvault.Pages;

public class ReviewItem : INotifyPropertyChanged
{
    int _id;
    int _reviewerId;
    string _question = string.Empty;
    string _answer = string.Empty;
    string _qImg = string.Empty;
    string _aImg = string.Empty;
    bool _isSaved;
    int _number;
    bool _learned;

    ImageSource? _visibleQ;
    ImageSource? _visibleA;
    bool _isVisibleToUser;

    CancellationTokenSource? _qLoadCts;
    CancellationTokenSource? _aLoadCts;

    // Tiny placeholder from app resources; replace if you have a dedicated one
    static readonly ImageSource Placeholder = ImageSource.FromFile("dotnet_bot.png");

    // Replace FromFile() with FromStream() for deferred decode and to avoid caching large bitmaps
    static ImageSource CreateStreamSource(string path)
        => ImageSource.FromStream(() => File.OpenRead(path));

    public int Id { get => _id; set { if (_id == value) return; _id = value; OnPropertyChanged(); } }
    public int ReviewerId { get => _reviewerId; set { if (_reviewerId == value) return; _reviewerId = value; OnPropertyChanged(); } }
    public string Question { get => _question; set { if (_question == value) return; _question = value ?? string.Empty; OnPropertyChanged(); } }
    public string Answer { get => _answer; set { if (_answer == value) return; _answer = value ?? string.Empty; OnPropertyChanged(); } }
    public string QuestionImagePath { get => _qImg; set { if (_qImg == value) return; _qImg = value ?? string.Empty; OnPropertyChanged(); OnPropertyChanged(nameof(QuestionImageVisible)); TriggerLazyLoadQ(); } }
    public string AnswerImagePath { get => _aImg; set { if (_aImg == value) return; _aImg = value ?? string.Empty; OnPropertyChanged(); OnPropertyChanged(nameof(AnswerImageVisible)); TriggerLazyLoadA(); } }

    public bool QuestionImageVisible => !string.IsNullOrWhiteSpace(QuestionImagePath);
    public bool AnswerImageVisible => !string.IsNullOrWhiteSpace(AnswerImagePath);

    public bool IsVisibleToUser
    {
        get => _isVisibleToUser;
        set
        {
            if (_isVisibleToUser == value) return;
            _isVisibleToUser = value;
            OnPropertyChanged();
            TriggerLazyLoadQ();
            TriggerLazyLoadA();
            if (!value)
            {
                // Release images when off-screen
                VisibleQuestionImage = null;
                VisibleAnswerImage = null;
                _qLoadCts?.Cancel();
                _aLoadCts?.Cancel();
            }
        }
    }

    public ImageSource? VisibleQuestionImage
    {
        get => _visibleQ;
        private set { if (ReferenceEquals(_visibleQ, value)) return; _visibleQ = value; OnPropertyChanged(); }
    }

    public ImageSource? VisibleAnswerImage
    {
        get => _visibleA;
        private set { if (ReferenceEquals(_visibleA, value)) return; _visibleA = value; OnPropertyChanged(); }
    }

    void TriggerLazyLoadQ()
    {
        if (!IsVisibleToUser || !QuestionImageVisible) { VisibleQuestionImage = null; return; }
        _qLoadCts?.Cancel();
        _qLoadCts = new();
        var token = _qLoadCts.Token;
        VisibleQuestionImage = Placeholder;
        Application.Current?.Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(10);
            var path = QuestionImagePath;
            if (string.IsNullOrWhiteSpace(path)) { VisibleQuestionImage = null; return; }
            try
            {
                await Task.Run(() =>
                {
                    if (token.IsCancellationRequested) return;
                    var img = CreateStreamSource(path);
                    if (token.IsCancellationRequested) return;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (!token.IsCancellationRequested) VisibleQuestionImage = img;
                    });
                }, token);
            }
            catch { }
        });
    }

    void TriggerLazyLoadA()
    {
        if (!IsVisibleToUser || !AnswerImageVisible) { VisibleAnswerImage = null; return; }
        _aLoadCts?.Cancel();
        _aLoadCts = new();
        var token = _aLoadCts.Token;
        VisibleAnswerImage = Placeholder;
        Application.Current?.Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(10);
            var path = AnswerImagePath;
            if (string.IsNullOrWhiteSpace(path)) { VisibleAnswerImage = null; return; }
            try
            {
                await Task.Run(() =>
                {
                    if (token.IsCancellationRequested) return;
                    var img = CreateStreamSource(path);
                    if (token.IsCancellationRequested) return;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (!token.IsCancellationRequested) VisibleAnswerImage = img;
                    });
                }, token);
            }
            catch { }
        });
    }

    public bool IsSaved { get => _isSaved; set { if (_isSaved == value) return; _isSaved = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEditing)); } }
    public bool IsEditing => !_isSaved;
    public int Number { get => _number; set { if (_number == value) return; _number = value; OnPropertyChanged(); } }
    public bool Learned { get => _learned; set { if (_learned == value) return; _learned = value; OnPropertyChanged(); } }

    // Lightweight notification freeze flag to avoid redundant UI updates during bulk set
    bool _suspendNotifications;

    public void SuspendNotifications() => _suspendNotifications = true;
    public void ResumeNotifications()
    {
        _suspendNotifications = false;
        OnPropertyChanged(string.Empty); // notify all bindings to refresh after bulk change
    }

    public static ReviewItem FromFlashcard(mindvault.Data.Flashcard c)
    {
        // Avoid multiple notifications by initializing fields first
        var item = new ReviewItem
        {
            _id = c.Id,
            _reviewerId = c.ReviewerId,
            _question = c.Question ?? string.Empty,
            _answer = c.Answer ?? string.Empty,
            _qImg = c.QuestionImagePath ?? string.Empty,
            _aImg = c.AnswerImagePath ?? string.Empty,
            _isSaved = true,
            _number = c.Order,
            _learned = c.Learned
        };
        return item;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        if (_suspendNotifications) return;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
