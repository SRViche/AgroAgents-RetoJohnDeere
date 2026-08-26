using UnityEngine;

[RequireComponent(typeof(Camera))]
public class IsometricView : MonoBehaviour
{
    [SerializeField] private Vector3 targetPoint = Vector3.zero;
    [SerializeField] private GridManager gridManager;

    [SerializeField] private float distance = 200f;
    [SerializeField] private float yaw = 45f;
    [SerializeField] private float pitch = 35.264f;

    [SerializeField] private bool useOrthographic = true;
    [SerializeField] private float padding = 2f;

    private void Start()
    {
        Vector3 focus = targetPoint;

        if (gridManager != null)
        {
            focus = new Vector3(
                gridManager.Width * gridManager.TileSize * 0.5f,
                0f,
                gridManager.Height * gridManager.TileSize * 0.5f);
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rotation;
        transform.position = focus - rotation * Vector3.forward * distance;

        Camera cam = GetComponent<Camera>();
        if (useOrthographic && cam != null)
        {
            cam.orthographic = true;
            if (gridManager != null)
            {
                float halfExtent = Mathf.Max(gridManager.Width, gridManager.Height) * gridManager.TileSize * 0.5f;
                cam.orthographicSize = halfExtent + padding;
            }
        }
    }
}