using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using mindvault.Services;
using mindvault.Utils;
using mindvault.Utils.Messages;
using mindvault.Srs;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace mindvault.Pages
{
    public partial class CourseReviewPage : ContentPage, INotifyPropertyChanged
    {
        private readonly DatabaseService _db = ServiceHelper.GetRequiredService<DatabaseService>();
        private readonly SrsEngine _engine = new();
        private SrsCard? _current;
        private bool _front = true;
        private bool _loaded;
        // === Batching / Round Logic ===
        int _roundCount = 0; // graded answers in current batch
        int _roundSize = 10;
        int _batchCorrect = 0; // correct answers in current batch
        int _batchWrong = 0;   // wrong answers in current batch
        readonly List<SrsCard> _batchMistakeCards = new();
        DateTime _lastSaveTime = DateTime.UtcNow;
        const string PrefRoundSize = "RoundSize";
        const string PrefStudyMode = "StudyMode"; // match ReviewerSettingsPage

        public new string Title { get; }
        public int ReviewerId { get; private set; }

        // Session stats
        private DateTime _sessionStart;
        public string ElapsedText => $"Time: {(int)(DateTime.UtcNow - _sessionStart).TotalMinutes} min";
        public bool AnswerButtonsEnabled => !_front;
        public bool SessionComplete { get; private set; }

        // === Message wiring for live resets ===
        private void WireMessages()
        {
            WeakReferenceMessenger.Default.Register<ProgressResetMessage>(this, (r, m) =>
            {
                if (m.Value != ReviewerId) return;
                _ = MainThread.InvokeOnMainThreadAsync(async () => await ResetSessionAsync());
            });
            WeakReferenceMessenger.Default.Register<StudyModeChangedMessage>(this, (r, m) =>
            {
                var (id, mode) = m.Value;
                if (id != ReviewerId) return;
                _ = MainThread.InvokeOnMainThreadAsync(async () => await ReloadForModeAsync(mode));
            });
            // Update round size live without resetting learned stats
            WeakReferenceMessenger.Default.Register<RoundSizeChangedMessage>(this, (r, m) =>
            {
                var (id, newSize) = m.Value;
                if (id != ReviewerId) return;
                _roundSize = newSize;
                UpdateProgressBar();
                OnPropertyChanged(nameof(ProgressWidth));
            });
            WeakReferenceMessenger.Default.Register<SettingsClosedMessage>(this, (r, m) =>
            {
                if (m.Value != ReviewerId) return;
                // Just re-fetch current round size from preferences and update progress bar; do NOT reload engine
                _roundSize = Preferences.Get($"RoundSize_{ReviewerId}", Preferences.Get("RoundSize", 10));
                UpdateProgressBar();
                OnPropertyChanged(nameof(ProgressWidth));
            });
        }

        public string FaceTag => _front ? "[Front]" : "[Back]";
        public string FaceText => _current == null ? string.Empty : (_front ? _current.Question : _current.Answer);
        public string? FaceImage => _current == null ? null : (_front ? _current.QuestionImagePath : _current.AnswerImagePath);
        public bool FaceImageVisible => !string.IsNullOrWhiteSpace(FaceImage);

        public CourseReviewPage(int reviewerId, string title)
        {
            InitializeComponent();
            ReviewerId = reviewerId;
            Title = title;
            _engine.StateChanged += UpdateBindingsAll;
            BindingContext = this;
            PageHelpers.SetupHamburgerMenu(this);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await mindvault.Utils.AnimHelpers.SlideFadeInAsync(Content);
            WeakReferenceMessenger.Default.UnregisterAll(this);
            WireMessages();
            _roundSize = Preferences.Get($"{PrefRoundSize}_{ReviewerId}", Preferences.Get(PrefRoundSize, 10));
            if (_loaded)
            {
                _roundCount = 0; _batchCorrect = 0; _batchWrong = 0; _batchMistakeCards.Clear();
                UpdateProgressBar();
                return;
            }
            _sessionStart = DateTime.UtcNow;
            _loaded = true;
            await LoadEngineAsync();
            _roundCount = 0; _batchCorrect = 0; _batchWrong = 0; _batchMistakeCards.Clear();
            UpdateProgressBar();
        }

        private async Task LoadEngineAsync(string? forcedMode = null)
        {
            // Determine current mode (deck-specific overrides global)
            var mode = forcedMode;
            if (string.IsNullOrWhiteSpace(mode))
            {
                mode = Preferences.Get($"{PrefStudyMode}_{ReviewerId}", Preferences.Get(PrefStudyMode, "Default"));
            }

            Func<int, Task<IEnumerable<SrsCard>>> fetchFunc = async (id) =>
            {
                var cards = await _db.GetFlashcardsAsync(id);
                return cards.Select(c => new SrsCard(c.Id, c.Question, c.Answer, c.QuestionImagePath, c.AnswerImagePath));
            };

            if (string.Equals(mode, "Exam", StringComparison.OrdinalIgnoreCase))
            {
                // Cram / Exam mode loading
                await _engine.LoadCardsForCramModeAsync(ReviewerId, fetchFunc, CramModeOptions.Fast);
            }
            else
            {
                await _engine.LoadCardsAsync(ReviewerId, fetchFunc);
            }
            _engine.PickNextCard();
            UpdateBindingsAll();
        }

        private async Task ReloadForModeAsync(string? mode = null)
        {
            _roundCount = 0; _batchCorrect = 0; _batchWrong = 0; _batchMistakeCards.Clear();
            UpdateProgressBar();
            OnPropertyChanged(nameof(ProgressWidth));
            _sessionStart = DateTime.UtcNow;
            SessionComplete = false;
            _front = true;
            await LoadEngineAsync(mode);
        }

        private async Task ResetSessionAsync()
        {
            _sessionStart = DateTime.UtcNow;
            SessionComplete = false;
            _front = true;
            _roundCount = 0; _batchCorrect = 0; _batchWrong = 0; _batchMistakeCards.Clear();
            await LoadEngineAsync();
            UpdateProgressBar();
        }

        private async void OnFlip(object? s, TappedEventArgs e)
        {
            await FlipAnimationAsync();
            _front = !_front;
            UpdateBindingsAll();
        }

        private async Task FlipAnimationAsync()
        {
            try
            {
                await CardBorder.ScaleXTo(0.0, 110, Easing.CubicIn);
                await CardBorder.ScaleXTo(1.0, 110, Easing.CubicOut);
            }
            catch { }
        }

        private async void OnPass(object? s, TappedEventArgs e)
        {
            await _engine.GradeCardAsync(true);
            _roundCount++; _batchCorrect++;
            bool roundFinished = _roundSize > 0 && _roundCount >= _roundSize;
            if (roundFinished)
            {
                try { await ShowCompletionAsync(); } catch { }
                _roundCount = 0; // prepare for next batch after summary
                _batchCorrect = 0; _batchWrong = 0; _batchMistakeCards.Clear();
            }
            _engine.PickNextCard();
            _front = true;
            UpdateBindingsAll();
            UpdateProgressBar();
            if (CorrectCount % 5 == 0 || (DateTime.UtcNow - _lastSaveTime).TotalSeconds > 15)
            {
                _engine.SaveProgress();
                _lastSaveTime = DateTime.UtcNow;
            }
        }

        private async void OnFail(object? s, TappedEventArgs e)
        {
            await _engine.GradeCardAsync(false);
            _roundCount++; _batchWrong++;
            if (_engine.CurrentCard is SrsCard wrongCard && !_batchMistakeCards.Contains(wrongCard)) _batchMistakeCards.Add(wrongCard);
            bool roundFinished = _roundSize > 0 && _roundCount >= _roundSize;
            if (roundFinished)
            {
                try { await ShowCompletionAsync(); } catch { }
                _roundCount = 0; _batchCorrect = 0; _batchWrong = 0; _batchMistakeCards.Clear();
            }
            _engine.PickNextCard();
            _front = true;
            UpdateBindingsAll();
            UpdateProgressBar();
            if (WrongCount % 5 == 0 || (DateTime.UtcNow - _lastSaveTime).TotalSeconds > 15)
            {
                _engine.SaveProgress();
                _lastSaveTime = DateTime.UtcNow;
            }
        }

        private async void OnSkip(object? s, TappedEventArgs e)
        {
            await _engine.SkipCardAsync();
            _front = true;
            UpdateBindingsAll();
        }

        private void OnBucketTapped(object? s, TappedEventArgs e)
        {
            // Placeholder: bucket selection not implemented in new engine version
            // Could map bucket to filtering logic; currently just refresh
            _engine.PickNextCard();
        }

        private double _progressWidth;
        public double ProgressWidth
        {
            get => _progressWidth;
            private set
            {
                if (Math.Abs(_progressWidth - value) > 0.1)
                {
                    _progressWidth = value;
                    OnPropertyChanged(nameof(ProgressWidth));
                }
            }
        }

        private void UpdateBindingsAll()
        {
            var previousId = _current?.Id;
            _current = _engine.CurrentCard;
            SessionComplete = _engine.SessionComplete;
            OnPropertyChanged(nameof(SessionComplete));
            OnPropertyChanged(nameof(FaceText));
            OnPropertyChanged(nameof(FaceTag));
            OnPropertyChanged(nameof(FaceImage));
            OnPropertyChanged(nameof(FaceImageVisible));
            OnPropertyChanged(nameof(AnswerButtonsEnabled));
            OnPropertyChanged(nameof(ElapsedText));
            OnPropertyChanged(nameof(Avail));
            OnPropertyChanged(nameof(Seen));
            OnPropertyChanged(nameof(Learned));
            OnPropertyChanged(nameof(Skilled));
            OnPropertyChanged(nameof(Memorized));
            OnPropertyChanged(nameof(CorrectCount));
            OnPropertyChanged(nameof(WrongCount));
        }

        private void UpdateProgressBar()
        {
            double ratio = _roundSize == 0 ? 0 : Math.Clamp((double)_roundCount / _roundSize, 0, 1);
            ProgressWidth = Math.Max(0, (Width - 32) * ratio);
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            // Recalculate progress width when page size changes
            UpdateProgressBar();
        }

        public int Avail => _engine.Total;
        public int Seen => _engine.Seen;
        public int Learned => _engine.Learned;
        public int Skilled => _engine.Skilled;
        public int Memorized => _engine.Memorized;
        public int CorrectCount => _engine.CorrectCount;
        public int WrongCount => _engine.WrongCount;


        // === TTS ===
        private CancellationTokenSource? _ttsCts;
        private bool _isSpeaking;

        private async void OnSpeakTapped(object? s, TappedEventArgs e)
        {
            try
            {
                if (_isSpeaking)
                {
                    _ttsCts?.Cancel();
                    return;
                }

                var text = FaceText;
                if (string.IsNullOrWhiteSpace(text)) return;

                _ttsCts = new CancellationTokenSource();
                _isSpeaking = true;
                if(SpeakButton is not null) SpeakButton.Opacity = 0.5;
                await TextToSpeech.Default.SpeakAsync(text, new SpeechOptions { Volume = 1.0f }, _ttsCts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Debug.WriteLine($"[TTS] {ex.Message}"); }
            finally
            {
                _isSpeaking = false;
                _ttsCts?.Dispose();
                _ttsCts = null;
                if(SpeakButton is not null) SpeakButton.Opacity = 1.0;
            }
        }

        private async void OnCloseTapped(object? s, EventArgs e)
        {
            await PageHelpers.SafeNavigateAsync(this, async () => await NavigationService.CloseCourseToReviewers(),
                "Could not return to reviewers");
        }

        private async void OnSettingsTapped(object? s, EventArgs e)
        {
            var page = new ReviewerSettingsPage { ReviewerId = ReviewerId, ReviewerTitle = Title };
            await Navigator.PushAsync(page, Navigation);
        }

        private async void OnImageTapped(object? s, TappedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(FaceImage)) return;
                FullImage.Source = FaceImage;
                ImageOverlay.IsVisible = true;
                await ImageOverlay.FadeTo(1, 160, Easing.CubicOut);
            }
            catch { }
        }

        private async void OnCloseImageOverlay(object? s, TappedEventArgs e)
        {
            try
            {
                await ImageOverlay.FadeTo(0, 120, Easing.CubicIn);
                ImageOverlay.IsVisible = false;
                FullImage.Source = null;
            }
            catch { }
        }

        private async void OnReviewMistakes(object? s, EventArgs e)
        {
            // Immediate session restart instead of waiting for page reload
            await ResetSessionAsync();
            try { await CompletionOverlay.FadeTo(0, 120); } catch { }
        }

        private async void OnStudyMore(object? s, EventArgs e)
        {
            // Also restart loop immediately
            await ResetSessionAsync();
            try { await CompletionOverlay.FadeTo(0, 120); } catch { }
        }

        private async void OnAddCards(object? s, EventArgs e)
        {
            await Navigator.PushAsync(new ReviewerEditorPage { ReviewerId = ReviewerId, ReviewerTitle = Title }, Navigation);
        }

        private async void OnBackToList(object? s, EventArgs e)
        {
            await PageHelpers.SafeNavigateAsync(this, async () => await NavigationService.CloseCourseToReviewers(),
                "Could not return to reviewers");
        }

        private async Task ShowCompletionAsync()
        {
            if (CompletionOverlay is not null)
            {
                try { await CompletionOverlay.FadeTo(1, 160, Easing.CubicOut); } catch { }
            }
            try
            {
                var duration = DateTime.UtcNow - _sessionStart;
                var mistakes = _batchMistakeCards.Select(c => (c.Question, c.Answer)).ToList();
                var summary = new SessionSummaryPage(ReviewerId, Title, _batchCorrect, _batchWrong, duration, _engine, mistakes);
                await Navigator.PushAsync(summary, Navigation);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CourseReview] Summary navigation failed: {ex.Message}");
            }
            if (CompletionOverlay is not null)
            {
                try { await CompletionOverlay.FadeTo(0, 120, Easing.CubicIn); CompletionOverlay.IsVisible = false; } catch { }
            }
        }

        #region INotifyPropertyChanged
        public new event PropertyChangedEventHandler? PropertyChanged;
        protected new void OnPropertyChanged([CallerMemberName] string? name = null)
            => MainThread.BeginInvokeOnMainThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)));
        #endregion

        protected override void OnDisappearing()
        {
            // Persist latest progress when user navigates away / closes session
            _engine.SaveProgress();
            base.OnDisappearing();
        }
    }
}
