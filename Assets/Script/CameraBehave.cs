using UnityEngine;

public class CameraBehave : MonoBehaviour
{
    public GameObject plane;
    public bool followAngle;
    private Vector3 displacement;
    private Quaternion angleDisplacement;

    void Start()
    {
        // ���� �� plane�� ī�޶��� ��� ��ġ/ȸ�� ����
        displacement = this.transform.position - plane.transform.position;
        angleDisplacement = Quaternion.Inverse(plane.transform.rotation) * this.transform.rotation;
    }

    void LateUpdate() // ī�޶� ������ LateUpdate ����
    {
        if (followAngle)
        {
            // plane�� Y�� ȸ���� ����
            float yaw = plane.transform.eulerAngles.y;

            // Yaw�� �ִ� ���ʹϾ� ����
            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);

            // ����
            this.transform.rotation = yawRot * angleDisplacement;
            this.transform.position = plane.transform.position + yawRot * displacement;
        }

        else
        {
            // ��ġ�� ���󰡱�
            this.transform.position = plane.transform.position + displacement;
        }
    }
}
