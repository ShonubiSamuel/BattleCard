using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Foundation;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class PlayerAvatar3D : MonoBehaviour
{
    [Header("Identity")]
    public BoardManager.Seat seat = BoardManager.Seat.P1;

    [Header("Aim / VFX")]
    [Tooltip("Where monsters should lunge to on a direct attack. Defaults to this.transform if null.")]
    public Transform attackOrigin;

    [Header("Optional Visuals")]
    public GameObject highlightObject;

    public void SetHighlighted(bool on) { if (highlightObject) highlightObject.SetActive(on); }

    private void Start()
    {
        if (!attackOrigin) attackOrigin = transform;

        if (ServiceLocator.TryGet<IAvatarLocator>(out var loc) && loc != null)
        {
            // Prefer interface method if available, else fall back to concrete type.
            if (loc is IAvatarRegistry reg) reg.Register(seat, attackOrigin);
            else if (loc is AvatarLocatorService svc) svc.Register(seat, attackOrigin);
        }
        else
        {
            Debug.LogWarning("[PlayerAvatar3D] No IAvatarLocator in ServiceLocator; direct attacks will use fallback.");
        }
    }

    private void OnDestroy()
    {
        if (ServiceLocator.TryGet<IAvatarLocator>(out var loc) && loc is IAvatarRegistry reg)
            reg.Unregister(seat);
    }
}