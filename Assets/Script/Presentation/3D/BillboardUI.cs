using UnityEngine;

[DisallowMultipleComponent]
public sealed class BillboardUI : MonoBehaviour
{
    [Tooltip("Optional: assign a camera explicitly; otherwise uses Camera.main.")]
    public Camera targetCamera;

    private void LateUpdate()
    {
        if (!targetCamera)
            targetCamera = Camera.main;
        if (!targetCamera) return;

        // Rotate to face the camera
        var camForward = targetCamera.transform.forward;
        var camUp      = targetCamera.transform.up;

        transform.rotation = Quaternion.LookRotation(camForward, camUp);
    }
}