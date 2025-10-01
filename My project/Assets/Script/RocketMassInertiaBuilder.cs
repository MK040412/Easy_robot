using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RocketMassInertiaBuilder : MonoBehaviour
{
    [Header("References")]
    public Rigidbody rb;

    [Header("Body/Brake Base Settings")]
    public float bodyMass = 10f;
    public float bodyLength = 2f;
    public float brakeMass = 0.1f;
    public float brakeLength = 1f;

    [Header("Thruster Mass Rule")]
    public float thrustMassDivisor = 10f; 

    [Header("Axis Control")]
    public bool exaggerateIxIz = true;
    public float lockMultiplier = 1000f;

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

    [ContextMenu("Rebuild Mass/CoM/Iy")]
    public void Rebuild()
    {
        _parts.Clear();
        if (rb == null) { Debug.LogError("[RocketMassInertiaBuilder] Rigidbody not set."); return; }

        float totalMass = 0f;

        {
            float m = Mathf.Max(0f, bodyMass);
            Vector3 localPos = Vector3.zero;
            float Iy_intrinsic = (1f / 12f) * m * (bodyLength * bodyLength);
            _parts.Add((m, localPos, Iy_intrinsic));
            totalMass += m;
        }

        var thrusters = GetComponentsInChildren<ThrusterBehave>(includeInactive: true);
        foreach (var t in thrusters)
        {
            if (t == null) continue;
            float T = Mathf.Max(0f, t.maxThrust);
            float mt = T / thrustMassDivisor;

            float s = Mathf.Pow(T / 10f, 1f / 3f);

            float r = 0.25f * s;
            float h = 1.0f * s;

            float Iy_intrinsic = (1f / 12f) * mt * (3f * r * r + h * h);

            Vector3 localPos = transform.InverseTransformPoint(t.transform.position);
            _parts.Add((mt, localPos, Iy_intrinsic));
            totalMass += mt;
        }

        var brakes = GetComponentsInChildren<BrakeBehave>(includeInactive: true);
        foreach (var b in brakes)
        {
            if (b == null) continue;
            float mr = Mathf.Max(0f, brakeMass);
            float Iy_intrinsic = (1f / 12f) * mr * (brakeLength * brakeLength);
            Vector3 localPos = transform.InverseTransformPoint(b.transform.position);
            _parts.Add((mr, localPos, Iy_intrinsic));
            totalMass += mr;
        }

        Vector3 comLocal = Vector3.zero;
        if (totalMass > 0f)
        {
            Vector3 sum = Vector3.zero;
            foreach (var p in _parts)
                sum += p.m * p.localPos;
            comLocal = sum / totalMass;
        }

        double Iy = 0.0;
        foreach (var p in _parts)
        {
            float dx = p.localPos.x - comLocal.x;
            float dz = p.localPos.z - comLocal.z;
            double d2 = (double)dx * dx + (double)dz * dz;
            Iy += p.Iy_intrinsic + p.m * d2;
        }

        rb.mass = totalMass;
        rb.centerOfMass = comLocal;

        float Iy_f = (float)Iy;
        float Ix = exaggerateIxIz ? Mathf.Max(1f, Iy_f * lockMultiplier) : Iy_f;
        float Iz = exaggerateIxIz ? Mathf.Max(1f, Iy_f * lockMultiplier) : Iy_f;

        rb.inertiaTensorRotation = Quaternion.identity;
        rb.inertiaTensor = new Vector3(Ix, Iy_f, Iz);
    }
}
