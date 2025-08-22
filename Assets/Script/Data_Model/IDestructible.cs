// Model.Contracts.cs
using System;

namespace YGO.Duel.Model.Contracts
{
    /// <summary>Anything that can be destroyed by battle or effects.</summary>
    public interface IDestructible
    {
        /// <summary>Destroy via effect (not battle). Optional source string for logs.</summary>
        void DestroyByEffect(string source = null);
    }

    /// <summary>Anything that could be banished (tokens will just vanish).</summary>
    public interface IBanishable
    {
        void Banish(bool faceDown = false, string source = null);
    }

    /// <summary>Anything that could be sent to the graveyard (tokens will just vanish).</summary>
    public interface IGraveMovable
    {
        void SendToGrave(string source = null);
    }

    /// <summary>Reasoned “leave field” categories for UI/logs.</summary>
    public enum RemoveReason
    {
        Battle,
        Effect,
        BanishRedirect,
        GraveyardRedirect,
        Rule
    }
}