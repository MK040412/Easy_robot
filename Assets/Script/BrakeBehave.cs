using UnityEngine;

public class BrakeBehave : MonoBehaviour
{
    [Range(0f, 1f)] public float controlVal; // 0 ~ 1
    [HideInInspector] public float brakeConstant;
    [HideInInspector] private float brakeAngle = 90f;

    private GameObject brakeUp;
    private GameObject brakeDown;
    private Quaternion upInitLocalRot;
    private Quaternion downInitLocalRot;
z
    void Awake()
    {
        brakeUp = transform.GetChild(1).gameObject;
        brakeDown = transform.GetChild(0).gameObject;
        upInitLocalRot = brakeUp.transform.localRotation;
        downInitLocalRot = brakeDown.transform.localRotation;
    }

    /// <summary>
    /// ?˜„?¬ Rigidbody ?ƒ?ƒœ?—?„œ ?´ ë¸Œë ˆ?´?¬ê°? ? œ?•ˆ?•˜?Š” ?˜(ë¡œì»¬ X, Y ?„±ë¶? drag)?„ ê³„ì‚°.
    /// ?‹¤? œ ? ?š©??? ë§¤ë‹ˆ????—?„œ ?¼ê´? ?ˆ˜?–‰?•œ?‹¤.
    /// </summary>
    public void ComputeForce(Rigidbody rb, out Vector3 force, out Vector3 atPos)
    {
        controlVal = Mathf.Clamp01(controlVal);

        atPos = transform.position;

        // ?´ ì§?? ?—?„œ?˜ ?†?„(?„ ?†?„ + ?šŒ? „?— ?˜?•œ ? ‘?„ ?†?„ ?¬?•¨)
        Vector3 v = rb.GetPointVelocity(atPos);

        // ë¸Œë ˆ?´?¬ ë¡œì»¬ ì¶?
        Vector3 axisX = transform.right;  // ? œ?–´?˜• ?“œ?˜ê·?
        Vector3 axisY = transform.up;     // ?•­?ƒ ìµœë?? ?“œ?˜ê·?

        float vX = Vector3.Dot(v, axisX);
        float vY = Vector3.Dot(v, axisY);

        // ? œê³±í˜• ????•­: F = -C * v * |v| * axis
        Vector3 Fx = -brakeConstant * vX * Mathf.Abs(vX) * axisX * controlVal;
        Vector3 Fy = -brakeConstant * vY * Mathf.Abs(vY) * axisY;

        force = Fx + Fy;
    }

    /// <summary> ?”Œ?© ë¹„ì£¼?–¼(?›?•˜ë©? ë§¤ë‹ˆ????—?„œ ë§? ?”„? ˆ?„ ?˜¸ì¶?) </summary>
    public void UpdateVisuals()
    {
        float ang = brakeAngle * controlVal;
        brakeUp.transform.localRotation = upInitLocalRot * Quaternion.AngleAxis(ang, Vector3.forward);
        brakeDown.transform.localRotation = downInitLocalRot * Quaternion.AngleAxis(-ang, Vector3.forward);
    }
}
