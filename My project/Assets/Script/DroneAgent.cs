
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class DroneAgent : Agent
{
    public Transform target;
    public float moveSpeed = 2f;
    private Rigidbody rBody;
    private ControlUnit controlUnit;
    private Vector3 startPosition;

    public override void Initialize()
    {
        rBody = GetComponent<Rigidbody>();
        controlUnit = GetComponent<ControlUnit>();
        startPosition = transform.position;
    }

    public override void OnEpisodeBegin()
    {
        // Reset drone's state
        rBody.linearVelocity = Vector3.zero;
        rBody.angularVelocity = Vector3.zero;
        transform.position = startPosition;
        transform.rotation = Quaternion.identity;

        // Reset target's position
        target.position = startPosition + new Vector3(Random.Range(-4, 4), Random.Range(1, 5), Random.Range(-4, 4));
        
        // Reset ControlUnit inputs
        if (controlUnit != null)
        {
            controlUnit.A0 = 512;
            controlUnit.A1 = 512;
            controlUnit.A2 = 512;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Target and Agent positions
        sensor.AddObservation(target.position - transform.position); // 3 floats
        sensor.AddObservation(rBody.linearVelocity); // 3 floats
        sensor.AddObservation(transform.up); // 3 floats - orientation
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Actions (size = 3)
        var continuousActions = actions.ContinuousActions;
        
        // Map actions from [-1, 1] to [0, 1023] for ControlUnit
        controlUnit.A0 = (ushort)((continuousActions[0] + 1f) / 2f * 1023f);
        controlUnit.A1 = (ushort)((continuousActions[1] + 1f) / 2f * 1023f);
        controlUnit.A2 = (ushort)((continuousActions[2] + 1f) / 2f * 1023f);

        // Rewards
        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        // Reached target
        if (distanceToTarget < 1.42f)
        {
            SetReward(1.0f);
            EndEpisode();
        }

        // Fell down or went too far
        if (transform.position.y < -1.0f || Vector3.Distance(transform.position, startPosition) > 15f)
        {
            SetReward(-1.0f);
            EndEpisode();
        }
        
        // Encourage getting closer
        AddReward(-distanceToTarget * 0.001f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = 0f;
        continuousActionsOut[1] = 0f;
        continuousActionsOut[2] = 0f;

        // Vertical (Thrust) & Forward/Backward
        if (Input.GetKey(KeyCode.W)) continuousActionsOut[0] = 1f;
        if (Input.GetKey(KeyCode.S)) continuousActionsOut[0] = -1f;

        // Left/Right
        if (Input.GetKey(KeyCode.A)) continuousActionsOut[1] = -1f;
        if (Input.GetKey(KeyCode.D)) continuousActionsOut[1] = 1f;
        
        // Yaw
        if (Input.GetKey(KeyCode.Q)) continuousActionsOut[2] = -1f;
        if (Input.GetKey(KeyCode.E)) continuousActionsOut[2] = 1f;
    }
}