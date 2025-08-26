using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YGO.Duel.Battle;
using YGO.Duel.Foundation;
using YGO.Duel.Runtime;
using YGO.Duel.Runtime.Actions;
using Card = YGO.Duel.Cards.Card;

public sealed class BattleAnimationController3D : MonoBehaviour
{
    [Header("Motion")]
    public float travelTime = 0.35f;
    public float holdTime   = 0.10f;
    public float returnTime = 0.30f;
    public float directLungeDistance = 0.35f;

    private EventBus       _bus;
    private DuelLogger     _log;
    public SpawnManager3D _spawner;

    // Track active attack coroutines per attacker to avoid overlaps
    private readonly Dictionary<Card, Coroutine> _running = new();
    
    private ActionQueue  _queue;
    private ICardIndex   _index;
    private TurnManager  _turns;
    
    private readonly Dictionary<Card, Vector3> _startPos = new();


    private void Start()
    {
        ServiceLocator.TryGet(out _bus);
        ServiceLocator.TryGet(out _log);
        //ServiceLocator.TryGet(out _spawner);
        ServiceLocator.TryGet(out _queue);
        ServiceLocator.TryGet(out _index);
        ServiceLocator.TryGet(out _turns);

        if (_bus != null)
        {
            _bus.OnAttackDeclared  += OnAttackDeclared;
            _bus.OnBattleDamage    += OnBattleDamage;
            if (ServiceLocator.TryGet(out BattleManager battle) && battle != null)
                battle.OnAfterDamageStep += OnAfterDamageStep;
        }
    }

    private void OnDisable()
    {
        if (_bus != null)
        {
            _bus.OnAttackDeclared  -= OnAttackDeclared;
            _bus.OnBattleDamage    -= OnBattleDamage;
        }
        if (ServiceLocator.TryGet(out BattleManager battle) && battle != null)
            battle.OnAfterDamageStep -= OnAfterDamageStep;

    }


    private void OnAttackDeclared(object sender, AttackDeclaredEvent e)
    {
        var atkCard = e.AttackerCard;
        var tgtCard = e.TargetCard; // null => direct
        if (atkCard == null || _spawner == null) return;

        // if (_spawner.TryGetView(atkCard, out var atkView) && atkView)
        //     _startPos[atkCard] = atkView.transform.position;

        if (_running.TryGetValue(atkCard, out var c) && c != null) StopCoroutine(c);
        _running[atkCard] = StartCoroutine(AnimateThenResolve(atkCard, tgtCard));
    }


    private IEnumerator AnimateThenResolve(Card atkCard, Card tgtCardOrNull)
    {
        if (!_spawner.TryGetView(atkCard, out var atkView) || !atkView) yield break;
        var attackTf = atkView.GetAttackTransform();
        if (!attackTf) yield break;
        
        
        var startPos = attackTf.position;
        CacheStart(atkCard, startPos);

        Vector3 targetPos;

        if (tgtCardOrNull != null && _spawner.TryGetView(tgtCardOrNull, out var tgtView) && tgtView)
        {
            targetPos = tgtView.AttackOrigin ? tgtView.AttackOrigin.position : tgtView.transform.position;
        }
        else
        {
            // DIRECT ATTACK → aim at opponent avatar’s AttackOrigin
            if (ServiceLocator.TryGet<IAvatarLocator>(out var avatars) && avatars != null)
            {
                var defender = YGO.Duel.Board.BoardManager.OpponentOf(atkCard.Controller);
                var aim = avatars.GetAttackOrigin(defender);
                if (aim) targetPos = aim.position;
                else     targetPos = startPos + attackTf.forward * directLungeDistance; // fallback
            }
            else
            {
                targetPos = startPos + attackTf.forward * directLungeDistance;
            }
        }

        
        // Lunge
        yield return MoveWorld(attackTf, startPos, targetPos, travelTime);
        yield return new WaitForSeconds(holdTime);

        // Impact -> enqueue resolve now
        if (_queue != null && _turns != null)
        {
            var a = new ResolveDamageStepAction {
                attackerId = GetId(atkCard),
                targetId   = (tgtCardOrNull != null) ? GetId(tgtCardOrNull) : null
            };
            a.FillSnapshot(atkCard.Controller, _turns);
            _queue.Enqueue(a, out _);
        }

        // Do NOT return here. We'll decide after we hear the result.
        _running.Remove(atkCard);
    }
    
    private void CacheStart(Card c, Vector3 pos) => _startPos[c] = pos;

    
    private string GetId(Card c)
    {
        if (_index != null)
        {
            var id = _index.GetId(c);
            if (!string.IsNullOrEmpty(id)) return id;
        }
        return c.InstanceId;
    }
    
    private void OnAfterDamageStep(IBattler attacker, IBattler target, AttackOutcome outcome, int lp, DamageType type)
    {
        // Map IBattler -> Card (CardBattlerAdapter exposes RuntimeCard)
        var atkCard = (attacker is CardBattlerAdapter ca) ? ca.RuntimeCard : null;
        if (atkCard == null) return;

        // If attacker destroyed, do nothing; SpawnManager3D will despawn on OnCardMoved
        if (outcome == AttackOutcome.AttackerDestroyed || outcome == AttackOutcome.BothDestroyed)
            return;

        // Otherwise, return attacker to cached start (if it still exists)
        if (_spawner.TryGetView(atkCard, out var atkView) && atkView && _startPos.TryGetValue(atkCard, out var start))
        {
            var attackTf = atkView.GetAttackTransform();
            if (attackTf) StartCoroutine(MoveWorld(attackTf, attackTf.position, start, returnTime));
        }
    }


    private void OnBattleDamage(object sender, BattleDamageEvent e)
    {
        // Optional: play LP hit UI, screenshake, etc.
    }



    private static IEnumerator MoveWorld(Transform tr, Vector3 a, Vector3 b, float time)
    {
        if (!tr || time <= 0f) yield break;
        float t0 = 0f;
        while (t0 < 1f)
        {
            t0 += Time.deltaTime / time;
            tr.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t0));
            yield return null;
        }
        tr.position = b;
    }
}
