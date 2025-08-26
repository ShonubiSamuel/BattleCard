// // ContextMenuPanel.cs (new)
// using System;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;
// using YGO.Duel.Foundation;
//
// public interface IContextMenuService
// {
//     void Show(Vector2 screenPos, IReadOnlyList<(string label, Action onClick, bool interactable)> items);
//     void Hide();
//     bool IsOpen { get; }
// }
//
// [DefaultExecutionOrder(-200)]
// public sealed class ContextMenuPanel : MonoBehaviour, IContextMenuService
// {
//     [Header("Wiring")]
//     [Tooltip("Root GameObject for the panel")]
//     public RectTransform panelRoot;
//     [Tooltip("Parent for runtime buttons")]
//     public RectTransform buttonsParent;
//     [Tooltip("Prefab with Button + TMP_Text")]
//     public Button buttonPrefab;
//
//     [Header("Behavior")]
//     public bool closeOnOutsideClick = true;
//
//     private readonly List<Button> _spawned = new();
//     private Canvas _canvas;
//
//     private void Awake()
//     {
//         _canvas = GetComponentInParent<Canvas>();
//         if (panelRoot) panelRoot.gameObject.SetActive(false);
//         ServiceLocator.Register<IContextMenuService>(this, overwrite: true);
//     }
//
//     private void OnDisable() { if (IsOpen) Hide(); }
//
//     public bool IsOpen => panelRoot && panelRoot.gameObject.activeSelf;
//
//     public void Show(Vector2 screenPos, IReadOnlyList<(string label, Action onClick, bool interactable)> items)
//     {
//         if (!panelRoot || !buttonsParent || !buttonPrefab) return;
//
//         ClearButtons();
//
//         foreach (var (label, onClick, interactable) in items)
//         {
//             var btn = Instantiate(buttonPrefab, buttonsParent);
//             _spawned.Add(btn);
//
//             // Text
//             var txt = btn.GetComponentInChildren<TMP_Text>();
//             if (txt) txt.text = label;
//
//             // Interactability + callback
//             btn.interactable = interactable;
//             btn.onClick.RemoveAllListeners();
//             btn.onClick.AddListener(() =>
//             {
//                 onClick?.Invoke();
//                 Hide();
//             });
//         }
//
//         // Position panel at screenPos
//         if (_canvas != null && _canvas.renderMode != RenderMode.WorldSpace)
//         {
//             RectTransformUtility.ScreenPointToLocalPointInRectangle(
//                 _canvas.transform as RectTransform, screenPos, _canvas.worldCamera, out var local);
//             panelRoot.anchoredPosition = local;
//         }
//
//         panelRoot.gameObject.SetActive(true);
//         InputLockService.PushLock(this); // lock board while open
//     }
//
//     public void Hide()
//     {
//         if (!IsOpen) return;
//         panelRoot.gameObject.SetActive(false);
//         ClearButtons();
//         InputLockService.PopLock(this);
//     }
//
//     private void Update()
//     {
// #if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
//         if (IsOpen && closeOnOutsideClick && Input.GetMouseButtonDown(0))
//         {
//             // If click is outside our rect → close
//             if (!RectTransformUtility.RectangleContainsScreenPoint(panelRoot, Input.mousePosition, _canvas ? _canvas.worldCamera : null))
//                 Hide();
//         }
// #endif
//     }
//
//     private void ClearButtons()
//     {
//         for (int i = 0; i < _spawned.Count; i++)
//             if (_spawned[i]) Destroy(_spawned[i].gameObject);
//         _spawned.Clear();
//     }
// }