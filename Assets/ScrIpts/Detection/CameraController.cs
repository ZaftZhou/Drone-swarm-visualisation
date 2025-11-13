using UnityEngine;

namespace Detection
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField]
        private float _minZ = -50;

        private Transform _camera;
        private Quaternion _initialRotation;
        private float _initialZ;

        private void Awake()
        {
            _camera = transform.GetChild(0);
            _initialRotation = transform.rotation;
            _initialZ = _camera.localPosition.z;
        }

        public void SetRotation(float amount)
        {
            var ea = _initialRotation.eulerAngles;
            transform.rotation = Quaternion.Euler(ea.x, ea.y + 180 * amount, ea.z);
        }

        public void SetZoom(float amount)
        {
            amount = Mathf.Clamp01(amount);
            var p = _camera.localPosition;
            _camera.localPosition = new Vector3(p.x, p.y, Mathf.Lerp(_initialZ, _minZ, amount));
        }
    }
}

