using System.Collections.Generic;
using UnityEngine;

public class BrakeManager : MonoBehaviour
{
    [Header("Source of brakes")]
    public ControlUnit cu;                        // cu.brake: List<BrakeBehave>
    [Header("Target rigidbody (rocket)")]
    public Rigidbody rb;

    [Header("Global settings")]
    public float brakeConstant = 10;             // �� �극��ũ ���� C (���� �����Ϸ��� �� BrakeBehave�� ����)
    private float stopSpeed = 0.1f;              // �� ���� �ӵ��� ������ ������ ����(������ ���� ����)
    private float safetyFactor = 0.95f;  // <= mv�� �� %������ ���� �������

    // ���� ĳ��
    private readonly List<Vector3> _forces = new();
    private readonly List<Vector3> _positions = new();

    void Reset()
    {
        // �±�/������ ������ �ִٸ� �ڵ����� ã�Ƶ� ��
        if (rb == null)
        {
            GameObject[] parts = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject part in parts)
            {
                if (part.GetComponent<ControlUnit>())
                {
                    rb = part.GetComponent<Rigidbody>();
                    break;
                }
            }
        }
    }

    void FixedUpdate()
    {
        if (rb == null || cu == null || cu.brake == null || cu.brake.Count == 0)
            return;

        _forces.Clear();
        _positions.Clear();

        // �� �극��ũ�� ���� �� ���
        foreach (var b in cu.brake)
        {
            if (b == null) continue;
            b.brakeConstant = brakeConstant;

            b.ComputeForce(rb, out var f, out var p);
            _forces.Add(f);
            _positions.Add(p);

            b.UpdateVisuals();
        }

        // �� ���޽� ��� Ŭ���� (�ӵ� ���� ���и� ����)
        float dt = Time.fixedDeltaTime;

        Vector3 totalForce = Vector3.zero;
        for (int i = 0; i < _forces.Count; i++)
            totalForce += _forces[i];

        Vector3 v = rb.linearVelocity;
        float speed = v.magnitude;

        float k = 1f; // ������ ����(<=1)
        if (speed > stopSpeed)
        {
            Vector3 vDir = v / speed;
            // �� ���޽� J = F * dt
            Vector3 J = totalForce * dt;

            // �ӵ� ���� ����(��Į��). ���� drag�� ������ ��.
            float jParallel = Vector3.Dot(J, vDir);

            // ������ ���� ����: |jParallel| > m*|v|
            float maxAbsParallel = rb.mass * speed * safetyFactor;

            // jParallel�� -maxAbsParallel���� �� �۾�����(=�ʹ� ū ����) ������ �ʿ�
            if (jParallel < -maxAbsParallel)
                k = (-maxAbsParallel) / jParallel; // jParallel<0�̹Ƿ� 0<k<1
        }
        else
        {
            // ��ǻ� ���� ����: �ʿ�� ���� ���踸 ����ϰų� ���� ����
            // ���⼭�� ������ �ӵ� ���� ����� �����ϰ�, ����(���� ��ȯ) ���и� �����ϰ� �ʹٸ�
            // k�� 1�� �ΰ� �Ʒ����� ���� ���� �����ϴ� ������ �߰��ص� ��.
            // ������ ���� ���ϰ�:
            k = 0.5f; // or 0f to fully stop applying when near zero
        }

        // ���� ������ ��� ���� ������ ��, �ش� ������ ���� �� ��ũ�� ���� ������ �ڿ����� ������
        if (k < 1f)
        {
            for (int i = 0; i < _forces.Count; i++)
                _forces[i] *= k;
        }

        for (int i = 0; i < _forces.Count; i++)
        {
            rb.AddForceAtPosition(_forces[i] * Time.fixedDeltaTime, _positions[i], ForceMode.Impulse);
            //Debug.Log(_forces[i].magnitude + ", " + _positions[i].x + ", " + _positions[i].z);
            //Debug.Log(rb.linearVelocity.x + ", " + rb.linearVelocity.z);
        }
    }
}
