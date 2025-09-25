using UnityEngine;
using TMPro;

public class CollisionDetector : MonoBehaviour
{
    public bool isTriggering = true;
    public TMP_Text collisionCountText;
    [Tooltip("�浹 1ȸ ���� ��, �� �ð�(��) ������ �߰� ���踦 �����ϴ�.")]
    public float thresholdTime;

    [Tooltip("����� �浹 Ƚ��")]
    public int collisionCount;

    // ���� ���谡 ���Ǵ� �ð�(Time.time ����)
    float _nextAllowedTime = 0f;

    void OnCollisionStay(Collision collision)
    {
        // ��ٿ� ���̸� �ƹ� �͵� �������� ����
        if (Time.time < _nextAllowedTime) return;

        GameObject other = collision.gameObject;

        // 1) �浹ü�� Player �±�
        if (other.CompareTag("Player"))
        {
            CountAndCooldown();
            return;
        }

        // 2) �浹ü�� FixedJoint�� ������ �ְ�, �� ���� ����� Player
        var fj = other.GetComponent<FixedJoint>();
        if (fj != null)
        {
            var connected = fj.connectedBody;
            if (connected != null && connected.gameObject.CompareTag("Player"))
            {
                CountAndCooldown();
                return;
            }
        }

    }

    void CountAndCooldown()
    {
        if (isTriggering)
        {
            collisionCount++;
            _nextAllowedTime = Time.time + thresholdTime;
            collisionCountText.text = collisionCount.ToString();
            // Debug.Log($"Collision counted: {collisionCount}");
        }
    }
}
