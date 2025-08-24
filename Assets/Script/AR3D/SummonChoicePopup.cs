// using System;
// using TMPro;
// using UnityEngine;
// using UnityEngine.UI;
// using YGO.Duel.Cards;
//
// public sealed class SummonChoicePopup : MonoBehaviour
// {
//     [Header("Wiring")]
//     public CanvasGroup group;
//     public Button btnNormalSummon;
//     public Button btnSetMonster;
//     public Button btnCancel;
//
//     [Header("Optional labels")]
//     public TMP_Text normalLabel;
//     public TMP_Text setLabel;
//
//     private Action _onNormal;
//     private Action _onSet;
//
//     void Awake()
//     {
//         Hide();
//         if (btnCancel) btnCancel.onClick.AddListener(Hide);
//     }
//
//     /// <summary>
//     /// Show popup; enable/disable options and supply callbacks.
//     /// </summary>
//     public void Show(Card card, bool canNormal, string normalWhy, bool canSet, string setWhy,
//                      Action onNormal, Action onSet, Vector2? screenPos = null)
//     {
//         _onNormal = onNormal;
//         _onSet    = onSet;
//
//         if (btnNormalSummon)
//         {
//             btnNormalSummon.interactable = canNormal;
//             btnNormalSummon.onClick.RemoveAllListeners();
//             btnNormalSummon.onClick.AddListener(() => { _onNormal?.Invoke(); Hide(); });
//             if (normalLabel) normalLabel.text = canNormal ? "Normal Summon" : $"Normal (blocked: {normalWhy})";
//         }
//
//         if (btnSetMonster)
//         {
//             btnSetMonster.interactable = canSet;
//             btnSetMonster.onClick.RemoveAllListeners();
//             btnSetMonster.onClick.AddListener(() => { _onSet?.Invoke(); Hide(); });
//             if (setLabel) setLabel.text = canSet ? "Set (Face-Down DEF)" : $"Set (blocked: {setWhy})";
//         }
//
//         if (group) { group.alpha = 1f; group.blocksRaycasts = true; group.interactable = true; }
//         gameObject.SetActive(true);
//
//         // Optional: reposition near clicked card
//         if (screenPos.HasValue && TryGetComponent<RectTransform>(out var rt))
//         {
//             rt.anchoredPosition = screenPos.Value; // Assumes Canvas render mode = Screen Space - Overlay
//         }
//     }
//
//     public void Hide()
//     {
//         if (group) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
//         gameObject.SetActive(false);
//         _onNormal = _onSet = null;
//     }
// }