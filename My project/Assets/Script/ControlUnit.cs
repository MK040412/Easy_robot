using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class ControlUnit : MonoBehaviour
{
    [Header("Raw ADC (0..1023)")]
    public ushort A0;    // Left Stick Y (Throttle)
    public ushort A1;    // Left Stick X
    public ushort A2;    // Right Stick X (Yaw)
    public ushort A3;    // Right Stick Y (Pitch)
    public ushort A4, A5; // Potentiometers

    [Header("Buttons (pressed = true)")]
    public bool D2, D3, D4, D5, D6;

    [Header("Robot Actuators - Assign in Inspector")]
    public Rigidbody rb;
    public List<ThrusterBehave> engine;
    public List<BrakeBehave> brake;
    
    public ServoBehave yawServo1;
    public ServoBehave yawServo2;

    [Header("UI Components - Assign in Inspector")]
    public TimerBehave timerBehave;
    public CollisionDetector collisionDetector;

    [Header("Control Settings")]
    public float joystickCenter = 512f;
    public float joystickDeadzone = 50f;
    public float maxServoAngle = 45f; // -90도에서 +90도까지, 총 180도 범위를 가집니다.

    void Start()
    {
        rb = this.GetComponent<Rigidbody>();
        if (engine.Count == 0) engine = new List<ThrusterBehave>(GetComponentsInChildren<ThrusterBehave>());

        // UI 컴포넌트 자동 탐색
        if (timerBehave == null) timerBehave = FindObjectOfType<TimerBehave>();
        if (collisionDetector == null) collisionDetector = FindObjectOfType<CollisionDetector>();

        // 서보가 할당되었는지 확인하고, ServoBehave 스크립트를 활성화합니다.
        if ( yawServo1 == null || yawServo2 == null)
        {
            Debug.LogError("ControlUnit 인스펙터에서 Pitch와 Yaw 서보를 할당해야 합니다!");
        }
        else
        {
            // ServoBehave 스크립트를 사용해야 하므로, 활성화 상태를 보장합니다.
            yawServo1.enabled = true;
            yawServo2.enabled = true;
        }

        // 로켣 물리 설정
        RocketMassInertiaBuilder massBuilder = GetComponent<RocketMassInertiaBuilder>();
        if (massBuilder != null)
        {
            massBuilder.exaggerateIxIz = false;
            massBuilder.Rebuild();
        }
    }

    void FixedUpdate()
    {
        HandleThrottle();
        HandleServos();
    }

    void HandleThrottle()
    {
        float thrustValue = 0f;
        if (A0 < joystickCenter - joystickDeadzone)
        {
            thrustValue = Mathf.InverseLerp(joystickCenter - joystickDeadzone, 0f, A0);
        }
        
        foreach(var thruster in engine)
        {
            thruster.controlVal = Mathf.Clamp01(thrustValue);
        }
    }

    void HandleServos()
    {
        if ( yawServo1 == null || yawServo2 == null || joystickCenter == 0) return;

        // 1. 오른쪽 조이스틱 입력을 -1 ~ 1 범위로 정규화
        float pitchInput = (A2 - joystickCenter) / joystickCenter;
        float yawInput = (A3 - joystickCenter) / joystickCenter;

        // 2. 데드존 적용
        float deadzoneNormalized = joystickDeadzone / joystickCenter;
        if (Mathf.Abs(pitchInput) < deadzoneNormalized) pitchInput = 0f;
        if (Mathf.Abs(yawInput) < deadzoneNormalized) yawInput = 0f;

        // 3. 목표 각도를 계산하여 각 서보의 controlVal에 전달
        // ServoBehave 스크립트가 이 값을 읽어 서보를 회전시킬 것입니다.
        yawServo1.controlVal = yawInput * maxServoAngle;
        yawServo2.controlVal = -yawInput * maxServoAngle;
    }
}