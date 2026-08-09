using UnityEngine;

public class ChefHeadLookAt : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float rotationSpeed = 5f;

    // Rotation limits
    [SerializeField] private float maxYaw = 60f;
    [SerializeField] private float maxPitch = 25f;

    private Quaternion initialLocalRotation;

    private void Start()
    {
        initialLocalRotation = transform.localRotation;
    }

    private void LateUpdate()
    {
        if (player == null)
            return;

        Vector3 dir = player.position - transform.position;

        Quaternion targetRotation = Quaternion.LookRotation(dir);

        // Convert to local rotation relative to parent
        Quaternion localTarget = Quaternion.Inverse(transform.parent.rotation) * targetRotation;

        Vector3 angles = localTarget.eulerAngles;

        angles.x = NormalizeAngle(angles.x);
        angles.y = NormalizeAngle(angles.y);

        angles.x = Mathf.Clamp(angles.x, -maxPitch, maxPitch);
        angles.y = Mathf.Clamp(angles.y, -maxYaw, maxYaw);
        angles.z = 0;

        Quaternion finalRotation = initialLocalRotation * Quaternion.Euler(angles);

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            finalRotation,
            rotationSpeed * Time.deltaTime);
    }

    float NormalizeAngle(float angle)
    {
        if (angle > 180)
            angle -= 360;

        return angle;
    }
}