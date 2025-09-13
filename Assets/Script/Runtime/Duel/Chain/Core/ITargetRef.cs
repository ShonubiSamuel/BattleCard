using System;

namespace YGO.Duel.Chain
{
    namespace YGO.Duel.Chain
    {
        /// Minimal, chain-friendly contract
        public interface ITargetRef
        {
            /// Stable identifier to re-resolve (card instance id, zone key, player key, …)
            string Id { get; }

            /// Short label for UI/logs (e.g., "Blue-Eyes @ P1.MZ[2]" or "P2")
            string DebugName { get; }

            /// Original object (card, zone, etc.). May be null later—don’t rely on it.
            object Raw { get; }

            /// True if the target still exists & is legal when resolving.
            bool IsStillValid();
        }
    }
}