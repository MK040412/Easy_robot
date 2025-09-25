using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ���� ��ü�� ����, �����߽�(CoM), ���� y�� �������Ʈ(Iy)��
/// �ڽ� Thruster/Brake ������ ���� Start���� �������� ����� Rigidbody�� �ݿ�.
/// </summary>
[DisallowMultipleComponent]
public class RocketMassInertiaBuilder : MonoBehaviour
{
    [Header("References")]
    public Rigidbody rb; // ������ �� �ڵ� GetComponent

    [Header("Body/Brake Base Settings")]
    [Tooltip("��ü(Body) ���� ���� [kg]")]
    public float bodyMass = 10f;
    [Tooltip("��ü ���� ���� [m] (y�࿡ ����)")]
    public float bodyLength = 2f;
    [Tooltip("����극��ũ 1���� ���� [kg]")]
    public float brakeMass = 0.1f;
    [Tooltip("����극��ũ ���� ���� [m] (y�࿡ ����)")]
    public float brakeLength = 1f;

    [Header("Thruster Mass Rule")]
    [Tooltip("Thruster ���� ����: m_t = maxThrust / thrustMassDivisor")]
    public float thrustMassDivisor = 10f; // ���� ����: m = MaxThrust/10

    [Header("Axis Control")]
    [Tooltip("y�ุ ȸ���ϵ��� X/Z�� ���� ũ�� ����")]
    public bool exaggerateIxIz = true;
    [Tooltip("Ix, Iz = Iy * lockMultiplier (yaw�� ��� ȿ��)")]
    public float lockMultiplier = 1000f;

    // ���� ĳ��
    private List<(float m, Vector3 localPos, float Iy_intrinsic)> _parts = new();

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        Rebuild();
    }

    /// <summary>
    /// ���� ������ �ʿ��� �� ȣ��(������ ��ư ��� ȣ�� ����)
    /// </summary>
    [ContextMenu("Rebuild Mass/CoM/Iy")]
    public void Rebuild()
    {
        _parts.Clear();
        if (rb == null) { Debug.LogError("[RocketMassInertiaBuilder] Rigidbody not set."); return; }

        // === 1) �ڽ� ���� �� ����/��ü Iy ��� ===
        float totalMass = 0f;

        // 1-1) Body �ڽ� (����, ���� bodyLength, y�� ����)
        {
            float m = Mathf.Max(0f, bodyMass);
            Vector3 localPos = Vector3.zero; // ��ü�� �������� CoM�̶� ����
            float Iy_intrinsic = (1f / 12f) * m * (bodyLength * bodyLength); // ����, �� ���� y
            _parts.Add((m, localPos, Iy_intrinsic));
            totalMass += m;
        }

        // 1-2) Thruster��
        var thrusters = GetComponentsInChildren<ThrusterBehave>(includeInactive: true);
        foreach (var t in thrusters)
        {
            if (t == null) continue;
            float T = Mathf.Max(0f, t.maxThrust);
            float mt = T / thrustMassDivisor;

            // ������ s = (T/10)^(1/3)  (�и� 10�� ���� ���� ����)
            float s = Mathf.Pow(T / 10f, 1f / 3f);

            // ����� ġ�� (���� d0=0.5, h0=1.0) �� d=0.5*s, r=0.25*s, h=1*s
            float r = 0.25f * s;
            float h = 1.0f * s;

            // ��ü Iy (�����, �� ���� y): Iy = (1/12) m (3 r^2 + h^2)
            float Iy_intrinsic = (1f / 12f) * mt * (3f * r * r + h * h);

            Vector3 localPos = transform.InverseTransformPoint(t.transform.position);
            _parts.Add((mt, localPos, Iy_intrinsic));
            totalMass += mt;
        }

        // 1-3) Brakes
        var brakes = GetComponentsInChildren<BrakeBehave>(includeInactive: true);
        foreach (var b in brakes)
        {
            if (b == null) continue;
            float mr = Mathf.Max(0f, brakeMass);
            float Iy_intrinsic = (1f / 12f) * mr * (brakeLength * brakeLength); // ����, �� ���� y
            Vector3 localPos = transform.InverseTransformPoint(b.transform.position);
            _parts.Add((mr, localPos, Iy_intrinsic));
            totalMass += mr;
        }

        // === 2) �����߽�(CoM) (Body ���� ����) ===
        Vector3 comLocal = Vector3.zero;
        if (totalMass > 0f)
        {
            Vector3 sum = Vector3.zero;
            foreach (var p in _parts)
                sum += p.m * p.localPos;
            comLocal = sum / totalMass;
        }

        // === 3) ���� y�� �������Ʈ Iy �ջ� (������ ����) ===
        // d_perp^2 = (x - x_c)^2 + (z - z_c)^2  (y�� �����Ÿ�)
        double Iy = 0.0;
        foreach (var p in _parts)
        {
            float dx = p.localPos.x - comLocal.x;
            float dz = p.localPos.z - comLocal.z;
            double d2 = (double)dx * dx + (double)dz * dz;
            Iy += p.Iy_intrinsic + p.m * d2;
        }

        // === 4) Rigidbody�� �ݿ� ===
        rb.mass = totalMass;
        rb.centerOfMass = comLocal;

        // inertiaTensor�� ���� �� ����. ���⼭�� y���� ���� y��� ����.
        // y�ุ ȸ�� ����Ϸ��� Ix/Iz�� ũ�� ����(�Ǵ� Constraints�� X/Z ȸ�� �����ص� ��).
        float Iy_f = (float)Iy;
        float Ix = exaggerateIxIz ? Mathf.Max(1f, Iy_f * lockMultiplier) : Iy_f;
        float Iz = exaggerateIxIz ? Mathf.Max(1f, Iy_f * lockMultiplier) : Iy_f;

        rb.inertiaTensorRotation = Quaternion.identity; // ���� �� ���� ����
        rb.inertiaTensor = new Vector3(Ix, Iy_f, Iz);
    }
}
