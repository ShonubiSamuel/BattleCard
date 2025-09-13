// Assets/Script/Runtime/Duel/UI/ChainViewerPanel.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YGO.Duel.Chain;
using YGO.Duel.Foundation;

public sealed class ChainViewerPanel : MonoBehaviour
{
    [Header("Wiring")]
    public TMP_Text body;  // Multi-line text area

    // Services
    private IChainManager _chain;
    private EventBus _bus;

    void Start()
    {
        ServiceLocator.TryGet(out _chain);
        ServiceLocator.TryGet(out _bus);

        if (_bus != null)
        {
            _bus.OnChainLinkAdded   += (_, __) => Refresh();
            _bus.OnChainResolved    += (_, __) => Refresh();
            _bus.OnChainCleared     += (_, __) => Refresh();
        }
        Refresh();
    }

    void OnDestroy()
    {
        if (_bus != null)
        {
            _bus.OnChainLinkAdded   -= (_, __) => Refresh();
            _bus.OnChainResolved    -= (_, __) => Refresh();
            _bus.OnChainCleared     -= (_, __) => Refresh();
        }
    }

    public void Refresh()
    {
        if (body == null || _chain == null)
        {
            if (body) body.text = "(No chain)";
            return;
        }

        if (_chain.IsEmpty)
        {
            body.text = "Chain: (empty)";
            return;
        }

        var sb = new System.Text.StringBuilder();
        var links = _chain.Snapshot(); // bottom..top
        sb.AppendLine("Chain (bottom → top):");
        for (int i = 0; i < links.Count; i++)
        {
            var L = links[i];
            sb.Append(i+1).Append(". ");
            sb.Append(L.ActivationSummary);
            sb.Append("  [SS").Append((int)L.Speed).Append("]");
            if (L.Targets != null && L.Targets.Count > 0)
            {
                sb.Append("  Targets: ");
                for (int t = 0; t < L.Targets.Count; t++)
                {
                    if (t > 0) sb.Append(", ");
                    sb.Append(L.Targets[t]?.DebugName ?? "(null)");
                }
            }
            sb.AppendLine();
        }
        body.text = sb.ToString();
    }
}