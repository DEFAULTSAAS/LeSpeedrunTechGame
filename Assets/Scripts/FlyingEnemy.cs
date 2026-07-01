using System;
using UnityEngine;

public interface IEnemy
{
    public bool IsAttacking { get; }
    public bool IsPursuing { get; }

    public float CurrAttackDelay { get; set; }
    public float CurrAttackAcc { get; set; }
    public float CurrOutOfRangeAcc { get; set; } 
    
    public float AttackDamage { get; }
    public float MaxAttackRange { get; }
    public float StopSeekingTime { get; }
    public Vector2 MinMaxAttackDelay { get; }
    public Vector2 MinMaxDetectionRange { get; }

    public bool TickAttackLogic(float inDT, float inTargetDist)
    {
        bool isAttacking = IsAttacking;
        if (!isAttacking)
        {
            if (inTargetDist < MaxAttackRange)
            CurrAttackAcc += inDT;

            if (CurrAttackAcc >= CurrAttackDelay)
            {
                Attack();
                isAttacking = true;
                CurrAttackDelay = UnityEngine.Random.Range(MinMaxAttackDelay.x, MinMaxAttackDelay.y);
                CurrAttackAcc = 0.0f;   
            }   
        }

        return isAttacking;
    }
    public bool TickSeekLogic(float inDT, float inTargetDist)
    {
        if (inTargetDist <= MinMaxDetectionRange.x)
        {
            CurrOutOfRangeAcc = 0.0f;
            return true;
        }

        if (inTargetDist > MinMaxDetectionRange.y)
            CurrOutOfRangeAcc += inDT;
        else if (CurrOutOfRangeAcc > 0.0f)
        {
            CurrOutOfRangeAcc -= inDT;
            CurrOutOfRangeAcc = Mathf.Max(CurrOutOfRangeAcc, 0.0f);   
        }

        if (CurrOutOfRangeAcc >= StopSeekingTime)
            return false;

        return true;
    }
    public float Attack();
}

public class FlyingEnemy : MonoBehaviour, IEnemy
{
    public bool IsAttacking { get; private set;}
    public bool IsPursuing { get; private set; }

    public float CurrAttackDelay {get; set;}
    public float CurrAttackAcc {get; set;}
    public float CurrOutOfRangeAcc {get; set;}

    [field : SerializeField] public float AttackDamage { get; private set; }
    [field : SerializeField] public float MaxAttackRange { get; private set; }
    [field : SerializeField] public float StopSeekingTime { get; private set; }
    [field : SerializeField] public Vector2 MinMaxAttackDelay { get; private set; }
    [field : SerializeField] public Vector2 MinMaxDetectionRange { get; private set; }

    public float OrbitHeight = 5.0f;
    public float OrbitRadius = 5.0f;
    public float OrbitSpeed = 5.0f;
    
    private PlayerController _target;
    private Quaternion _currOrbitOrientation;
    private Vector3 _targetPos;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _target = FindFirstObjectByType<PlayerController>();
    }

    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;
        if (!_target)
        {
            _target = FindFirstObjectByType<PlayerController>();
            return;   
        }

        _currOrbitOrientation = Quaternion.AngleAxis(5.0f * dt, Vector3.up) * _currOrbitOrientation;
        if (!IsAttacking)
        {
            _targetPos = _target.transform.position + 
                         Vector3.up * OrbitHeight + 
                         _currOrbitOrientation * Vector3.forward * OrbitRadius;   
        }
    }

    public float Attack()
    {
        return 0.0f;
    }
}
