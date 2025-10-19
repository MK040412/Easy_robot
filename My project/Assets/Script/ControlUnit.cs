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
    public PositionTracker positionTracker; // PositionTracker 참조 추가

    [Header("Control Settings")]
    public float joystickCenter = 512f;
    public float joystickDeadzone = 0f; // 데드존 제거
    public float maxJoystickValue = 1023f;
    public float maxServoAngle = 180f; // ±180도 범위
    public bool usePolarControl = true;
    public bool lastButton = false;

    // 칼만 필터 인스턴스들
    private SlidingWindowKalmanFilter thrustFilter;
    private SlidingWindowKalmanFilter yawFilter;

    void Start()
    {
        rb = this.GetComponent<Rigidbody>();
        if (engine.Count == 0) engine = new List<ThrusterBehave>(GetComponentsInChildren<ThrusterBehave>());

        // UI 컴포넌트 자동 탐색
        if (timerBehave == null) timerBehave = FindFirstObjectByType<TimerBehave>();
        if (collisionDetector == null) collisionDetector = FindFirstObjectByType<CollisionDetector>();
        if (positionTracker == null) positionTracker = FindFirstObjectByType<PositionTracker>(); // PositionTracker 자동 탐색

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

        // 칼만 필터 초기화
        thrustFilter = new SlidingWindowKalmanFilter(5, 0.1f, 0.05f);
        yawFilter = new SlidingWindowKalmanFilter(5, 2.0f, 1.0f);

        // 로켣 물리 설정
        RocketMassInertiaBuilder massBuilder = GetComponent<RocketMassInertiaBuilder>();
        if (massBuilder != null)
        {
            massBuilder.exaggerateIxIz = false;
            massBuilder.Rebuild();
        }
    }

    void CheckButtonPress()
    {
        bool currentButton = D2 || D3 || D4 || D5 || D6;
        if (currentButton && !lastButton)
        {
            usePolarControl = !usePolarControl;
            Debug.Log("Polar Control Mode: " + (usePolarControl ? "Enabled" : "Disabled"));
        }
        lastButton = currentButton;
    }

    void FixedUpdate()
    {
        // Always use Polar Control for ML-Agents
        usePolarControl = true;
        HandlePolarControl();
    }

    void HandlePolarControl() 
    {
        // Left Stick (A0=Y, A1=X)로 추력 제어
        float deltaX = A1 - joystickCenter;
        float deltaY = A0 - joystickCenter;
        float distance = Mathf.Sqrt(deltaX * deltaX + deltaY * deltaY);
        
        float thrustValue = 0f;
        // 데드존 없이 직접 매핑
        thrustValue = Mathf.Clamp01(distance / joystickCenter);
        
        // 칼만 필터 적용
        thrustValue = thrustFilter.Filter(thrustValue);

        // DEBUG: Log the final thrust value
        Debug.Log($"[ControlUnit] A0: {A0}, A1: {A1} -> Thrust: {thrustValue}");
        
        foreach(var thruster in engine)
        {
            thruster.controlVal = thrustValue;
        }

        if (yawServo1 != null && yawServo2 != null)
        {
            // x, y 입력을 사용하여 시계 방향 각도 계산
        // y가 위쪽일 때 0도, 시계 방향으로 회전
        float yawAngle = Mathf.Atan2(deltaX, deltaY) * Mathf.Rad2Deg;
        
        // 칼만 필터 적용
        yawAngle = yawFilter.Filter(yawAngle);
        
        yawServo1.controlVal = yawAngle;
        yawServo2.controlVal = yawAngle;
        }
    }

    void HandleThrottle() 
    {
        // 데드존 없이 직접 매핑
        float thrustValue = Mathf.InverseLerp(joystickCenter, 0f, A0);
        
        // 칼만 필터 적용
        thrustValue = thrustFilter.Filter(thrustValue);
        
        foreach(var thruster in engine)
        {
            thruster.controlVal = Mathf.Clamp01(thrustValue);
        }
    }

    void HandleServos() 
    {
        if (yawServo1 == null || yawServo2 == null || joystickCenter == 0) return;

        // 데드존 없이 정확한 각도 매핑
        float yawAngle = MapJoystickToAngle(A3);
        
        // 칼만 필터 적용
        yawAngle = yawFilter.Filter(yawAngle);
        
        yawServo1.controlVal = yawAngle;
        yawServo2.controlVal = yawAngle;
    }

    // 조이스틱 값을 정확한 각도로 매핑하는 함수
    float MapJoystickToAngle(ushort joystickValue)
    {
        // 조이스틱 값을 -512에서 +511 범위로 변환
        float centeredValue = joystickValue - joystickCenter;
        
        // -512에서 +511 범위를 -180도에서 +180도로 정확히 매핑
        if (centeredValue >= 0)
        {
            // 0에서 +511을 0도에서 +180도로 매핑
            return (centeredValue / 511f) * 180f;
        }
        else
        {
            // -512에서 0을 -180도에서 0도로 매핑
            return (centeredValue / 512f) * 180f;
        }
    }
}

// 슬라이딩 윈도우 칼만 필터 클래스
public class SlidingWindowKalmanFilter
{
    private Queue<float> window;
    private int windowSize;
    private float measurementNoise;
    private float processNoise;
    private float filteredValue;
    private float errorCovariance;

    public SlidingWindowKalmanFilter(int windowSize, float measurementNoise, float processNoise)
    {
        this.windowSize = windowSize;
        this.measurementNoise = measurementNoise;
        this.processNoise = processNoise;
        this.window = new Queue<float>();
        this.filteredValue = 0f;
        this.errorCovariance = 1f;
    }

    public float Filter(float measurement)
    {
        // 윈도우에 새 측정값 추가
        window.Enqueue(measurement);
        if (window.Count > windowSize)
        {
            window.Dequeue();
        }

        // 윈도우가 가득 찼을 때만 필터링 적용
        if (window.Count == windowSize)
        {
            // 예측 단계
            errorCovariance += processNoise;

            // 슬라이딩 윈도우의 평균값 계산
            float sum = 0f;
            foreach (float value in window)
            {
                sum += value;
            }
            float windowAverage = sum / window.Count;

            // 측정값과 윈도우 평균의 가중 평균 사용
            float blendedMeasurement = (measurement * 0.7f + windowAverage * 0.3f);

            // 업데이트 단계
            float kalmanGain = errorCovariance / (errorCovariance + measurementNoise);
            filteredValue = filteredValue + kalmanGain * (blendedMeasurement - filteredValue);
            errorCovariance = (1 - kalmanGain) * errorCovariance;

            return filteredValue;
        }
        else
        {
            // 윈도우가 채워지기 전에는 원래 값 반환
            return measurement;
        }
    }
}