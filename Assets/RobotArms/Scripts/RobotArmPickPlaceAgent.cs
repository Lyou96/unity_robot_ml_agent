using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.VisualScripting;
using UnityEngine;


public class RobotArmPickPlaceAgent : Agent
{
  
   [Header("연결")]
   public RobotArmController arm;
   public Transform targetObject;
   public Transform placeTarget;
  
   [Header("보상")]
   public float grabReward = 0.5f; // 집업을 때 성공 보상
   public float placeReward = 1.0f; // placeTarget 올바르게 놓아서 받는 성공 보상
   public float dropPenalty = -0.3f; // 엉뚱한 곳에서 놓았다
   public float lostPenalty = -0.5f; // 작업 범위 밖으로 보냈다
  
  
   // 아래는 실측으로 정한 값이다. 바꾸면 팔이 물체에 닿지 못한다.
   const float GrabDistance = 0.25f;     // 이 안에서 잡기를 누르면 붙는다
   const float PlaceRadius = 0.25f;      // 이 안에 놓으면 성공
   const float RestSpeed = 0.2f;         // 이보다 느려야 "다 놓였다"
   const float MinSeparation = 0.6f;     // 물체와 놓을 자리를 띄우는 거리
   const float MaxStepReward = 0.05f;    // 한 스텝 진전 보상 상한
  
   const float WorkMin = 1.00f;
   const float WorkMax = 2.80f;
   const float WorkYaw = 115f;


   bool _holding; bool _grabbed;
  
   Transform _objectHome; // 물체가 원래 매달려 있던 부모
   int _cycles; // 이번 판에 몇 번 옮겼는가
  
   Rigidbody _objectBody;
   float _lastDistance;
   int _successCount;


   Vector3 CurrentGoal()
   {
       // 현재 object를 잡고 있지않으면 오브젝트 주소를 반환
       if(!_holding) return targetObject.position;
      
       // placeTarget 위치를 반한
       Vector3 local = arm.ToLocal(placeTarget.position);
       local.y = arm.spawnHeight; // 중요하지 않음 보정 굳이
       return arm.transform.TransformPoint(local);
   }
   Vector3 Mover => _holding ?
       targetObject.position : arm.TipPosition;


   float DistanceToGoal() =>
       Vector3.Distance(Mover, CurrentGoal());


   static float Flat(Vector3 a, Vector3 b) =>
       new Vector2(a.x - b.x, a.z - b.z).magnitude;


   bool OverPlaceTarget() =>
       Flat(arm.ToLocal(targetObject.position),
       arm.ToLocal(placeTarget.position)) < PlaceRadius;


   // PlaceTarget 도 성공하면 다른 위치 배치 되어야한다.
   void MovePlaceTarget(Vector3 objLocal)
   {
       Vector3 spot;
       int guard = 0;
      
       do { spot = RandomSpot(); }
       while (Flat(objLocal, spot) < MinSeparation && ++guard < 50);
      
       spot.y = 0.01f;     // 납작한 원판이라 테이블에 붙여 둔다
       placeTarget.position = arm.transform.TransformPoint(spot);
   }
   Vector3 RandomSpot() => arm.SpawnPointLocal(
       Random.Range(arm.spawnDistanceRange.x, arm.spawnDistanceRange.y),
       Random.Range(arm.spawnYawRange.x, arm.spawnYawRange.y));


   void UpdateGrip(bool wantGrip)
   {
       if (!_holding && wantGrip)
       {
           if (Vector3.Distance(arm.TipPosition, targetObject.position) < GrabDistance)
               Grab();
       }
       else if (_holding && !wantGrip)
       {
           Release(true);
       }
   }
   void Grab()
   {
       _holding = true;
      
       // 드는 동안은 물리를 꺼둔다.
       // 안 그러면 중력이 물체를 끌어내리고 팔이 붙잡으면서 서로 싸운다.
       _objectBody.isKinematic = true;
       targetObject.SetParent(arm.tip);


       // 이 보상이 없으면 "잡기"를 시도할 이유가 없어서 물체 근처만 맴돈다.
       // 한 사이클에 한 번만 준다. 안 그러면 잡았다 놨다만 반복한다.
       if (!_grabbed) AddReward(grabReward); // object 를 잡았어 그랩 보상 지급
       _grabbed = true;


       _lastDistance = DistanceToGoal(); // 목표가 바뀌었으니 기준을 새로 잡는다
   }


   void Release(bool penalty)
   {
       if (!_holding) return;
      
       _holding = false;
      
       targetObject.SetParent(_objectHome);
       _objectBody.isKinematic = false;
       _objectBody.velocity = Vector3.zero;
       _objectBody.angularVelocity = Vector3.zero;
      
       // 엉뚱한 곳에 놓았다. 벌점만 주고 판은 계속한다.
       // 여기서 끝내면 "놓으면 큰일 난다"만 배우고
       // "잘못 놓았으면 다시 주우면 된다"는 배우지 못한다
       if (penalty && !OverPlaceTarget()) AddReward(dropPenalty);
      
       _lastDistance = DistanceToGoal();
      
   }


   bool ObjectLost()
   {
       if(_holding) return false;
      
       Vector3 p = arm.ToLocal(targetObject.position);
       if (p.y < -0.3f) return true;   // 테이블 아래로 떨어졌다


       float d = new Vector2(p.x, p.z).magnitude;
       if (d < WorkMin || d > WorkMax) return true;


       return Mathf.Abs(Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg) > WorkYaw;
   }
  
   public override void Initialize()
   {
       _objectBody = targetObject.GetComponent<Rigidbody>();
       _objectHome = targetObject.parent;
   }


   public override void OnEpisodeBegin()
   {
       _cycles = 0;


       Release(false);
       arm.ResetPose();
      
       Vector3 spot = RandomSpot();
       targetObject.SetPositionAndRotation(
           arm.transform.TransformPoint(spot), Quaternion.identity);
       _objectBody.linearVelocity = Vector3.zero;
       _objectBody.angularVelocity = Vector3.zero;
      
       MovePlaceTarget(spot);
      
       // 옮긴 결과를 물리 엔진에 알려준다. 안 하면 질의가 옛 위치로 계산된다.
       Physics.SyncTransforms();
       ResetCycle();
   }
  
   float Distance() => Vector3.Distance(arm.TipPosition, targetObject.position);
  
   // 물체를 테이블 위 부채꼴 구역의 랜덤한 자리에 놓는다.
   void PlaceObject()
   {
       Vector3 spot = RandomSpot();
      
       targetObject.SetPositionAndRotation(
           arm.transform.TransformPoint(spot), Quaternion.identity);
      
       // 지난 판에 굴러가던 속도를 지운다.
       _objectBody.linearVelocity = Vector3.zero;
       _objectBody.angularVelocity = Vector3.zero;
   }


   public override void OnActionReceived(ActionBuffers actionBuffers)
   {
       for (int i = 0; i < arm.joints.Length; i++)
       {
           arm.Drive(i, actionBuffers.ContinuousActions[i], Time.fixedDeltaTime);
       }
      
       // 이산 액션: 잡기 / 놓기.
       // 관절 각도는 "조금 더 / 조금 덜"이 의미 있는 연속량이지만,
       // 잡기는 잡거나 안 잡거나 둘 중 하나다. 중간값이 의미가 없다.
       UpdateGrip(actionBuffers.DiscreteActions[0] == 1);
      
       GiveReward();
   }


   void GiveReward()
   {
       if(ObjectLost()) { AddReward(lostPenalty); EndEpisode(); return; }
      
      
       // 진전 보상
       float distance = Distance();
      
       // object와 TIP의 거리가 가까워질수록 보상을 많이 줄것
       AddReward((_lastDistance - distance) / arm.reach);
      
       // 시간 페널티 한 판 내내 더하면 대략 -1 이 된다.
       AddReward(-1f / MaxStep);
       _lastDistance = distance;
      
       // 놓기 성공 — 네 가지가 모두 맞아야 한다.
       //   ① 한 번이라도 들었다 ② 지금은 손을 뗐다
       //   ③ 놓을 자리 위에 있다 ④ 멈춰 있다 (떨어지는 중에 성공 처리되는 것을 막는다)
       if (_grabbed
           && !_holding
           && OverPlaceTarget()
           && _objectBody.linearVelocity.magnitude < RestSpeed)
       {
           AddReward(placeReward);
           _cycles++;
          
           // 판을 끝내지 않고 놓을 자리만 새 랜덤 자리로 옮긴다.
           MovePlaceTarget(arm.ToLocal(targetObject.position));
           ResetCycle();
       }
   }


   void ResetCycle()
   {
       _grabbed = false;
       _lastDistance = DistanceToGoal();
   }


   public override void CollectObservations(VectorSensor sensor)
   {
       // 관절의 각도 값을 센서 신호로 전송
       for (int i = 0; i < arm.joints.Length; i++)
       {
           sensor.AddObservation(arm.NormalizedAngle(i)); // 3 관절 각도
       }
       // 관절의 속도를 센서 신호로 전송
       for (int i = 0; i < arm.joints.Length; i++)
       {
           sensor.AddObservation(arm.NormalizedVelocity(i)); // 3 관절 각속도
       }
      
       sensor.AddObservation(arm.ToNormalizedLocal(arm.TipPosition)); // 3 팔 끝
       sensor.AddObservation(arm.ToNormalizedLocal(targetObject.position)); // 3 물체
      
       sensor.AddObservation(_holding ? 1f : 0f); // 1 holding 상태여부
       sensor.AddObservation(arm.ToNormalizedLocal(CurrentGoal()));  // 3 목표에 대한 정규화 주소
   }


   public override void Heuristic(in ActionBuffers actionsOut)
   {
       var continuous = actionsOut.ContinuousActions;
       for (int i = 0; i < arm.joints.Length && i < continuous.Length; i++)
           continuous[i] = RobotArmInput.Joint(i);
      
       var discrete = actionsOut.DiscreteActions;
       discrete[0] = RobotArmInput.Grip ? 1 : 0;
   }
}


