using mindvault.Services;
using mindvault.Data;
using mindvault.Controls;
using Microsoft.Maui.Media;
using CommunityToolkit.Maui.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace mindvault.Pages;

// Participant score model for leaderboard
public class ParticipantScore : INotifyPropertyChanged
{
    private string _id = "";
    private string _name = "";
    private int _score;
    private string _avatar = "avatar1.png";
    private int _rank;

    public string Id { get => _id; set { if (_id == value) return; _id = value; OnPropertyChanged(); } }
    public string Name { get => _name; set { if (_name == value) return; _name = value; OnPropertyChanged(); } }
    public int Score { get => _score; set { if (_score == value) return; _score = value; OnPropertyChanged(); OnPropertyChanged(nameof(PointsText)); } }
    public string Avatar { get => _avatar; set { if (_avatar == value) return; _avatar = value; OnPropertyChanged(); } }
    public int Rank { get => _rank; set { if (_rank == value) return; _rank = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLeader)); } }
    public bool IsLeader => Rank == 1;
    public string PointsText => $"{Score} PTS";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class HostJudgePage : ContentPage
{
    private readonly MultiplayerService _multi = Services.ServiceHelper.GetRequiredService<MultiplayerService>();
    private readonly DatabaseService _db = Services.ServiceHelper.GetRequiredService<DatabaseService>();

    private string _currentQuestion = string.Empty;
    private string _currentAnswer = string.Empty;
    private string _buzzWinnerName = string.Empty;
    private string _buzzWinnerAvatar = "avatar1.png";
    private string _secondBuzzWinnerName = string.Empty;
    private string _secondBuzzWinnerAvatar = "avatar1.png";
    private bool _showAnswer;
    private bool _hasBuzzWinner;
    private bool _hasSecondBuzzWinner;
    private readonly bool _startRematch;

    public string CurrentQuestion { get => _currentQuestion; set { _currentQuestion = value; OnPropertyChanged(); } }
    public string CurrentAnswer { get => _currentAnswer; set { _currentAnswer = value; OnPropertyChanged(); } }
    public string BuzzWinner { get => _buzzWinnerName; set { _buzzWinnerName = value; OnPropertyChanged(); HasBuzzWinner = !string.IsNullOrEmpty(_buzzWinnerName); } }
    public string BuzzAvatar { get => _buzzWinnerAvatar; set { _buzzWinnerAvatar = value; OnPropertyChanged(); } }
    public string SecondBuzzWinner { get => _secondBuzzWinnerName; set { _secondBuzzWinnerName = value; OnPropertyChanged(); HasSecondBuzzWinner = !string.IsNullOrEmpty(_secondBuzzWinnerName); } }
    public string SecondBuzzAvatar { get => _secondBuzzWinnerAvatar; set { _secondBuzzWinnerAvatar = value; OnPropertyChanged(); } }
    public bool HasBuzzWinner { get => _hasBuzzWinner; set { if (_hasBuzzWinner == value) return; _hasBuzzWinner = value; OnPropertyChanged(); } }
    public bool HasSecondBuzzWinner { get => _hasSecondBuzzWinner; set { if (_hasSecondBuzzWinner == value) return; _hasSecondBuzzWinner = value; OnPropertyChanged(); } }

    public ObservableCollection<ParticipantScore> Participants { get; } = new();
    private readonly Dictionary<string, int> _scoreMap = new();
    private readonly Dictionary<string, string> _avatarMap = new();
    private readonly Dictionary<string, string> _nameMap = new();

    private int _reviewerId;
    private List<Flashcard> _cards = new();
    private int _index = -1;

    public HostJudgePage(int reviewerId, string title) : this(reviewerId, title, startRematch: false) { }

    public HostJudgePage(int reviewerId, string title, bool startRematch)
    {
        InitializeComponent();
        BindingContext = this;
        _reviewerId = reviewerId;
        _startRematch = startRematch;

        // Subscribe to multiplayer events
        _multi.HostParticipantJoined += OnHostParticipantJoined;
        _multi.HostParticipantLeft += OnHostParticipantLeft;
        _multi.HostBuzzWinner += OnHostBuzzWinner;
        _multi.HostGameOverOccurred += OnHostGameOver;
        DeckTitle = title;

        _multi.HostSetCurrentDeck(reviewerId, title);
        
        // Initialize leaderboard with current participants
        RefreshLeaderboard();
    }

    private string _deckTitle = string.Empty;
    public string DeckTitle { get => _deckTitle; set { _deckTitle = value; OnPropertyChanged(); } }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Load existing participants into leaderboard
        InitializeLeaderboardFromSnapshot();
        
        await LoadDeckAsync();
        NextCard();
        if (_startRematch)
        {
            // Delay slightly to ensure UI is ready
            await Task.Delay(50);
            try { _multi.HostStartRematch(); } catch { }
        }
    }

    private void InitializeLeaderboardFromSnapshot()
    {
        var snapshot = _multi.GetHostParticipantsSnapshot();
        foreach (var p in snapshot)
        {
            if (!string.IsNullOrEmpty(p.Id))
            {
                _nameMap[p.Id] = p.Name ?? string.Empty;
                _avatarMap[p.Id] = string.IsNullOrEmpty(p.Avatar) ? "avatar1.png" : p.Avatar;
                if (!_scoreMap.ContainsKey(p.Id))
                    _scoreMap[p.Id] = 0;
            }
        }
        RefreshLeaderboard();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _multi.HostParticipantJoined -= OnHostParticipantJoined;
        _multi.HostParticipantLeft -= OnHostParticipantLeft;
        _multi.HostBuzzWinner -= OnHostBuzzWinner;
        _multi.HostGameOverOccurred -= OnHostGameOver;
    }

    private async Task LoadDeckAsync()
    {
        try
        {
            _cards = await _db.GetFlashcardsAsync(_reviewerId);
            _cards = _cards.OrderBy(c => c.Order).ToList();
        }
        catch
        {
            _cards = new();
        }
    }

    private void NextCard()
    {
        _index++;
        _multi.OpenBuzzForAll();
        BuzzWinner = string.Empty; // hides avatar/name
        SecondBuzzWinner = string.Empty; // hides second avatar/name
        _showAnswer = false;
        if (_index >= 0 && _index < _cards.Count)
        {
            CurrentQuestion = _cards[_index].Question;
            CurrentAnswer = string.Empty;
        }
        else
        {
            CurrentQuestion = "No more cards.";
            CurrentAnswer = string.Empty;
            _multi.HostGameOver(_deckTitle);
            return;
        }
        _multi.UpdateQuestionState(Math.Min(_index + 1, Math.Max(_cards.Count, 1)), _cards.Count);
    }

    private void OnHostBuzzWinner(MultiplayerService.ParticipantInfo p)
    {
        MainThread.BeginInvokeOnMainThread(async () => 
        { 
            // If first winner is empty, this is the first buzz
            if (string.IsNullOrEmpty(BuzzWinner))
            {
                BuzzWinner = p.Name; 
                BuzzAvatar = string.IsNullOrEmpty(p.Avatar) ? "avatar1.png" : p.Avatar; 
                _lastWinnerId = p.Id;
                
                // Animate buzz banner sliding down from top
                var banner = this.FindByName<Border>("HostBuzzBanner");
                if (banner != null)
                {
                    await Task.Delay(50);
                    banner.TranslationY = -100;
                    banner.Opacity = 0;
                    await Task.WhenAll(
                        banner.TranslateTo(0, 0, 400, Easing.CubicOut),
                        banner.FadeTo(1, 300)
                    );
                }
                
                // Animate first judge bar sliding in from left
                await Task.Delay(50); // Small delay to ensure visibility is set
                JudgeBar.TranslationX = -400; // Start off-screen to the left
                JudgeBar.Opacity = 0;
                await Task.WhenAll(
                    JudgeBar.TranslateTo(0, 0, 400, Easing.CubicOut),
                    JudgeBar.FadeTo(1, 300)
                );
            }
            // If first winner exists but second is empty, this is the second buzz
            else if (string.IsNullOrEmpty(SecondBuzzWinner))
            {
                SecondBuzzWinner = p.Name;
                SecondBuzzAvatar = string.IsNullOrEmpty(p.Avatar) ? "avatar1.png" : p.Avatar;
                _lastSecondWinnerId = p.Id;
                
                // Animate second judge bar sliding in from left
                await Task.Delay(50);
                JudgeBar2.TranslationX = -400;
                JudgeBar2.Opacity = 0;
                await Task.WhenAll(
                    JudgeBar2.TranslateTo(0, 0, 400, Easing.CubicOut),
                    JudgeBar2.FadeTo(1, 300)
                );
            }
        });
    }

    private void OnHostParticipantJoined(MultiplayerService.ParticipantInfo p)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!string.IsNullOrEmpty(p.Id))
            {
                _nameMap[p.Id] = p.Name ?? string.Empty;
                _avatarMap[p.Id] = string.IsNullOrEmpty(p.Avatar) ? "avatar1.png" : p.Avatar;
                if (!_scoreMap.ContainsKey(p.Id))
                    _scoreMap[p.Id] = 0;
                RefreshLeaderboard();
            }
        });
    }

    private void OnHostParticipantLeft(string id)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var name = _nameMap.TryGetValue(id, out var nm) ? nm : "A player";
            
            _scoreMap.Remove(id);
            _nameMap.Remove(id);
            _avatarMap.Remove(id);
            RefreshLeaderboard();
            
            // Show notification to host
            try { await this.ShowPopupAsync(new AppModal("Player Left", $"{name} has left the game.", "OK")); } catch { }
        });
    }

    private void RefreshLeaderboard()
    {
        var sorted = _scoreMap.OrderByDescending(kv => kv.Value).ToList();
        Participants.Clear();
        int rank = 1;
        foreach (var kv in sorted)
        {
            var name = _nameMap.TryGetValue(kv.Key, out var n) ? n : kv.Key;
            var avatar = _avatarMap.TryGetValue(kv.Key, out var av) ? av : "avatar1.png";
            Participants.Add(new ParticipantScore
            {
                Id = kv.Key,
                Name = name,
                Score = kv.Value,
                Avatar = avatar,
                Rank = rank
            });
            rank++;
        }
    }

    private void OnHostGameOver(MultiplayerService.GameOverPayload payload)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Navigation.PushAsync(new GameOverPage(payload));
        });
    }

    private string _lastWinnerId = string.Empty;
    private string _lastSecondWinnerId = string.Empty;

    private void OnFlip(object? s, TappedEventArgs e)
    {
        _showAnswer = !_showAnswer;
        if (_showAnswer)
        {
            if (_index >= 0 && _index < _cards.Count)
                CurrentAnswer = _cards[_index].Answer;
        }
        else
        {
            CurrentAnswer = string.Empty;
        }
    }

    private void OnSkip(object? s, TappedEventArgs e)
    {
        if (_index >= 0 && _index < _cards.Count)
        {
            var cur = _cards[_index];
            _cards.RemoveAt(_index);
            _cards.Add(cur);
            _index--;
        }
        NextCard();
    }

    private void OnAccept(object? s, TappedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastWinnerId))
        {
            _multi.HostStopTimerFor(_lastWinnerId);
            _multi.HostAwardPoint(_lastWinnerId, +1);
            
            // Update local score and refresh leaderboard
            if (_scoreMap.ContainsKey(_lastWinnerId))
            {
                _scoreMap[_lastWinnerId]++;
                RefreshLeaderboard();
            }
            
            if (_index >= 0 && _index < _cards.Count)
            {
                var answer = _cards[_index].Answer ?? string.Empty;
                _multi.HostAnnounceCorrectAnswer(answer);
            }
        }
        NextCard();
    }

    private async void OnReject(object? s, TappedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastWinnerId))
            _multi.HostStopTimerFor(_lastWinnerId);
        await _multi.ReopenBuzzExceptWinnerAsync();
        BuzzWinner = string.Empty; // let others buzz
    }

    private void OnAccept2(object? s, TappedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastSecondWinnerId))
        {
            _multi.HostStopTimerFor(_lastSecondWinnerId);
            _multi.HostAwardPoint(_lastSecondWinnerId, +1);
            
            // Update local score and refresh leaderboard
            if (_scoreMap.ContainsKey(_lastSecondWinnerId))
            {
                _scoreMap[_lastSecondWinnerId]++;
                RefreshLeaderboard();
            }
            
            if (_index >= 0 && _index < _cards.Count)
            {
                var answer = _cards[_index].Answer ?? string.Empty;
                _multi.HostAnnounceCorrectAnswer(answer);
            }
        }
        NextCard();
    }

    private async void OnReject2(object? s, TappedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastSecondWinnerId))
            _multi.HostStopTimerFor(_lastSecondWinnerId);
        await _multi.ReopenBuzzExceptWinnerAsync();
        SecondBuzzWinner = string.Empty;
    }

    private async void OnSpeakTapped(object? s, TappedEventArgs e)
    {
        var text = string.IsNullOrWhiteSpace(CurrentQuestion) ? "" : CurrentQuestion;
        if (!string.IsNullOrEmpty(text))
        {
            try { await TextToSpeech.Default.SpeakAsync(text); } catch { }
        }
    }

    private async void OnExitTapped(object? s, TappedEventArgs e)
    {
        // Stop hosting which will notify all clients
        _multi.StopHosting();
        
        // Navigate to home
        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("//HomePage");
        else
            await Navigation.PopToRootAsync();
    }
}
