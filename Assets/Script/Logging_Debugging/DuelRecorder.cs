// DuelRecorder.cs
// Records duel events (from DuelLogger) and user/game inputs you choose to track.
// Stores an ordered timeline; can be saved/loaded or fed to DuelReplay.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YGO.Duel.Rules;

namespace YGO.Duel.Foundation
{
    public enum RecordKind { Event, Input }

    [Serializable]
    public sealed class RecordedEntry
    {
        public long Seq;               // recorder sequence (not logger seq)
        public DateTime Utc;
        public int Frame;

        public RecordKind Kind;        // Event or Input

        // Context markers (snapshotted from logger when present)
        public int Turn;
        public RuleSet.Phase Phase;

        // Common fields
        public string Type;            // e.g. "Phase.Change" or "Input.Attack"
        public string Summary;         // human-friendly description
        public string PayloadJson;     // inputs: your serialized payload; events: optional detail

        public override string ToString()
            => $"[{Seq}@{Utc:HH:mm:ss.fff} F{Frame}] T{Turn}:{Phase} {Kind}:{Type} — {Summary}" +
               (string.IsNullOrEmpty(PayloadJson) ? "" : $" | {PayloadJson}");
    }

    /// <summary>
    /// Recorder: attach to DuelLogger to capture events; call RecordInput for inputs.
    /// </summary>
    public sealed class DuelRecorder
    {
        private readonly List<RecordedEntry> _timeline = new List<RecordedEntry>(1024);
        private long _seq = 0;

        private DuelLogger _logger;

        /// <summary>Raised whenever a new RecordedEntry is added.</summary>
        public event Action<RecordedEntry> OnRecorded;

        public IReadOnlyList<RecordedEntry> Timeline => _timeline.AsReadOnly();

        /// <summary>Start listening to a logger (idempotent).</summary>
        public void AttachLogger(DuelLogger logger)
        {
            if (_logger == logger) return;
            if (_logger != null) _logger.OnLogged -= HandleLog;
            _logger = logger;
            if (_logger != null) _logger.OnLogged += HandleLog;
        }

        public void DetachLogger()
        {
            if (_logger != null) _logger.OnLogged -= HandleLog;
            _logger = null;
        }

        private void HandleLog(DuelLogger.LogEntry e)
        {
            var rec = new RecordedEntry
            {
                Seq   = ++_seq,
                Utc   = e.Utc,
                Frame = e.Frame,
                Kind  = RecordKind.Event,
                Turn  = e.Turn,
                Phase = e.Phase,
                Type  = e.Type,
                Summary = e.Summary,
                // For events, we can keep extra info in PayloadJson as a compact string
                PayloadJson = BuildEventPayloadJson(e)
            };
            _timeline.Add(rec);
            OnRecorded?.Invoke(rec);
        }

        /// <summary>
        /// Record a user/gameplay input. You decide the type string and provide any JSON payload.
        /// Keep it small and deterministic (ids, indices, not object references).
        /// </summary>
        public void RecordInput(string type, string summary, string payloadJson, int turnMarker, RuleSet.Phase phaseMarker)
        {
            var rec = new RecordedEntry
            {
                Seq   = ++_seq,
                Utc   = DateTime.UtcNow,
                Frame = Time.frameCount,
                Kind  = RecordKind.Input,
                Turn  = turnMarker,
                Phase = phaseMarker,
                Type  = type,
                Summary = summary,
                PayloadJson = payloadJson ?? ""
            };
            _timeline.Add(rec);
            OnRecorded?.Invoke(rec);
        }

        /// <summary>Clear timeline and sequence.</summary>
        public void Clear()
        {
            _timeline.Clear();
            _seq = 0;
        }

        // ---------------- Optional save/load (simple line-based JSON-ish for debugging) ----------------

        /// <summary>Write a simple text dump (one line per entry). Useful for bug repro attachments.</summary>
        public void SaveText(string path)
        {
            using (var w = new StreamWriter(path))
            {
                foreach (var e in _timeline)
                    w.WriteLine(e.ToString());
            }
        }

        /// <summary>
        /// Export a minimal JSON array (manually constructed to avoid external dependencies).
        /// </summary>
        public string ExportJson()
        {
            var sb = new System.Text.StringBuilder(_timeline.Count * 80);
            sb.Append('[');
            for (int i = 0; i < _timeline.Count; i++)
            {
                var e = _timeline[i];
                sb.Append('{');
                AppendJsonPair(sb, "seq", e.Seq.ToString());
                sb.Append(',');
                AppendJsonPair(sb, "utc", e.Utc.ToString("o"));
                sb.Append(',');
                AppendJsonPair(sb, "frame", e.Frame.ToString());
                sb.Append(',');
                AppendJsonPair(sb, "kind", e.Kind.ToString());
                sb.Append(',');
                AppendJsonPair(sb, "turn", e.Turn.ToString());
                sb.Append(',');
                AppendJsonPair(sb, "phase", e.Phase.ToString());
                sb.Append(',');
                AppendJsonPair(sb, "type", e.Type ?? "");
                sb.Append(',');
                AppendJsonPair(sb, "summary", e.Summary ?? "");
                sb.Append(',');
                AppendJsonPair(sb, "payload", e.PayloadJson ?? "");
                sb.Append('}');
                if (i < _timeline.Count - 1) sb.Append(',');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static void AppendJsonPair(System.Text.StringBuilder sb, string key, string value)
        {
            sb.Append('\"').Append(Escape(key)).Append('\"').Append(':').Append('\"').Append(Escape(value)).Append('\"');
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        private static string BuildEventPayloadJson(DuelLogger.LogEntry e)
        {
            // Compact key=value pairs as JSON-like string. (Keep it simple & readable.)
            // You can swap this with proper JSON later if you add a JSON lib.
            var parts = new List<string>(4);
            if (!string.IsNullOrEmpty(e.Source)) parts.Add($"src={e.Source}");
            if (!string.IsNullOrEmpty(e.Actor))  parts.Add($"actor={e.Actor}");
            if (!string.IsNullOrEmpty(e.Data))   parts.Add($"data={e.Data}");
            return string.Join("; ", parts);
        }
    }
}
