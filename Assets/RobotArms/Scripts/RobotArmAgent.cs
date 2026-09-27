using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.VisualScripting;
using UnityEngine;

public class RobotArmAgent : Agent
{
    [Header("연결")]
    public RobotArmController arm;
    public Transform targetObject;
    [Header("보상")]
    public float successReward = 1.0f;

    Rigidbody _objectBody;
    float _lastDistance;
    int _successCount;

    public override void Initialize()
    {
        _objectBody = targetObject.GetComponent<Rigidbody>();
        
    }

    public override void OnEpisodeBegin()
    {
        arm.ResetPose();
        PlaceObject();

        _lastDistance = Distance();
    }

    float Distance() => Vector3.Distance(arm.TipPosition, targetObject.position);
    
    
    
    void PlaceObject()
    {
        Vector3 spot = arm.SpawnPointLocal(
            Random.Range(arm.spawnDistanceRange.x, arm.spawnDistanceRange.y),
            Random.Range(arm.spawnYawRange.x, arm.spawnYawRange.y)
        );
        targetObject.SetPositionAndRotation(
            arm.transform.TransformPoint(spot), Quaternion.identity);
        
        _objectBody.linearVelocity = Vector3.zero;
        _objectBody.angularVelocity = Vector3.zero;
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        for (int i = 0; i < arm.joints.Length; i++)
        {
            arm.Drive(i, actionBuffers.ContinuousActions[i], Time.fixedDeltaTime);
        }
        
        GiveReward();
        
    }
    void GiveReward()
    {
        float distance = Distance();
        
        // objects와 tip의 위치가 가까워질수록 보상을 많이
        AddReward((_lastDistance - distance) / arm.reach);

        AddReward(-1f / MaxStep);
        
        
        
        _lastDistance = distance;
        
        const float SuccessDistance = 0.25f;
        if (distance < SuccessDistance)
        {
            AddReward(successReward);
            _successCount++;
            EndEpisode();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 관절의 각도 값을 센서 신호로 보내는 것
        for (int i = 0; i < arm.joints.Length; i++)
        {
            sensor.AddObservation(arm.NormalizedAngle(i));
        }
        // 관절의 속도 값을 센서 신호로 전송
        for (int i = 0; i < arm.joints.Length; i++)
        {
            sensor.AddObservation(arm.NormalizedVelocity(i));
        }

        sensor.AddObservation(arm.ToNormalizedLocal(arm.TipPosition));
        sensor.AddObservation(arm.ToNormalizedLocal(targetObject.position));
    }
    
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        
    }


    
    
    
    
}
