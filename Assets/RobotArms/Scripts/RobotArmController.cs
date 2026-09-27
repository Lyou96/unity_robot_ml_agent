using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;

public class RobotArmController : MonoBehaviour
{
    [System.Serializable]
    public class joint
    {
        public string label = "관절"; // 해당 객체 이름
        public Transform pivot; // 회전을 담당하는 오브젝트
        public Vector3 axis = Vector3.left; //  회전축, 회전방향
        
        public float minAngle = -90f;
        public float maxAngle = 90f;
        public float maxSpeed = 120f;
        public float startAngle = 0f;
        
        
        public float Angle {get; private set;} //지금 각도
        public float Velocity {get; private set;} //지금 각속도

        public void Drive(float normalizeSpeed, float deltaTime)
        {
            float beforeAngle = Angle;
            float deltaAngle = Mathf.Clamp(normalizeSpeed, min: -1f, max: 1f) * maxSpeed * deltaTime;
            
            Angle = Mathf.Clamp(Angle + deltaAngle, minAngle, maxAngle);
            Velocity = deltaTime > 0f ? (Angle - beforeAngle) / deltaTime : 0f;

            Apply();
        }

        public void ResetTo(float angle)
        {
            Angle = Mathf.Clamp(angle, minAngle, maxAngle);
            Velocity = 0f;
            Apply();
        }
        
        void Apply()  // 해당 모터에 실제로 회전값을 적용시키는 함수
        {
            if(pivot != null)
                pivot.localRotation = Quaternion.AngleAxis(Angle, axis);
        }
        
        
    }

    public joint[] joints = new joint[0];

    void Awake()
    {
        ResetPose();
    }

    public void ResetPose()
    {
        foreach (var j in joints)
        {
            j.ResetTo(j.startAngle);
        }
    }

    public void Drive(int index, float normalizeSpeed, float deltaTime)
    {
        float beforeAngle = joints[index].Angle;
        
        joints[index].Drive(normalizeSpeed, deltaTime);
        if (IsBlocked())
        {
            joints[index].ResetTo(beforeAngle);
        }
    }
    
    
    
    
    void FixedUpdate()
    {
        if (RobotArmInput.ResetPressed) ResetPose();

        for (int i = 0; i < joints.Length; i++)
        {
            Drive(i, RobotArmInput.Joint(i), Time.fixedDeltaTime);
        }
    }

    // 물체를 잡는 지점
    public Transform tip;

    public float floorHeight = 0f;


    Transform[] _chain;
    Transform[] Chain
    {
        get
        {
            if (_chain != null && _chain.Length > 0) return _chain;

            var list = new List<Transform>();
            for (var t = tip; t != null && t != transform; t = t.parent)
                list.Add(t);
            list.Reverse();
            return _chain = list.ToArray();
        }
    }
    
    
    
    // 바닥에서 약간 띄우기
    public float clearance = 0.07f;
    public float tipExemptDistance = 0.28f;
    public float obstacleRadius = 0.16f;
    public Transform[] obstacles = new Transform[0];

    
    public Vector3 TipPosition =>
        tip !=  null ? tip.position : transform.position;
    
    
    // 물체 관통 예방
    public bool IsBlocked()
    {
        var chain  = Chain;
        float minY = floorHeight + clearance;
        Vector3 tipPos = TipPosition;

        
        // 링크 하나 몇점으로 쪼개서 계산할지
        int samplesPerLink = 6;
        
        
        for (int seg = 0; seg < chain.Length - 1; seg++)
        {
            Vector3 a = chain[seg].position;
            Vector3 b = chain[seg + 1].position;

            for (int s = 0; s <= samplesPerLink; s++)
            {
                Vector3 p = Vector3.Lerp(a, b, s/(float)samplesPerLink);
                
                //바닥 검사
                if (ToLocal(p).y < minY) return true;

                
                
                if (Vector3.Distance(p, tipPos) <= tipExemptDistance) continue;

                foreach (var o in obstacles)
                {
                    if (o == null) continue;
                    if (Vector3.Distance(p, o.position) < obstacleRadius) return true;
                }
            }
        }
        return false;
    }
    public Vector3 ToLocal(Vector3 worldPosition) =>
        transform.InverseTransformPoint(worldPosition);
    
    // 받침대에서 멀어진 거리
    public Vector2 spawnDistanceRange = new Vector2(1.2f, 2.4f);
    public Vector2 spawnYawRange = new Vector2(0f, 0f);
    public float spawnHeight = 0.1f;

    public Vector3 SpawnPointLocal(float distance, float yawDegrees)
    {
        float yaw = yawDegrees * Mathf.Deg2Rad;
        return new Vector3(
            Mathf.Sin(yaw) * distance,
            spawnHeight,
            Mathf.Cos(yaw) * distance);
    }


    public float NormalizedAngle(int i)
    {
        return joints[i].Angle / 180f;
    }

    public float NormalizedVelocity(int i)
    {
        return joints[i].Velocity / joints[i].maxSpeed;
    }
    public float reach = 2.7f;
    public Vector3 ToNormalizedLocal(Vector3 worldPosition)
    {
        return ToLocal(worldPosition) / reach;
    }
    
    
    
}