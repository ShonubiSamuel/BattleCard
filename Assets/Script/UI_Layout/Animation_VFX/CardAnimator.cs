// CardAnimator.cs
// Simple, dependency-free tweens for cards (UI or world): move, flip, scale, shake.
// Works with RectTransform (UI) or Transform (world). Optional link to CardView for face-up/down flips.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using YGO.Duel.UI; // for CardView (optional)

namespace YGO.Duel.VFX
{
    public sealed class CardAnimator : MonoBehaviour
    {
        [Header("Defaults")]
        [Range(0.01f, 2f)] public float defaultMove = 0.25f;
        [Range(0.01f, 0.8f)] public float defaultFlip = 0.18f;
        [Range(0.01f, 0.8f)] public float defaultScale = 0.18f;
        [Range(0.01f, 1.0f)] public float defaultShake = 0.25f;
        public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public bool unscaledTime = true;

        // --------------- Public API ---------------

        public Coroutine MoveTo(Transform t, Vector3 target, float? duration = null)
            => StartCoroutine(CoMoveTo(t, target, duration ?? defaultMove));

        public Coroutine MoveTo(RectTransform rt, Vector2 anchoredTarget, float? duration = null)
            => StartCoroutine(CoMoveToRT(rt, anchoredTarget, duration ?? defaultMove));

        public Coroutine ScaleTo(Transform t, Vector3 targetScale, float? duration = null)
            => StartCoroutine(CoScaleTo(t, targetScale, duration ?? defaultScale));

        public Coroutine Pulse(Transform t, float peakScale = 1.08f, float? duration = null)
            => StartCoroutine(CoPulse(t, peakScale, duration ?? defaultScale));

        public Coroutine FlipY(CardView view, bool toFaceDown, float? duration = null)
            => StartCoroutine(CoFlipY(view, toFaceDown, duration ?? defaultFlip));

        public Coroutine FlipY(Transform t, float? duration = null, System.Action halfway = null)
            => StartCoroutine(CoFlipYRaw(t, duration ?? defaultFlip, halfway));

        public Coroutine Shake(Transform t, float magnitude = 8f, float? duration = null, float frequency = 30f)
            => StartCoroutine(CoShake(t, magnitude, duration ?? defaultShake, frequency));

        // --------------- Coroutines ---------------

        private IEnumerator CoMoveTo(Transform t, Vector3 to, float d)
        {
            if (!t) yield break;
            var from = t.position;
            float t0 = unscaledTime ? Time.unscaledTime : Time.time;
            while (true)
            {
                float u = Mathf.Clamp01(((unscaledTime ? Time.unscaledTime : Time.time) - t0) / Mathf.Max(0.0001f, d));
                t.position = Vector3.LerpUnclamped(from, to, ease.Evaluate(u));
                if (u >= 1f) break;
                yield return null;
            }
        }

        private IEnumerator CoMoveToRT(RectTransform rt, Vector2 to, float d)
        {
            if (!rt) yield break;
            var from = rt.anchoredPosition;
            float t0 = unscaledTime ? Time.unscaledTime : Time.time;
            while (true)
            {
                float u = Mathf.Clamp01(((unscaledTime ? Time.unscaledTime : Time.time) - t0) / Mathf.Max(0.0001f, d));
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, ease.Evaluate(u));
                if (u >= 1f) break;
                yield return null;
            }
        }

        private IEnumerator CoScaleTo(Transform t, Vector3 to, float d)
        {
            if (!t) yield break;
            var from = t.localScale;
            float t0 = unscaledTime ? Time.unscaledTime : Time.time;
            while (true)
            {
                float u = Mathf.Clamp01(((unscaledTime ? Time.unscaledTime : Time.time) - t0) / Mathf.Max(0.0001f, d));
                t.localScale = Vector3.LerpUnclamped(from, to, ease.Evaluate(u));
                if (u >= 1f) break;
                yield return null;
            }
        }

        private IEnumerator CoPulse(Transform t, float peak, float d)
        {
            if (!t) yield break;
            var baseScale = t.localScale;
            var up = d * 0.5f; var down = d - up;
            yield return CoScaleTo(t, baseScale * peak, up);
            yield return CoScaleTo(t, baseScale, down);
        }

        // flip around Y with a halfway callback to swap graphics
        private IEnumerator CoFlipYRaw(Transform t, float d, System.Action halfway)
        {
            if (!t) yield break;
            var from = t.localEulerAngles;
            var to = from + new Vector3(0f, 180f, 0f);

            float t0 = unscaledTime ? Time.unscaledTime : Time.time;
            bool called = false;

            while (true)
            {
                float u = Mathf.Clamp01(((unscaledTime ? Time.unscaledTime : Time.time) - t0) / Mathf.Max(0.0001f, d));
                float e = ease.Evaluate(u);

                // rotate 0->90, call halfway, rotate 90->180
                float y = Mathf.LerpUnclamped(from.y, to.y, e);
                t.localEulerAngles = new Vector3(from.x, y, from.z);

                if (!called && u >= 0.5f)
                {
                    called = true;
                    halfway?.Invoke();
                }

                if (u >= 1f) break;
                yield return null;
            }
        }

        private IEnumerator CoFlipY(CardView view, bool toFaceDown, float d)
        {
            if (!view) yield break;
            var t = view.transform;
            yield return CoFlipYRaw(t, d, () =>
            {
                // halfway: actually flip the card data
                view.SetFaceDown(toFaceDown);
                // tiny pulse when reveal
                if (!toFaceDown) StartCoroutine(CoPulse(t, 1.05f, defaultScale * 0.8f));
            });
        }

        private IEnumerator CoShake(Transform t, float mag, float duration, float freq)
        {
            if (!t) yield break;

            // Guard/normalize inputs
            duration = Mathf.Max(0f, duration);
            freq     = Mathf.Max(1f, freq); // at least 1 tick/sec

            var rt = t as RectTransform;
            bool isRT = rt != null;

            // Cache base position
            Vector2 basePos2 = Vector2.zero;
            Vector3 basePos3 = Vector3.zero;
            if (isRT) basePos2 = rt.anchoredPosition; else basePos3 = t.localPosition;

            float step   = 1f / freq; // seconds per shake tick
            float tEnd   = (unscaledTime ? Time.unscaledTime : Time.time) + duration;

            while ((unscaledTime ? Time.unscaledTime : Time.time) < tEnd)
            {
                float angle = Random.value * (Mathf.PI * 2f);
                float dx = Mathf.Cos(angle) * mag;
                float dy = Mathf.Sin(angle) * mag;

                if (isRT) rt.anchoredPosition = basePos2 + new Vector2(dx, dy);
                else      t.localPosition     = basePos3 + new Vector3(dx, dy, 0f);

                if (unscaledTime)
                    yield return new WaitForSecondsRealtime(step);
                else
                    yield return new WaitForSeconds(step);
            }

            // Restore base position
            if (isRT) rt.anchoredPosition = basePos2;
            else      t.localPosition     = basePos3;
        }

    }
}
