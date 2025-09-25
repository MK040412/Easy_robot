using UnityEngine;

public class ServoBehave : MonoBehaviour
{
    [Tooltip("��ǥ ����(0 �̻� 360 �̸�, degrees)")]
    [Range(0f, 360f)]
    public float controlVal; // 0(inclusive) - 360(exclusive)

    private float maxAngularSpeed = 600f; // do not change

    private Transform childTf;

    void Start()
    {
        // ù ��° �ڽ��� ��� (�ʿ�� ���� �Ҵ��ϵ��� �ٲ㵵 ��)
        if (transform.childCount > 0)
            childTf = transform.GetChild(0);
        else
            Debug.LogWarning("[ServoBehave] �ڽ��� �����ϴ�. ȸ�� ����� �ʿ��մϴ�.");
    }

    void FixedUpdate()
    {
        if (childTf == null) return;

        // ��ǥ ������ 0~360 ������ ����
        float target = Mathf.Repeat(controlVal, 360f);

        // ���� ���� Z ����(0~360)
        float current = childTf.localEulerAngles.y;

        // �ִ� ��� ���� ���� ����(-180 ~ +180)
        float delta = Mathf.DeltaAngle(current, target);

        // �̹� �����ӿ� ���Ǵ� �ִ� ȸ����
        float maxStep = maxAngularSpeed * Time.fixedDeltaTime;

        float newY;
        if (Mathf.Abs(delta) <= maxStep)
        {
            // ��ǥ�� ����� ������ ����
            newY = target;
        }
        else
        {
            // �ִ� �������� maxStep��ŭ ȸ��
            newY = current + Mathf.Sign(delta) * maxStep;
        }

        // X/Y�� �����ϰ� Z�� ����
        Vector3 e = childTf.localEulerAngles;
        childTf.localEulerAngles = new Vector3(e.x, newY, e.z);
    }
}
