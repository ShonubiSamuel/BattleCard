// ChainView.cs
// Renders the current chain (top-most last). Pure view; listens to a read-only chain interface.

using System.Text;
using UnityEngine;
using UnityEngine.UI;
using YGO.Duel.Chain;
using YGO.Duel.Foundation;

public sealed class ChainView : MonoBehaviour
{
    [Header("UI")]
    public Text chainText; // multiline text

    // Minimal read-only bridge so we don't couple to your ChainManager type
    public interface IChainReadOnly
    {
        System.Collections.Generic.IReadOnlyList<ChainLink> Current { get; }
        event System.Action OnChainChanged;
    }

    private IChainReadOnly _chain;
    private DuelLogger _logger;

    private void Awake()
    {
        ServiceLocator.TryGet(out _logger);

        // Try resolve a compatible chain service
        // If your ChainManager implements this interface, register it with ServiceLocator.
        ServiceLocator.TryGet(out _chain);

        if (_chain != null)
            _chain.OnChainChanged += Refresh;

        if (_logger != null)
            _logger.OnLogged += _ => Refresh();
    }

    private void OnDestroy()
    {
        if (_chain != null)
            _chain.OnChainChanged -= Refresh;
        if (_logger != null)
            _logger.OnLogged -= _ => Refresh();
    }

    private void Start() => Refresh();

    public void Refresh()
    {
        if (!chainText) return;

        if (_chain == null || _chain.Current == null || _chain.Current.Count == 0)
        {
            chainText.text = "(no chain)";
            return;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < _chain.Current.Count; i++)
        {
            var link = _chain.Current[i];
            sb.Append('#').Append(link.Index)
                .Append(' ').Append(link.Effect?.EffectName ?? "Effect")
                .Append(" — ").Append(link.Activator.ToString())
                .Append('\n');
        }
        chainText.text = sb.ToString();
    }
}