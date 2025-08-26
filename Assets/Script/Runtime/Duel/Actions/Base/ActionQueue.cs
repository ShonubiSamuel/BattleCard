// ActionQueue.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using YGO.Duel.Foundation;

// Make "GameAction" mean your Actions base type everywhere in this file
using GameAction       = YGO.Duel.Runtime.Actions.GameAction;
using ActionEnvelope   = YGO.Duel.Runtime.Actions.ActionEnvelope;
using GameActionCodec  = YGO.Duel.Runtime.Actions.GameActionCodec;

namespace YGO.Duel.Runtime
{
    public interface IGameActionValidator
    {
        bool Validate(GameAction action, out string error);
    }

    [Serializable]
    public sealed class ActionQueue
    {
        [Serializable]
        private sealed class EnvelopeList { public List<ActionEnvelope> items = new List<ActionEnvelope>(); }

        private readonly List<GameAction> _queue = new List<GameAction>(256);
        private long _nextSeq = 1;
        private readonly DuelLogger _logger;
        private readonly string _sessionId;
        private IGameActionValidator _validator;

        public event Action<GameAction> OnActionEnqueued;
        public event Action<GameAction> OnActionDequeued;

        public int Count => _queue.Count;

        public ActionQueue(DuelLogger logger, string sessionId = null)
        {
            _logger = logger ?? new DuelLogger();
            _sessionId = string.IsNullOrEmpty(sessionId) ? Guid.NewGuid().ToString("N") : sessionId;
        }

        public void SetValidator(IGameActionValidator validator) => _validator = validator;

        public bool Enqueue(GameAction action, out string error)
        {
            error = "";

            if (action == null)
            {
                error = "Null action";
                return false;
            }

            // Attach metadata if not set
            if (action.seq <= 0) action.seq = _nextSeq++;
            if (string.IsNullOrEmpty(action.sessionId)) action.sessionId = _sessionId;

            if (_validator != null && !_validator.Validate(action, out error))
            {
                _logger.LogText("ActionQueue.Reject", $"Rejected {action.Type}", data: error, source: nameof(ActionQueue));
                return false;
            }

            _queue.Add(action);
            _logger.LogText("ActionQueue.Enqueue", action.ToString(), source: nameof(ActionQueue));
            OnActionEnqueued?.Invoke(action);
            return true;
        }

        public bool TryPeek(out GameAction action)
        {
            if (_queue.Count > 0) { action = _queue[0]; return true; }
            action = null; return false;
        }

        public bool TryDequeue(out GameAction action)
        {
            if (_queue.Count == 0) { action = null; return false; }
            action = _queue[0];
            _queue.RemoveAt(0);
            _logger.LogText("ActionQueue.Dequeue", action.ToString(), source: nameof(ActionQueue));
            OnActionDequeued?.Invoke(action);
            return true;
        }

        public void Clear()
        {
            _queue.Clear();
            _nextSeq = 1;
            _logger.LogText("ActionQueue.Clear", "Queue cleared", source: nameof(ActionQueue));
        }

        // ---- Serialization using envelopes (polymorphism-safe) ----

        public string ToJson()
        {
            var env = new EnvelopeList();
            foreach (var a in _queue)
                env.items.Add(GameActionCodec.Serialize(a));
            return JsonUtility.ToJson(env);
        }

        public void FromJson(string json)
        {
            Clear();
            if (string.IsNullOrEmpty(json)) return;

            var env = JsonUtility.FromJson<EnvelopeList>(json);
            if (env?.items == null) return;

            long maxSeq = 0;
            foreach (var e in env.items)
            {
                var a = GameActionCodec.Deserialize(e);
                if (a == null) continue;
                _queue.Add(a);
                maxSeq = Math.Max(maxSeq, a.seq);
            }
            _nextSeq = Math.Max(_nextSeq, maxSeq + 1);

            _logger.LogText("ActionQueue.Load", $"Loaded {_queue.Count} actions", source: nameof(ActionQueue));
        }
    }
}
