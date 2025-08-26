using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Runtime; // for TurnManager

/// <summary>
/// Hook this to your Screen Space Canvas. Wire P1/P2 TMP labels and (optional) bars/highlights.
/// Listens to EventBus.OnLifePointsChanged and TurnManager.OnTurnStarted.
/// </summary>
public sealed class LifePointsHUD : MonoBehaviour
{
    [Header("P1 UI")]
    public TMP_Text p1LpText;
    public Image    p1BarFill;            // optional
    public GameObject p1TurnHighlight;    // optional glow/frame

    [Header("P2 UI")]
    public TMP_Text p2LpText;
    public Image    p2BarFill;            // optional
    public GameObject p2TurnHighlight;    // optional glow/frame

    [Header("Visuals")]
    [Tooltip("Duration of the little flash when LP changes.")]
    public float flashDuration = 0.25f;
    [Tooltip("How much to scale the LP text during flash.")]
    public float flashScale = 1.15f;

    [Tooltip("Max LP used to normalize bar fill. If 0, auto-reads from BoardManager at Start.")]
    public int maxLPOverride = 0;

    // services
    private EventBus _bus;
    private BoardManager _board;
    private TurnManager _turns;

    private int _maxLP = 8000; // sensible default

    private void Start()
    {
        ServiceLocator.TryGet(out _bus);
        ServiceLocator.TryGet(out _board);
        ServiceLocator.TryGet(out _turns);

        // Determine max LP once, either override or from board if available.
        if (maxLPOverride > 0) _maxLP = maxLPOverride;
        else if (_board != null && _board.Players != null && _board.Players.Length >= 2)
            _maxLP = Mathf.Max(_board.Players[0].LifePoints, _board.Players[1].LifePoints);

        // Subscribe
        if (_bus != null)
            _bus.OnLifePointsChanged += OnLifePointsChanged;

        if (_turns != null)
            _turns.OnTurnStarted += OnTurnStarted;

        // Initial paint
        RefreshAllFromBoard();
        RefreshTurnHighlight();
    }

    private void OnDestroy()
    {
        if (_bus != null)
            _bus.OnLifePointsChanged -= OnLifePointsChanged;

        if (_turns != null)
            _turns.OnTurnStarted -= OnTurnStarted;
    }

    // ---------- Event handlers ----------

    private void OnLifePointsChanged(object sender, LifePointsChangedEvent e)
    {
        var isP1 = e.Seat == BoardManager.Seat.P1;
        var lp = Mathf.Max(0, e.Current);

        // Text
        if (isP1 && p1LpText) p1LpText.text = lp.ToString();
        if (!isP1 && p2LpText) p2LpText.text = lp.ToString();

        // Bar (normalized)
        float t = _maxLP > 0 ? Mathf.Clamp01(lp / (float)_maxLP) : 1f;
        if (isP1 && p1BarFill) p1BarFill.fillAmount = t;
        if (!isP1 && p2BarFill) p2BarFill.fillAmount = t;

        // Small feedback
        if (isP1 && p1LpText) StartCoroutine(FlashPulse(p1LpText.rectTransform));
        if (!isP1 && p2LpText) StartCoroutine(FlashPulse(p2LpText.rectTransform));
    }

    private void OnTurnStarted(BoardManager.Seat seat, int turn)
    {
        RefreshTurnHighlight();
    }

    // ---------- Helpers ----------

    private void RefreshAllFromBoard()
    {
        if (_board == null || _board.Players == null || _board.Players.Length < 2)
        {
            // still set something so the HUD isn't empty
            if (p1LpText) p1LpText.text = "8000";
            if (p2LpText) p2LpText.text = "8000";
            if (p1BarFill) p1BarFill.fillAmount = 1f;
            if (p2BarFill) p2BarFill.fillAmount = 1f;
            return;
        }

        var p1 = _board.Players[(int)BoardManager.Seat.P1].LifePoints;
        var p2 = _board.Players[(int)BoardManager.Seat.P2].LifePoints;

        if (p1LpText) p1LpText.text = p1.ToString();
        if (p2LpText) p2LpText.text = p2.ToString();

        if (_maxLP <= 0) _maxLP = Mathf.Max(p1, p2, 1);

        if (p1BarFill) p1BarFill.fillAmount = Mathf.Clamp01(p1 / (float)_maxLP);
        if (p2BarFill) p2BarFill.fillAmount = Mathf.Clamp01(p2 / (float)_maxLP);
    }

    private void RefreshTurnHighlight()
    {
        if (_turns == null) { SetActive(p1TurnHighlight, false); SetActive(p2TurnHighlight, false); return; }

        var cur = _turns.CurrentPlayer;
        SetActive(p1TurnHighlight, cur == BoardManager.Seat.P1);
        SetActive(p2TurnHighlight, cur == BoardManager.Seat.P2);
    }

    private IEnumerator FlashPulse(RectTransform rt)
    {
        if (!rt) yield break;

        float t = 0f;
        var baseScale = rt.localScale;
        var peak = baseScale * flashScale;

        // scale up
        while (t < flashDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / flashDuration);
            rt.localScale = Vector3.Lerp(baseScale, peak, Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }

        // scale back
        t = 0f;
        while (t < flashDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / flashDuration);
            rt.localScale = Vector3.Lerp(peak, baseScale, Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }

        rt.localScale = baseScale;
    }

    private static void SetActive(GameObject go, bool on) { if (go) go.SetActive(on); }
}