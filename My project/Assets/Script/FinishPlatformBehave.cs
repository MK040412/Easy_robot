using UnityEngine;

public class FinishPlatformBehave : MonoBehaviour
{
    public TimerBehave tb;
    private ControlUnit controlUnit; // ControlUnit 참조

    private void Start()
    {
        // 씬에서 ControlUnit을 찾아서 할당합니다.
        controlUnit = FindFirstObjectByType<ControlUnit>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.GetComponent<ControlUnit>())
        {
            tb.arriveFinishPoint();
            if (controlUnit != null && controlUnit.positionTracker != null)
            {
                controlUnit.positionTracker.OnGameFinished(); // ControlUnit을 통해 OnGameFinished() 호출
            }
        }
    }
}