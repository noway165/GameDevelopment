using UnityEngine;

namespace Metropolis.Game
{
    /// <summary>Moves a yaw-only rig; the camera is a tilted child, so forward stays flat.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        const float MapSize = 800f;

        [SerializeField] float panSpeed = 1f;
        [SerializeField] float rotateSpeed = 90f;
        [SerializeField] float zoomStep = 20f;
        [SerializeField] float minHeight = 30f;
        [SerializeField] float maxHeight = 300f;

        public void Pan(Vector2 input, float deltaTime)
        {
            var step = (transform.right * input.x + transform.forward * input.y)
                * (panSpeed * transform.position.y * deltaTime);
            MoveTo(transform.position + step);
        }

        public void Rotate(float direction, float deltaTime) =>
            transform.Rotate(0f, direction * rotateSpeed * deltaTime, 0f, Space.World);

        public void Zoom(float notches) =>
            MoveTo(transform.position + Vector3.down * (notches * zoomStep));

        void MoveTo(Vector3 position) =>
            transform.position = Clamp(position, MapSize, minHeight, maxHeight);

        public static Vector3 Clamp(Vector3 position, float mapSize, float minHeight, float maxHeight) =>
            new Vector3(
                Mathf.Clamp(position.x, 0f, mapSize),
                Mathf.Clamp(position.y, minHeight, maxHeight),
                Mathf.Clamp(position.z, 0f, mapSize));
    }
}
