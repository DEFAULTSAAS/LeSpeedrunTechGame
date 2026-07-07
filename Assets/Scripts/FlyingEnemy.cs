using System;
using UnityEngine;

public interface IEnemy
{
    public bool IsAttacking { get; }
    public bool IsPursuing { get; }

    public float CurrHealth {get; set;}
    public float CurrAttackDelay { get; set; }
    public float CurrAttackAcc { get; set; }
    public float CurrOutOfRangeAcc { get; set; } 
    public EnemySpawner CurrEnemySpawner { get; set; }

    public float Health {get;}
    public float AttackDamage { get; }
    public float MaxAttackRange { get; }
    public float StopSeekingTime { get; }
    public Vector2 MinMaxAttackDelay { get; }
    public Vector2 MinMaxDetectionRange { get; }
    public Vector3 SpawnPos { get; }
    public GameObject DamageOutline { get; }
    public GameObject DestructionEffect { get; }

    public Tuple<bool, float> TickAttackLogic(float inDT, float inTargetDist, bool inIsAttacking = true)
    {
        bool isAttacking = IsAttacking;
        float damage = 0.0f;

        if (!isAttacking)
        {
            if (inTargetDist < MaxAttackRange)
                CurrAttackAcc += inDT;

            if (CurrAttackAcc > CurrAttackDelay)
            {
                var result = Attack();
                isAttacking = result.Item1;
                damage = result.Item2;

                CurrAttackDelay = UnityEngine.Random.Range(MinMaxAttackDelay.x, MinMaxAttackDelay.y);
                CurrAttackAcc = 0.0f;   
            }   
        }

        return new (isAttacking, damage);
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
    public Tuple<bool, float> Attack();
    public float DamageEnemy(float inDamage)
    {
        CurrHealth -= inDamage;
        return inDamage;
    }
}

[Serializable]
public enum FlyingEnemyTypes
{
    Melee,
    Gunner
}

public class FlyingEnemy : MonoBehaviour, IEnemy
{
    public bool IsAttacking { get; private set;}
    public bool IsPursuing { get; private set; }

    public float CurrHealth {get; set;}
    public float CurrAttackDelay {get; set;}
    public float CurrAttackAcc {get; set;}
    public float CurrOutOfRangeAcc {get; set;}
    [field : SerializeField] public EnemySpawner CurrEnemySpawner { get; set; }

    [field : SerializeField] public float Health { get; private set; }
    [field : SerializeField] public float AttackDamage { get; private set; }
    [field : SerializeField] public float MaxAttackRange { get; private set; }
    [field : SerializeField] public float StopSeekingTime { get; private set; }
    [field : SerializeField] public Vector2 MinMaxAttackDelay { get; private set; }
    [field : SerializeField] public Vector2 MinMaxDetectionRange { get; private set; }
    [field : SerializeField] public GameObject DamageOutline { get; private set; }
    [field : SerializeField] public GameObject DestructionEffect { get; private set; }
    public Vector3 SpawnPos { get; private set; }

    public GameObject ProjectilePrefab;
    public GameObject MeleeWeapon;
    public FlyingEnemyTypes FlyingEnemyType;
    public AudioClip DeathAudioClip;
    public AudioClip ShootAudioClip;
    public AudioClip MeleeAudioClip;
    public AudioClip HurtAudioClip;
    
    public float OrbitHeight = 5.0f;
    public float OrbitRadius = 5.0f;
    public float OrbitSpeed = 90.0f;
    public float MoveSpeed = 8.0f;

    private IEnemy _enemy;
    private AudioSource _enemyAudioSource;

    private PlayerController _target;
    private Quaternion _currOrbitOrientation = Quaternion.identity;
    private Vector3 _targetPos;
    private bool _justAttacked = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _enemyAudioSource = GetComponent<AudioSource>();
        _target = FindFirstObjectByType<PlayerController>();
        _enemy = this;
        CurrHealth = Health;
        SpawnPos = transform.position;
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
        _currOrbitOrientation = Quaternion.AngleAxis(OrbitSpeed * dt, Vector3.up) * _currOrbitOrientation;

        if (!IsAttacking && IsPursuing)
        {
            _targetPos = _target.transform.position + 
                         Vector3.up * OrbitHeight + 
                         _currOrbitOrientation * Vector3.forward * OrbitRadius;
            _justAttacked = false;   
        }
        else if (FlyingEnemyType == FlyingEnemyTypes.Melee && IsAttacking && !_justAttacked)
        {
            MeleeWeapon.SetActive(true);
            _targetPos = _target.transform.position;
            _justAttacked = true;
        }
        else if (FlyingEnemyType == FlyingEnemyTypes.Gunner && IsAttacking)
        {
            IsAttacking = false;

            Vector3 dirToTarget = (_target.transform.position - transform.position).normalized;
            GameObject projectileGameObj = Instantiate(ProjectilePrefab, 
                                                       transform.position, 
                                                       Quaternion.FromToRotation(Vector3.forward, 
                                                       dirToTarget));
            
            Projectile projectile = projectileGameObj.GetComponent<Projectile>();
            projectile.TrajectoryPos = transform.position;
            projectile.TrajectoryDir = dirToTarget;

            if (ShootAudioClip)
                _enemyAudioSource.PlayOneShot(ShootAudioClip);
        }
        
        float distanceToTarget = Vector3.Distance(transform.position, _targetPos);
        if (IsAttacking && distanceToTarget < 0.1f)
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, 2.0f, 1 << LayerMask.NameToLayer("Player"));
            if (colliders.Length > 0)
            {
                PlayerController pc = colliders[0].GetComponentInParent<PlayerController>();
                if (pc)
                {
                    pc.HurtPlayer(AttackDamage);

                    if (MeleeAudioClip)
                        _enemyAudioSource.PlayOneShot(MeleeAudioClip);
                }
            }

            IsAttacking = false;
            _targetPos = _target.transform.position + 
                         Vector3.up * OrbitHeight + 
                         _currOrbitOrientation * Vector3.forward * OrbitRadius;

            MeleeWeapon.SetActive(false);
        }
        if (!IsPursuing)
            _targetPos = SpawnPos;
        
        IsAttacking = _enemy.TickAttackLogic(dt, distanceToTarget).Item1;
        IsPursuing = _enemy.TickSeekLogic(dt, distanceToTarget);

        transform.position += (_targetPos - transform.position).normalized * MoveSpeed * dt;

        if (CurrHealth < 0.0f)
        {
            if (DestructionEffect)
            {
                GameObject gameObject = Instantiate(DestructionEffect, transform.position, Quaternion.identity);
                gameObject.transform.localScale = Vector3.one * 0.5f;
                Destroy(gameObject, 4.0f);   
            }
            if (DeathAudioClip)
            {
                GameObject gameObject = new();
                AudioSource audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.volume = 0.2f;
                audioSource.minDistance = 5.0f;
                audioSource.clip = DeathAudioClip;
                audioSource.Play();

                if (!DestructionEffect)
                    Destroy(gameObject, 4.0f);
            }
            Destroy(gameObject);   
        }

        MeleeWeapon.transform.rotation = Quaternion.AngleAxis(720.0f * dt, MeleeWeapon.transform.up) * MeleeWeapon.transform.rotation;
    }

    public float DamageEnemy(float inDamage)
    {
        CurrHealth -= inDamage;
        float result = inDamage;
        if (DamageOutline)
        {
            DamageOutline.SetActive(true);
            Invoke(nameof(MakeDamageOutlineInvisible), 0.5f);   
        }
        if (HurtAudioClip)
            _enemyAudioSource.PlayOneShot(HurtAudioClip);

        return result;
    }

    public Tuple<bool, float> Attack()
    {
        return new(true, 0.0f);
    }
    
    private void MakeDamageOutlineInvisible()
    {
        DamageOutline.SetActive(false);
    }

    void OnDestroy()
    {
        if (CurrEnemySpawner)
            CurrEnemySpawner.OnEnemyDestroyed();
    }
}
