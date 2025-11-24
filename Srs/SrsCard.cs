using System;
using System.Collections.Generic;

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
        public DateTime DueAt { get; set; } = DateTime.MinValue;
        public DateTime CooldownUntil { get; set; } = DateTime.MinValue;
        public double Ease { get; set; } = 2.5;
        public TimeSpan Interval { get; set; } = TimeSpan.Zero;
        public int SeenCount { get; set; } = 0;
        public bool CorrectOnce { get; set; } = false;
        public int ConsecutiveCorrects { get; set; } = 0;
        public bool CountedSkilled { get; set; } = false;
        public bool CountedMemorized { get; set; } = false;
        public Queue<DateTime> AnswerTimes { get; } = new();

        public SrsCard(int id, string q, string a, string qImg, string aImg)
        {
            Id = id;
            Question = q;
            Answer = a;
            QuestionImagePath = qImg;
            AnswerImagePath = aImg;
        }
    }
}
