// HintToast.cs
// Tiny toast that fades in/out to nudge players (e.g., "You can respond", "Missing timing").

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace YGO.Duel.UI
{
    public sealed class HintToast : MonoBehaviour
    {
        public CanvasGroup cg;
        public Text messageText;
        public float fadeIn = 0.15f;
        public float hold   = 1.65f;
        public float fadeOut= 0.4f;

        private Coroutine _routine;

        private void Reset()
        {
            cg = GetComponentInChildren<CanvasGroup>();
            messageText = GetComponentInChildren<Text>();
        }

        public void Show(string msg, float? overrideHold = null)
        {
            if (messageText) messageText.text = msg ?? "";
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(CoShow(overrideHold ?? hold));
        }

        public void YouCanRespond() => Show("You can respond");
        public void MissingTiming()  => Show("Missing timing");

        private IEnumerator CoShow(float holdTime)
        {
            if (!cg) yield break;

            cg.gameObject.SetActive(true);
            // fade in
            for (float t = 0f; t < fadeIn; t += Time.deltaTime)
            {
                cg.alpha = Mathf.Lerp(0f, 1f, t / fadeIn);
                yield return null;
            }
            cg.alpha = 1f;

            // hold
            yield return new WaitForSeconds(holdTime);

            // fade out
            for (float t = 0f; t < fadeOut; t += Time.deltaTime)
            {
                cg.alpha = Mathf.Lerp(1f, 0f, t / fadeOut);
                yield return null;
            }
            cg.alpha = 0f;
            cg.gameObject.SetActive(false);
            _routine = null;
        }
    }
}