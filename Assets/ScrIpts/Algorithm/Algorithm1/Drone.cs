// Drone_Fixed.cs
// 修复版无人机控制脚本
// ✅ 修复角落摇摆问题
// ✅ 正确的高度控制
// ✅ 防止无人机穿模
// ✅ 增强传感器可视化
// ✅ 更真实的飞行效果

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Drone : MonoBehaviour
{
    [Header("Movement Parameters")]
    [SerializeField] private float maxSpeed = 15f;
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float targetReachedThreshold = 1.5f;
    [SerializeField] private float minSpeedForRotation = 0.5f;

    [Header("Flight Dynamics")]
    [Range(0f, 45f)]
    [SerializeField] private float maxTiltAngle = 15f;
    [SerializeField] private float tiltSpeed = 3f;
    [Range(0f, 1f)]
    [SerializeField] private float verticalDamping = 0.95f;

    [SerializeField] private float altitudeControlStrength = 10f;

    [Header("Collision Avoidance ")]
    [SerializeField] private bool enableCollisionAvoidance = true;
    [SerializeField] private float avoidanceDistance = 5f;
    [SerializeField] private float avoidanceStrength = 3f;
    [SerializeField] private LayerMask avoidanceLayer;

    [Header("Sensing Parameters")]
    [SerializeField] private float sensorRadius = 10f;
    [SerializeField] private SensorType sensorType = SensorType.Cone;
    [Range(15f, 120f)]
    [SerializeField] private float coneAngle = 60f;
    [SerializeField] private LayerMask droneLayer;

    [Header("Visualization 可视化")]
    [SerializeField] private bool showSensorRange = true;
    [SerializeField] private Color sensorColor = new Color(0, 1, 0, 0.3f);
    [SerializeField] private bool showScannedArea = true;
    [SerializeField] private Color scannedColor = new Color(0, 0.5f, 1f, 0.15f);
    [SerializeField] private bool showDirectionArrow = true;

    private Rigidbody rb;
    private Vector3 currentTarget;
    private bool isMovingToTarget = false;
    private Vector3 currentTilt = Vector3.zero;
    private float targetAltitude = 0f; 
    private List<Vector3> scannedPositions = new List<Vector3>();
    private float lastScanRecordTime = 0f;
    private const float scanRecordInterval = 0.5f;

    public Vector3 Velocity => rb.linearVelocity;
    public Vector3 Position => transform.position;
    public float SensorRadius => sensorRadius;
    public SensorType CurrentSensorType => sensorType;

    public enum SensorType
    {
        Cylinder,
        Cone
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Drone lack of Rigidbody！", this);
        }

        rb.useGravity = false;
        rb.angularDamping = 3f;
        rb.linearDamping = 0.5f; 
        currentTarget = transform.position;
        targetAltitude = transform.position.y;
    }

    void FixedUpdate()
    {
        if (isMovingToTarget)
        {
            HandleTargetMovement();
        }

        MaintainAltitude();
        if (enableCollisionAvoidance)
        {
            ApplyCollisionAvoidance();
        }

        EnforceSpeedAndFlightLevel();

        ApplyRealisticTilt();

        RecordScannedPosition();
    }

    private void HandleTargetMovement()
    {
        Vector3 toTarget = currentTarget - Position;
        float distanceToTarget = toTarget.magnitude;

        if (distanceToTarget < targetReachedThreshold)
        {
   
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, 0.1f);
            isMovingToTarget = false;
            return;
        }

        Vector3 horizontalDirection = new Vector3(toTarget.x, 0, toTarget.z);
        float horizontalDistance = horizontalDirection.magnitude;

        if (horizontalDistance < 0.01f)
        {
            return;
        }

        horizontalDirection.Normalize();
        float speedFactor = Mathf.Clamp01(horizontalDistance / 10f);
        float currentMaxSpeed = maxSpeed * Mathf.Max(speedFactor, 0.3f); 
        Vector3 desiredVelocity = horizontalDirection * currentMaxSpeed;
        Vector3 currentHorizontalVelocity = new Vector3(Velocity.x, 0, Velocity.z);
        Vector3 velocityChange = desiredVelocity - currentHorizontalVelocity;
        velocityChange = Vector3.ClampMagnitude(velocityChange, acceleration * Time.fixedDeltaTime);

        rb.AddForce(velocityChange, ForceMode.VelocityChange);

        if (currentHorizontalVelocity.magnitude > minSpeedForRotation)
        {
            float targetYaw = Mathf.Atan2(currentHorizontalVelocity.x, currentHorizontalVelocity.z) * Mathf.Rad2Deg;
            float currentYaw = transform.eulerAngles.y;

            float yawDifference = Mathf.DeltaAngle(currentYaw, targetYaw);

            float dynamicRotationSpeed = rotationSpeed * Mathf.Clamp01(currentHorizontalVelocity.magnitude / maxSpeed);
            float newYaw = currentYaw + yawDifference * dynamicRotationSpeed * Time.fixedDeltaTime;

            Vector3 currentEuler = transform.eulerAngles;
            transform.eulerAngles = new Vector3(currentEuler.x, newYaw, currentEuler.z);
        }
    }


    private void MaintainAltitude()
    {
        float currentAltitude = Position.y;
        float altitudeError = targetAltitude - currentAltitude;

        if (Mathf.Abs(altitudeError) > 0.1f)
        {
            float verticalForce = altitudeError * altitudeControlStrength;
            verticalForce -= rb.linearVelocity.y * 2f;

            rb.AddForce(Vector3.up * verticalForce, ForceMode.Acceleration);
        }

        Vector3 vel = rb.linearVelocity;
        vel.y *= (1.0f - verticalDamping);
        rb.linearVelocity = vel;
    }


    private void ApplyCollisionAvoidance()
    {
        Collider[] nearbyDrones = Physics.OverlapSphere(Position, avoidanceDistance, avoidanceLayer);

        Vector3 avoidanceForce = Vector3.zero;
        int avoidanceCount = 0;

        foreach (var collider in nearbyDrones)
        {
            if (collider.gameObject == this.gameObject)
                continue;

            Vector3 toOther = collider.transform.position - Position;
            float distance = toOther.magnitude;

            if (distance < avoidanceDistance && distance > 0.1f)
            {
                float repulsionStrength = (1f - distance / avoidanceDistance) * avoidanceStrength;
                Vector3 repulsion = -toOther.normalized * repulsionStrength;

                repulsion.y = 0;

                avoidanceForce += repulsion;
                avoidanceCount++;
            }
        }

        if (avoidanceCount > 0)
        {
            rb.AddForce(avoidanceForce, ForceMode.Acceleration);
        }
    }


    private void ApplyRealisticTilt()
    {
        Vector3 horizontalVel = new Vector3(Velocity.x, 0, Velocity.z);

        if (horizontalVel.sqrMagnitude < 0.1f)
        {
            currentTilt = Vector3.Lerp(currentTilt, Vector3.zero, tiltSpeed * Time.fixedDeltaTime);
        }
        else
        {
            Vector3 localVel = transform.InverseTransformDirection(horizontalVel);

            float speedRatio = Mathf.Clamp01(horizontalVel.magnitude / maxSpeed);

            float targetRoll = -localVel.x / maxSpeed * maxTiltAngle * speedRatio;

            float targetPitch = localVel.z / maxSpeed * maxTiltAngle * 0.3f * speedRatio;

            Vector3 targetTilt = new Vector3(targetPitch, 0, targetRoll);
            currentTilt = Vector3.Lerp(currentTilt, targetTilt, tiltSpeed * Time.fixedDeltaTime);
        }

        Vector3 currentEuler = transform.eulerAngles;
        transform.eulerAngles = new Vector3(
            currentTilt.x,
            currentEuler.y,
            currentTilt.z
        );
    }

    private void EnforceSpeedAndFlightLevel()
    {
        Vector3 vel = rb.linearVelocity;

        Vector3 horizontalVel = new Vector3(vel.x, 0, vel.z);
        if (horizontalVel.sqrMagnitude > maxSpeed * maxSpeed)
        {
            horizontalVel = horizontalVel.normalized * maxSpeed;
            vel.x = horizontalVel.x;
            vel.z = horizontalVel.z;
        }

        rb.linearVelocity = vel;
    }


    private void RecordScannedPosition()
    {
        if (Time.time - lastScanRecordTime > scanRecordInterval)
        {
            scannedPositions.Add(Position);
            lastScanRecordTime = Time.time;

            if (scannedPositions.Count > 1000)
            {
                scannedPositions.RemoveAt(0);
            }
        }
    }


    public void SetNewTarget(Vector3 newTarget)
    {
        currentTarget = newTarget;
        targetAltitude = newTarget.y; 
        isMovingToTarget = true;
    }

    public void ApplyForce(Vector3 force)
    {
        isMovingToTarget = false;
        rb.AddForce(force);
    }

    public bool IsCloseToTarget()
    {
        return Vector3.Distance(Position, currentTarget) < targetReachedThreshold;
    }

    public List<Drone> GetNeighbors()
    {
        List<Drone> neighbors = new List<Drone>();
        Collider[] hits = Physics.OverlapSphere(Position, sensorRadius, droneLayer);

        foreach (var hit in hits)
        {
            if (hit.gameObject == this.gameObject)
                continue;

            Drone neighbor = hit.GetComponent<Drone>();
            if (neighbor != null)
            {
                neighbors.Add(neighbor);
            }
        }
        return neighbors;
    }

    public bool IsPointInSensorRange(Vector3 point)
    {
        Vector3 toPoint = point - Position;
        float distance = toPoint.magnitude;

        if (distance > sensorRadius)
            return false;

        if (sensorType == SensorType.Cylinder)
        {
            return true;
        }
        else // Cone
        {
            Vector3 down = -transform.up;
            float angle = Vector3.Angle(down, toPoint);
            return angle <= coneAngle / 2f;
        }
    }

    public void SetSensorRadius(float radius)
    {
        sensorRadius = radius;
    }


    public void ClearScannedHistory()
    {
        scannedPositions.Clear();
    }


    void OnDrawGizmos()
    {
        Vector3 pos = Application.isPlaying ? Position : transform.position;

        if (showSensorRange)
        {
            if (sensorType == SensorType.Cylinder)
            {
                DrawCylinderSensor(pos);
            }
            else
            {
                DrawConeSensor(pos);
            }
        }

        if (showScannedArea && Application.isPlaying && scannedPositions.Count > 1)
        {
            Gizmos.color = scannedColor;
            for (int i = 0; i < scannedPositions.Count - 1; i++)
            {
                Vector3 scanPos = scannedPositions[i];

                if (sensorType == SensorType.Cylinder)
                {
                    if (i % 3 == 0)
                    {
                        DrawCircle(scanPos, sensorRadius, Vector3.up);
                    }
                }
                else
                {
                    if (i % 3 == 0)
                    {
                        Vector3 down = Vector3.down;
                        float halfAngle = coneAngle / 2f * Mathf.Deg2Rad;
                        float baseRadius = sensorRadius * Mathf.Tan(halfAngle);
                        Vector3 baseCenter = scanPos + down * sensorRadius;
                        DrawCircle(baseCenter, baseRadius, down);
                    }
                }
            }
        }

        if (showDirectionArrow && Application.isPlaying && Velocity.sqrMagnitude > 0.1f)
        {
            Gizmos.color = Color.cyan;
            Vector3 direction = Velocity.normalized;
            Gizmos.DrawRay(pos, direction * 3f);

            Vector3 arrowTip = pos + direction * 3f;
            Vector3 right = Vector3.Cross(direction, Vector3.up).normalized * 0.5f;
            Gizmos.DrawLine(arrowTip, arrowTip - direction * 0.5f + right);
            Gizmos.DrawLine(arrowTip, arrowTip - direction * 0.5f - right);
        }

        if (enableCollisionAvoidance && Application.isPlaying)
        {
            Gizmos.color = new Color(1, 0, 0, 0.1f);
            Gizmos.DrawWireSphere(pos, avoidanceDistance);
        }
    }

    void DrawCylinderSensor(Vector3 center)
    {
        Gizmos.color = sensorColor;

        float height = sensorRadius * 2f;
        Vector3 topCenter = center + Vector3.up * height / 2f;
        Vector3 bottomCenter = center - Vector3.up * height / 2f;

        DrawCircle(topCenter, sensorRadius, Vector3.up);
        DrawCircle(bottomCenter, sensorRadius, Vector3.up);

        int segments = 8;
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * sensorRadius;

            Vector3 top = topCenter + offset;
            Vector3 bottom = bottomCenter + offset;

            Gizmos.DrawLine(top, bottom);
        }
    }

    void DrawConeSensor(Vector3 apex)
    {
        Gizmos.color = sensorColor;

        Vector3 down = -transform.up;
        float halfAngle = coneAngle / 2f * Mathf.Deg2Rad;
        float baseRadius = sensorRadius * Mathf.Tan(halfAngle);
        Vector3 baseCenter = apex + down * sensorRadius;

        DrawCircle(baseCenter, baseRadius, down);

        int segments = 8;
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;

            Vector3 right = Vector3.Cross(down, Vector3.forward).normalized;
            if (right.sqrMagnitude < 0.1f)
                right = Vector3.Cross(down, Vector3.up).normalized;

            Vector3 forward = Vector3.Cross(right, down).normalized;
            Vector3 offset = (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * baseRadius;
            Vector3 bottomPoint = baseCenter + offset;

            Gizmos.DrawLine(apex, bottomPoint);
        }

        Gizmos.color = new Color(sensorColor.r, sensorColor.g, sensorColor.b, 1f);
        Gizmos.DrawLine(apex, baseCenter);
    }

    void DrawCircle(Vector3 center, float radius, Vector3 normal)
    {
        int segments = 24;
        Vector3 prevPoint = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;

            Vector3 right = Vector3.Cross(normal, Vector3.forward).normalized;
            if (right.sqrMagnitude < 0.1f)
                right = Vector3.Cross(normal, Vector3.up).normalized;

            Vector3 forward = Vector3.Cross(right, normal).normalized;
            Vector3 offset = (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius;
            Vector3 point = center + offset;

            if (i > 0)
            {
                Gizmos.DrawLine(prevPoint, point);
            }

            prevPoint = point;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        if (isMovingToTarget)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(currentTarget, 0.5f);
            Gizmos.DrawLine(Position, currentTarget);
        }
    }
}