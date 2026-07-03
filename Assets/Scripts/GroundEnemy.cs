using System;
using UnityEngine;
using UnityEngine.AI;

public enum GroundEnemyTypes
{
    Melee,
    Shooter
}

public class GroundEnemy : MonoBehaviour, IEnemy
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
    public Vector3 SpawnPos { get; private set; }

    public float MoveSpeed = 5.0f;
    public float JumpHeight = 2.0f;
    public float AttackDuration = 0.5f;
    public float MeleeDistance = 1.5f;
    public float DefendChance = 0.4f;
    public GroundEnemyTypes GroundEnemyType;
    public NavMeshAgent _navMeshAgent;
    public GameObject ProjectilePrefab;

    private IEnemy _enemy;
    private PlayerController _target;
    private float _currAttackTime = 0.0f;
    private bool IsDefending = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (!_navMeshAgent)
            _navMeshAgent = GetComponent<NavMeshAgent>();
        if (!_navMeshAgent)
            throw new NullReferenceException("Could not get nav mesh agent for ground enemy.");

        _target = FindFirstObjectByType<PlayerController>();
        _enemy = this;

        _navMeshAgent.speed = MoveSpeed;
        _navMeshAgent.height = JumpHeight;
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

        _currAttackTime += dt;
        if (IsAttacking && _currAttackTime > AttackDuration)
        {
            IsAttacking = false;
            IsDefending = false;
        }

        if (!IsAttacking && IsPursuing)
            _navMeshAgent.destination = _target.transform.position;
        else if (IsAttacking)
            _navMeshAgent.destination = transform.position;
        else if (!IsPursuing)
            _navMeshAgent.destination = SpawnPos;

        float distanceToTarget = Vector3.Distance(transform.position, _target.transform.position);
        IsAttacking = _enemy.TickAttackLogic(dt, distanceToTarget).Item1;
        IsPursuing = _enemy.TickSeekLogic(dt, distanceToTarget);
    }

    public Tuple<bool, float> Attack()
    {
        float distanceToTarget = Vector3.Distance(transform.position, _target.transform.position);
        if (GroundEnemyType == GroundEnemyTypes.Shooter && distanceToTarget > MeleeDistance)
        {
            float randomNum = UnityEngine.Random.Range(0.0f, 1.0f);
            if (randomNum <= DefendChance)
            {
                IsDefending = true;
                Debug.Log("Defending");   
            }
            else
            {
                Vector3 dirToTarget = (_target.transform.position - transform.position).normalized;
                GameObject projectileGameObj = Instantiate(ProjectilePrefab, 
                                                       transform.position, 
                                                       Quaternion.FromToRotation(Vector3.forward, 
                                                       dirToTarget));
                Projectile projectile = projectileGameObj.GetComponent<Projectile>();
                projectile.TrajectoryPos = transform.position;
                projectile.TrajectoryDir = dirToTarget;

                Debug.Log("Shooting");
            }
        }
        else if (GroundEnemyType == GroundEnemyTypes.Shooter && distanceToTarget < MeleeDistance)
        {
            Debug.Log("Melee");
        }

        _currAttackTime = 0.0f;
        return new (true, 0.0f);
    }
}
