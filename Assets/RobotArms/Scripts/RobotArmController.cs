using System.Collections.Generic;
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
        joints[index].Drive(normalizeSpeed, deltaTime);
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
    
    // 물체 관통 예방
    public bool IsBlocked()
    {
        return false;
    }
    
}