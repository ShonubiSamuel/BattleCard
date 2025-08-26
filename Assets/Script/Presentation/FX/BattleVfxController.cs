// BattleVfxController.cs
// Attack lines, hit sparks, damage popups, and optional screen shake.
// Designed to be called directly OR to subscribe to an optional battle events interface.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.UI;   // CardView
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.VFX
{
    /// <summary>
    /// Optional battle events bridge. If you have a battle system, expose these and register via ServiceLocator.
    /// </summary>
    public interface IBattleEvents
    {
        // target may be null for direct attacks
        event System.Action<object /*attacker*/, object /*target*/> OnAttackDeclared;
        event System.Action<int /*amount*/, BoardManager.Seat /*victim*/> OnBattleDamage;
        event System.Action<Vector3 /*hitPos*/> OnHitRegistered;
    }

    public sealed class BattleVfxController : MonoBehaviour
    {
        [Header("Prefabs (optional)")]
        public LineRenderer attackLinePrefab;      // simple line from attacker to target
        public ParticleSystem hitSparkPrefab;      // spawn at target
        public Text damageTextPrefab;              // UI popup text for damage

        [Header("Anchors")]
        public Canvas uiCanvas;                    // for damage text
        public RectTransform p1LpAnchor;
        public RectTransform p2LpAnchor;

        [Header("Tuning")]
        public float attackLineDuration = 0.25f;
        public float hitSparkDuration = 0.6f;
        public Color attackLineColor = Color.white;
        public Vector3 lineOffset = new Vector3(0, 0, 0);
        public Vector2 dmgOffset = new Vector2(0, 30f);

        [Header("Shake")]
        public CardAnimator animator;              // optional; used for small shakes
        public Transform shakeTargetCamera;        // optional, e.g., camera transform
        public float shakeMagnitude = 10f;
        public float shakeDuration  = 0.12f;

        private Camera _cam;
        private DuelLogger _logger;

        private void Awake()
        {
            _cam = Camera.main;
            ServiceLocator.TryGet(out _logger);

            // Optional subscription if a battle event source exists
            if (ServiceLocator.TryGet<IBattleEvents>(out var ev) && ev != null)
            {
                ev.OnAttackDeclared += HandleAttackDeclared;
                ev.OnBattleDamage   += HandleBattleDamage;
                ev.OnHitRegistered  += pos => PlayHitSpark(pos);
            }
        }

        private void OnDestroy()
        {
            if (ServiceLocator.TryGet<IBattleEvents>(out var ev) && ev != null)
            {
                ev.OnAttackDeclared -= HandleAttackDeclared;
                ev.OnBattleDamage   -= HandleBattleDamage;
            }
        }

        // --------- Public methods you can call from your battle system ---------

        public void PlayAttack(CardView attacker, CardView targetOrNull, float? duration = null)
        {
            if (!attacker) return;
            StartCoroutine(CoAttackLine(attacker.transform.position + lineOffset,
                                        targetOrNull ? targetOrNull.transform.position + lineOffset
                                                     : attacker.transform.position + lineOffset + Vector3.up * 1.0f,
                                        duration ?? attackLineDuration));
        }

        public void PlayHitSpark(Vector3 worldPos)
        {
            if (!hitSparkPrefab) return;
            var vfx = Instantiate(hitSparkPrefab, worldPos, Quaternion.identity);
            Destroy(vfx.gameObject, hitSparkDuration);
            if (animator && shakeTargetCamera) animator.Shake(shakeTargetCamera, shakeMagnitude, shakeDuration);
        }

        public void ShowDamage(BoardManager.Seat victim, int amount)
        {
            if (!damageTextPrefab || !uiCanvas) return;
            var anchor = (victim == BoardManager.Seat.P1 ? p1LpAnchor : p2LpAnchor);
            if (!anchor) return;

            var txt = Instantiate(damageTextPrefab, uiCanvas.transform);
            txt.text = "-" + amount.ToString();
            txt.color = (amount >= 0 ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.2f, 0.8f, 0.2f)); // heal if negative

            var rt = txt.rectTransform;
            rt.anchoredPosition = anchor.anchoredPosition + dmgOffset;

            StartCoroutine(FadeAndRise(txt, 0.8f));
        }

        // --------- Event adapters ---------

        private void HandleAttackDeclared(object attackerObj, object targetObj)
        {
            CardView atk = null, tgt = null;
            if (attackerObj is CardView cav) atk = cav;
            else if (attackerObj is Card ac && CardViewRegistry.TryGet(ac, out var cav2)) atk = cav2;

            if (targetObj is CardView ctv) tgt = ctv;
            else if (targetObj is Card tc && CardViewRegistry.TryGet(tc, out var ctv2)) tgt = ctv2;

            PlayAttack(atk, tgt);
        }

        private void HandleBattleDamage(int amount, BoardManager.Seat victim)
            => ShowDamage(victim, amount);

        // --------- Internals ---------

        private IEnumerator CoAttackLine(Vector3 from, Vector3 to, float d)
        {
            if (!attackLinePrefab) yield break;

            var lr = Instantiate(attackLinePrefab, transform);
            lr.positionCount = 2;
            lr.startColor = lr.endColor = attackLineColor;
            lr.SetPosition(0, from);
            lr.SetPosition(1, from);

            float t0 = Time.time;
            while (true)
            {
                float u = Mathf.Clamp01((Time.time - t0) / Mathf.Max(0.0001f, d));
                lr.SetPosition(1, Vector3.Lerp(from, to, u));
                if (u >= 1f) break;
                yield return null;
            }

            Destroy(lr.gameObject, 0.02f);
        }

        private IEnumerator FadeAndRise(Text t, float d)
        {
            var cg = t.GetComponent<CanvasGroup>();
            if (!cg) cg = t.gameObject.AddComponent<CanvasGroup>();

            var rt = t.rectTransform;
            var start = rt.anchoredPosition;
            var end = start + new Vector2(0, 32f);

            float t0 = Time.unscaledTime;
            while (true)
            {
                float u = Mathf.Clamp01((Time.unscaledTime - t0) / d);
                rt.anchoredPosition = Vector2.Lerp(start, end, u);
                cg.alpha = 1f - u;
                if (u >= 1f) break;
                yield return null;
            }
            Destroy(t.gameObject);
        }
    }
}
