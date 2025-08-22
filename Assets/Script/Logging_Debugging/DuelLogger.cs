// DuelLogger.cs
// Structured duel logs with turn/phase markers, sequence numbers, and optional console mirroring.

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using YGO.Duel.Rules; // for RuleSet.Phase

namespace YGO.Duel.Foundation
{
    /// <summary>Basic event DTO you can construct anywhere and pass to the logger.</summary>
    [Serializable]
    public sealed class DuelEvent
    {
        public string Type;    // e.g., "Turn.Start", "Phase.Change", "Attack.Declared"
        public string Summary; // human-friendly line
        public string Source;  // optional: system/card/seat marker
        public string Actor;   // optional: "P1"/"P2"
        public string Data;    // optional: JSON or key=value pairs

        public DuelEvent() { }
        public DuelEvent(string type, string summary, string source = null, string actor = null, string data = null)
        {
            Type = type; Summary = summary; Source = source; Actor = actor; Data = data;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Type ?? "Event");
            if (!string.IsNullOrEmpty(Actor))  sb.Append($" actor={Actor}");
            if (!string.IsNullOrEmpty(Source)) sb.Append($" src={Source}");
            if (!string.IsNullOrEmpty(Summary)) sb.Append($" :: {Summary}");
            if (!string.IsNullOrEmpty(Data))   sb.Append($" | {Data}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Central logger. Keeps structured entries and optionally echoes to Unity console.
    /// Turn/Phase can be updated via MarkTurnPhase to tag subsequent entries.
    /// </summary>
    public sealed class DuelLogger
    {
        /// <summary>One concrete log entry captured by the logger.</summary>
        [Serializable]
        public sealed class LogEntry
        {
            public long Seq;            // monotonic sequence
            public DateTime Utc;        // wallclock
            public int Frame;           // Unity frame at log time
            public int Turn;            // current turn (0 if unknown)
            public RuleSet.Phase Phase; // current phase
            public string Type;         // event.Type
            public string Summary;      // event.Summary
            public string Source;       // event.Source
            public string Actor;        // event.Actor
            public string Data;         // event.Data

            public override string ToString()
            {
                return $"[{Seq}@{Utc:HH:mm:ss.fff} F{Frame}] T{Turn}:{Phase} {Type} — {Summary}" +
                       $"{(string.IsNullOrEmpty(Actor) ? "" : $" [actor={Actor}]")}" +
                       $"{(string.IsNullOrEmpty(Source) ? "" : $" [src={Source}]")}" +
                       $"{(string.IsNullOrEmpty(Data) ? "" : $" | {Data}")}";
            }
        }

        // Entries buffer
        private readonly List<LogEntry> _entries = new List<LogEntry>(1024);
        private long _seq = 0;

        // Turn/phase markers applied to future entries
        private int _turnNumber = 0;
        private RuleSet.Phase _phase = RuleSet.Phase.Draw;

        public bool EchoToUnityConsole = true;
        public int MaxEntries = 5000; // 0 = unlimited

        /// <summary>Raised when a new entry is logged.</summary>
        public event Action<LogEntry> OnLogged;

        /// <summary>Get a snapshot copy of the logs for UI.</summary>
        public IReadOnlyList<LogEntry> Entries => _entries.AsReadOnly();

        /// <summary>Update current turn/phase; future entries will include these markers.</summary>
        public void MarkTurnPhase(int turn, RuleSet.Phase phase)
        {
            _turnNumber = Math.Max(0, turn);
            _phase = phase;
        }

        /// <summary>Log a structured event.</summary>
        public void LogEvent(DuelEvent e)
        {
            if (e == null) return;

            var entry = new LogEntry
            {
                Seq     = ++_seq,
                Utc     = DateTime.UtcNow,
                Frame   = Time.frameCount,
                Turn    = _turnNumber,
                Phase   = _phase,
                Type    = e.Type ?? "Event",
                Summary = e.Summary ?? "",
                Source  = e.Source ?? "",
                Actor   = e.Actor ?? "",
                Data    = e.Data ?? ""
            };

            _entries.Add(entry);
            if (MaxEntries > 0 && _entries.Count > MaxEntries)
                _entries.RemoveRange(0, _entries.Count - MaxEntries);

            if (EchoToUnityConsole)
                Debug.Log(entry.ToString());

            OnLogged?.Invoke(entry);
        }

        /// <summary>Convenience wrapper for quick messages.</summary>
        public void LogText(string type, string summary, string data = null, string source = null, string actor = null)
            => LogEvent(new DuelEvent(type, summary, source, actor, data));

        /// <summary>Dump all entries into a single string (for export/bug reports).</summary>
        public string DumpAsText()
        {
            var sb = new StringBuilder(_entries.Count * 96);
            foreach (var e in _entries) sb.AppendLine(e.ToString());
            return sb.ToString();
        }

        /// <summary>Reset logger state (does not change turn/phase markers).</summary>
        public void Clear()
        {
            _entries.Clear();
            _seq = 0;
        }
    }
}
