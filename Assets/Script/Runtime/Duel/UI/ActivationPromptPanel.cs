// Assets/Script/Runtime/Duel/UI/ActivationPromptPanel.cs
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Chain;
using YGO.Duel.Chain.YGO.Duel.Chain;
using YGO.Duel.Effects; // EffectLibrary
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Targeting;

public sealed class ActivationPromptPanel : MonoBehaviour
{
    [Header("Wiring")]
    public CanvasGroup root;           // fades + blocks
    public TMP_Text titleLabel;        // “Card Name — Effect”
    public TMP_Text bodyLabel;         // effect text/body
    public Button activateBtn;         // “Activate”
    public Button pickTargetsBtn;      // “Pick Targets…”
    public Button cancelBtn;           // “Cancel”

    [Header("Optional")]
    public TargetPickerOverlay targetPickerOverlay; // assign if you want target picking

    // Services
    private DuelLogger _log;
    private RuleSet _rules;
    private BoardManager _board;
    private IChainManager _chain;

    // Context for current card/effect
    private Card _card;
    private string _effectId;
    private IEffectHandle _handle;
    private BoardManager.Seat _seat;
    private RuleSet.Timing _timing;

    private IDisposable _lock;

    void Awake()
    {
        HideImmediate();
        ServiceLocator.TryGet(out _log);
        ServiceLocator.TryGet(out _rules);
        ServiceLocator.TryGet(out _board);
        ServiceLocator.TryGet(out _chain);

        if (cancelBtn) cancelBtn.onClick.AddListener(Hide);
        if (activateBtn) activateBtn.onClick.AddListener(OnActivateNoTargets);
        if (pickTargetsBtn) pickTargetsBtn.onClick.AddListener(OnPickTargets);
    }

    public void Show(Card card, string effectId, BoardManager.Seat activator, RuleSet.Timing timing)
    {
        _card = card; _effectId = effectId ?? ""; _seat = activator; _timing = timing;

        _handle = card.Def.GetHandleFromBlueprint(card, effectId);

        // UI text
        if (titleLabel) titleLabel.text = $"{card?.Name ?? "(Card)"} — {_handle?.EffectName ?? "Effect"}";
        if (bodyLabel)  bodyLabel.text  = card?.Def?.effectText ?? "(No text)";

        // If you want to auto-decide whether targets are required, you’d inspect the handle here.
        // For now: expose both buttons; you can hide 'Pick Targets' if this card never targets.
        if (pickTargetsBtn) pickTargetsBtn.gameObject.SetActive(targetPickerOverlay != null);

        _lock?.Dispose();
        _lock = InputLockService.Acquire();

        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
        _card = null; _handle = null;
        _lock?.Dispose(); _lock = null;
    }

    private void HideImmediate()
    {
        if (root) { root.alpha = 0; root.blocksRaycasts = false; root.interactable = false; }
        else gameObject.SetActive(false);
    }

    private void SetVisible(bool v)
    {
        if (root)
        {
            root.alpha = v ? 1 : 0;
            root.blocksRaycasts = v;
            root.interactable = v;
        }
        else gameObject.SetActive(v);
    }

    private void OnActivateNoTargets()
    {
        if (_card == null || _handle == null || _chain == null) { Hide(); return; }

        // No targets: add a link with empty list
        var args = new YGO.Duel.Chain.AddLinkArgs(
            activator: _seat,
            source: _card,
            sourceId: _card.InstanceId,
            isCardSource: true,
            effect: _handle,
            targets: new List<ITargetRef>(),
            timing: _timing,
            summaryOverride: null
        );
        if (!_chain.TryAddLink(args, out var link, out var why))
        {
            _log?.LogText("UI.Activate.Fail", $"Chain rejected: {why}", source: nameof(ActivationPromptPanel));
        }
        else
        {
            ServiceLocator.TryGet(out EventBus bus);
            bus?.RaiseCardActivated(_card, _handle.Speed, _effectId);
        }
        Hide();
    }

    private void OnPickTargets()
    {
        if (targetPickerOverlay == null || _card == null || _handle == null) { return; }

        // Example targeting config:
        // - Pick 1 monster on the opponent field (face-up or any—your choice).
        // Adjust this to your effect’s real requirements.
        var me   = _seat;
        var opp  = BoardManager.OpponentOf(me);

        Func<Card, bool> filter = c =>
            c != null &&
            c.Controller == opp &&
            c.IsOnField &&
            c.IsMonsterRuntime; // <- tweak per effect

        // Request: exactly 1 target
        var req = new TargetPickerOverlay.Request(
            min: 1, max: 1,
            filter: filter,
            label: "Select 1 opponent monster"
        );

        targetPickerOverlay.Show(
            request: req,
            onDone: targets =>
            {
                // targets: List<Card>
                var trefs = new List<ITargetRef>(targets.Count);
                foreach (var card in targets)
                    trefs.Add(new CardTargetRef(card));
// If you want a stricter validity rule:
// trefs.Add(new CardTargetRef(card, c => c != null && c.IsOnField && c.IsFaceUp));
                var args = new YGO.Duel.Chain.AddLinkArgs(
                    activator: _seat,
                    source: _card,
                    sourceId: _card.InstanceId,
                    isCardSource: true,
                    effect: _handle,
                    targets: trefs,
                    timing: _timing,
                    summaryOverride: null
                );

                if (!_chain.TryAddLink(args, out var link, out var why))
                {
                    _log?.LogText("UI.Activate.Fail", $"Chain rejected: {why}", source: nameof(ActivationPromptPanel));
                }
                else if (ServiceLocator.TryGet(out EventBus bus))
                {
                    bus.RaiseCardActivated(_card, _handle.Speed, _effectId);
                }

                Hide();
            },
            onCancel: () => { /* nothing */ }
        );
    }
}