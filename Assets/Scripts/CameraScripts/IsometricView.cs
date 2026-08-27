using AgroAgents.Presentation.Authoring;
using AgroAgents.Presentation.Mapping;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class IsometricView : MonoBehaviour
{
    [SerializeField] private Vector3 targetPoint = Vector3.zero;
    [SerializeField] private WorldBootstrapper worldBootstrapper;

    [SerializeField] private float distance = 200f;
    [SerializeField] private float yaw = 45f;
    [SerializeField] private float pitch = 35.264f;

    [SerializeField] private bool useOrthographic = true;
    [SerializeField] private float padding = 2f;

    private bool _initialized;

    private void LateUpdate()
    {
        if (_initialized) return;
        if (worldBootstrapper == null) return;

        CoordinateMapper mapper = worldBootstrapper.Mapper;
        if (mapper == null) return;

        _initialized = true;

        Vector3 focus = mapper.GridCentreWorld;

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rotation;
        transform.position = focus - rotation * Vector3.forward * distance;

        Camera cam = GetComponent<Camera>();
        if (useOrthographic && cam != null)
        {
            cam.orthographic = true;
            float halfExtent = Mathf.Max(mapper.Width, mapper.Height) * mapper.TileSize * 0.5f;
            cam.orthographicSize = halfExtent + padding;
        }
    }
}
