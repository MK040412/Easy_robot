using UnityEngine;

public class ThrusterBehave : MonoBehaviour
{
    [Header("Input")]
    public float controlVal; // 0 ~ 1

    [Header("Thrust")]
    public float maxThrust = 10f; 

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
    void OnValidate()
    {
        ClampThrustAndApplyScale();
    }
#endif

    void ClampThrustAndApplyScale()
    {
        maxThrust = Mathf.Clamp(maxThrust, 0f, 60f);

        float s = Mathf.Pow(maxThrust / 10f, 1f / 3f);
        transform.localScale = new Vector3(s, s, s);
        ps = GetComponentInChildren<ParticleSystem>();
        var temp = ps.main;
        temp.startSize = s;
        temp.startSpeed = s * -10f;
    }
}
