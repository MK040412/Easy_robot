using UnityEngine;

public class ThrusterBehave : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("0~1 ���� �Է�")]
    public float controlVal; // 0 ~ 1

    [Header("Thrust")]
    [Tooltip("Inspector���� ���� �Է��ϸ� �ڵ����� 5~60���� Ŭ���εǰ�, ũ��(scale)�� (maxThrust/10)^(1/3)�� �������ϴ�.")]
    public float maxThrust = 10f; // Inspector���� ����

    [Header("Internal (do not change)")]
    [SerializeField, Tooltip("Dead zone for visuals only")]
    private float deadZone = 0.1f; // do not change
    private Rigidbody rb;
    private ParticleSystem ps;
    [SerializeField] private float maxEmit = 100f; // do not change

    void Start()
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
        ps = GetComponentInChildren<ParticleSystem>();

        // ��Ÿ�� ���� �ÿ��� �ѹ� �� ����
        ClampThrustAndApplyScale();
    }

    void FixedUpdate()
    {
        controlVal = Mathf.Clamp01(controlVal);

        Vector3 worldDir = transform.TransformDirection(Vector3.right);
        rb.AddForceAtPosition(worldDir * maxThrust * controlVal, transform.position, ForceMode.Force);

        Visuals();
    }

    void Visuals()
    {
        if (ps == null) return;
        var emission = ps.emission;
        emission.rateOverTime = (controlVal < deadZone) ? 0f : maxEmit * controlVal;
    }

#if UNITY_EDITOR
    // Inspector���� ���� �ٲ� ������ ȣ�� (Play/Editor �� ��)
    void OnValidate()
    {
        ClampThrustAndApplyScale();
    }
#endif

    void ClampThrustAndApplyScale()
    {
        // 1) 5~60���� �ڵ� Ŭ����
        maxThrust = Mathf.Clamp(maxThrust, 0f, 60f);

        // 2) ������ = (maxThrust / 10)^(1/3)
        //    10�� �������� 10�� �� ������ 1, 80�̸� 2�� �ǵ���(���� ~ �߷� ��� ���� �� �ڿ������� ť���Ʈ �����ϸ�)
        float s = Mathf.Pow(maxThrust / 10f, 1f / 3f);
        transform.localScale = new Vector3(s, s, s);
        var temp = ps.main;
        temp.startSize = s;
        temp.startSpeed = s * -10f;
    }
}
