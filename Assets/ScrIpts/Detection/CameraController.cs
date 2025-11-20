using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Detection
{
    public class CameraController : MonoBehaviour
    {
        [Serializable]
        public enum CameraTarget
        {
            Default = -2,
            Target,
            Drone1,
            Drone2,
            Drone3,
            Drone4,
            Drone5,
            Drone6,
            Drone7,
            Drone8,
            Drone9,
            Drone10,
        }

        [SerializeField]
        private float _minZ = -50;
        [SerializeField]
        private float _zoomAmount = 10f;
        [SerializeField]
        private Transform _target;
        [SerializeField]
        DroneVisualiser _visualiser;
        private Transform _camera;
        private Quaternion _initialRotation;
        private Quaternion _initialCameraLocalRotation;
        private Vector3 _initialPivotPosition;
        private Vector3 _initialCameraLocalPosition;
        private float _initialZ;
        private int _currentTarget;

        private void Awake()
        {
            _camera = transform.GetChild(0);
            _initialRotation = transform.rotation;
            _initialPivotPosition = transform.position;
            _initialCameraLocalPosition = _camera.localPosition;
            _initialCameraLocalRotation = _camera.localRotation;
            _initialZ = _camera.localPosition.z;
        }
        private void Update()
        {
            if (Mouse.current.middleButton.isPressed == true || Mouse.current.rightButton.isPressed == true)
            {
                var rot = transform.rotation.eulerAngles;
                transform.rotation = Quaternion.Euler(rot.x, rot.y + Pointer.current.delta.x.ReadValue(), rot.z);
            }
            if (Mouse.current.scroll.ReadValue().y != 0)
            {
                _camera.position += _zoomAmount * Mouse.current.scroll.ReadValue().y * _camera.forward;
            }
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
        public void Retarget()
        {
            SetFocusTo(_currentTarget);
        }

        public void SetFocusTo(int target)
        {
            _currentTarget = target;
            target -= 2;
            Debug.Log((CameraTarget)target);

            if (target == (int)CameraTarget.Default)
            {
                transform.position = _initialPivotPosition;
                _camera.localPosition = _initialCameraLocalPosition;
                _zoomAmount = 50f;
            }
            else
            {
                var targetTransform = ((CameraTarget)target == CameraTarget.Target ? _target : _visualiser.GetDrone(target));
                var cameraTrueAngle = 90 - Vector3.Angle(_camera.forward, Vector3.ProjectOnPlane(_camera.forward, Vector3.up));
                transform.position = new(targetTransform.position.x, transform.position.y, targetTransform.position.z);
                _camera.localPosition = new(_initialCameraLocalPosition.x, _initialCameraLocalPosition.y, -50f);
                var projectedDistance = Vector3.ProjectOnPlane(targetTransform.position - _camera.position, Vector3.up).magnitude;
                var offset = projectedDistance / Mathf.Tan(cameraTrueAngle * Mathf.Deg2Rad);
                _camera.position = new Vector3(_camera.position.x, targetTransform.position.y + offset, _camera.position.z);
                _zoomAmount = 5;
            }

        }
    }
}

