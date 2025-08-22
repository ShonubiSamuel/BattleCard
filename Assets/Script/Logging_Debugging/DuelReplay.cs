// DuelReplay.cs
// Steps through recorded INPUTS and asks your dispatcher to apply them to the live game.
// Events in the recording are kept for context but not executed (they were effects of inputs).

using System;
using System.Collections.Generic;

namespace YGO.Duel.Foundation
{
    /// <summary>
    /// Your game supplies one of these to apply inputs during replay.
    /// Example: switch(type) { case "Input.Attack": // parse payload and call BattleFlow.TryAttack(...) }
    /// </summary>
    public interface IReplayDispatcher
    {
        void Dispatch(string type, string payloadJson);
    }

    public sealed class DuelReplay
    {
        private readonly List<RecordedEntry> _inputs = new List<RecordedEntry>();
        private int _index = 0;

        public int Count => _inputs.Count;
        public int Index => _index;                // next input to play
        public bool IsDone => _index >= _inputs.Count;

        public DuelReplay() { }

        /// <summary>Load from a recorder's timeline (filters only inputs).</summary>
        public void LoadFrom(DuelRecorder recorder)
        {
            _inputs.Clear();
            _index = 0;
            foreach (var e in recorder.Timeline)
                if (e.Kind == RecordKind.Input)
                    _inputs.Add(e);
        }

        /// <summary>Load directly from a list of entries (only INPUT entries are used).</summary>
        public void Load(IEnumerable<RecordedEntry> timeline)
        {
            _inputs.Clear();
            _index = 0;
            foreach (var e in timeline)
                if (e.Kind == RecordKind.Input)
                    _inputs.Add(e);
        }

        /// <summary>Reset playback to the beginning.</summary>
        public void Reset() => _index = 0;

        /// <summary>Advance to an absolute index (0..Count). Values outside the range are clamped.</summary>
        public void Seek(int index) => _index = Math.Max(0, Math.Min(index, _inputs.Count));

        /// <summary>Execute the next input via the provided dispatcher. Returns false when finished.</summary>
        public bool StepNext(IReplayDispatcher dispatcher, out RecordedEntry executed)
        {
            executed = null;
            if (_index >= _inputs.Count) return false;

            var e = _inputs[_index++];
            executed = e;

            // Let the app apply the input to the current game state.
            dispatcher?.Dispatch(e.Type, e.PayloadJson);
            return true;
        }

        /// <summary>Play until done (or until maxSteps executed). Returns count executed.</summary>
        public int PlayAll(IReplayDispatcher dispatcher, int maxSteps = int.MaxValue)
        {
            int count = 0;
            while (_index < _inputs.Count && count < maxSteps)
            {
                StepNext(dispatcher, out _);
                count++;
            }
            return count;
        }
    }
}
