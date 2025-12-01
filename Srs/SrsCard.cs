using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace mindvault.Srs
{
    public enum Stage
    {
        Avail,
        Seen,
        Learned,
        Skilled,
        Memorized
    }

    public class SrsCard
    {
        public int Id { get; }
        public string Question { get; }
        public string Answer { get; }
        public string QuestionImagePath { get; }
        public string AnswerImagePath { get; }

        public Stage Stage { get; set; } = Stage.Avail;
        public DateTime DueAt { get; set; } = DateTime.UtcNow;
        public DateTime CooldownUntil { get; set; } = DateTime.UtcNow;
        public double Ease { get; set; } = 2.5; // SM-2 EF
        public TimeSpan Interval { get; set; } = TimeSpan.Zero;
        public int SeenCount { get; set; } = 0;
        public bool CorrectOnce { get; set; } = false;
        public int ConsecutiveCorrects { get; set; } = 0; // retained for potential UI stats
        public bool CountedSkilled { get; set; } = false;
        public bool CountedMemorized { get; set; } = false;
        [JsonIgnore]
        public Queue<DateTime> AnswerTimes { get; } = new();
        public int Repetitions { get; set; } = 0; // SM-2 successful repetitions

        public bool IsDue => Stage != Stage.Avail && DateTime.UtcNow >= DueAt && DateTime.UtcNow >= CooldownUntil;

        public SrsCard(int id, string q, string a, string qImg, string aImg)
        {
            Id = id;
            Question = q;
            Answer = a;
            QuestionImagePath = qImg;
            AnswerImagePath = aImg;
        }

        public void Reset()
        {
            Stage = Stage.Avail;
            DueAt = DateTime.UtcNow;
            CooldownUntil = DateTime.UtcNow;
            Interval = TimeSpan.Zero;
            Ease = 2.5;
            SeenCount = 0;
            CorrectOnce = false;
            ConsecutiveCorrects = 0;
            CountedSkilled = false;
            CountedMemorized = false;
            Repetitions = 0;
            AnswerTimes.Clear();
        }
    }
}
