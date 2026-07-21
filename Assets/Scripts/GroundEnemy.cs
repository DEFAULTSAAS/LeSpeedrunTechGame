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

    public float MoveSpeed = 5.0f;
    public float JumpHeight = 2.0f;
    public float AttackDuration = 0.5f;
    public float MeleeDistance = 1.5f;
    public float DefendChance = 0.4f;
    public GroundEnemyTypes GroundEnemyType;
    public NavMeshAgent _navMeshAgent;
    public GameObject ProjectilePrefab;
    public GameObject MeleeWeapon;
    public Material ShieldMaterial;
    public AudioClip DeathAudioClip;
    public AudioClip ShootAudioClip;
    public AudioClip MeleeAudioClip;
    public AudioClip HurtAudioClip;

    private IEnemy _enemy;
    private AudioSource _enemyAudioSource;
    private PlayerController _target;
    private Material _baseMaterial;
    private MeshRenderer _outlineMeshRenderer;
    private float _currAttackTime = 0.0f;
    private bool IsDefending = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (!_navMeshAgent)
            _navMeshAgent = GetComponent<NavMeshAgent>();
        if (!_navMeshAgent)
            throw new NullReferenceException("Could not get nav mesh agent for ground enemy.");

        _outlineMeshRenderer = DamageOutline.GetComponent<MeshRenderer>();
        _baseMaterial = _outlineMeshRenderer.material;
        _enemyAudioSource = GetComponent<AudioSource>();
        _target = FindFirstObjectByType<PlayerController>();
        _enemy = this;

        CurrHealth = Health;
        _navMeshAgent.speed = MoveSpeed;
        _navMeshAgent.height = JumpHeight;
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

        _currAttackTime += dt;
        if (IsAttacking && _currAttackTime > AttackDuration)
        {
            IsAttacking = false;
            IsDefending = false;

            _outlineMeshRenderer.material = _baseMaterial;
            _outlineMeshRenderer.gameObject.SetActive(false);
            MeleeWeapon.SetActive(false);
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

        if (CurrHealth < 0.0f)
        {
            if (DestructionEffect)
            {
                GameObject gameObject = Instantiate(DestructionEffect, transform.position, Quaternion.identity);
                Destroy(gameObject, 4.0f);   
            }
            if (DeathAudioClip)
            {
                GameObject gameObject = new();
                AudioSource audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.outputAudioMixerGroup = MusicManager.GlobalMixerGroup;
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
        if (!IsDefending)
        {
            CurrHealth -= inDamage;
            float result = inDamage;
            if (DamageOutline)
            {
                DamageOutline.SetActive(true);
                Invoke(nameof(MakeDamageOutlineInvisible), 0.25f);   
            }
            
            if (!IsPursuing)
            {
                IsPursuing = true;
                CurrOutOfRangeAcc = 0.0f;
            }

            if (HurtAudioClip)
                _enemyAudioSource.PlayOneShot(HurtAudioClip);

            return result;
        }
        
        return 0.0f;
    }

    public Tuple<bool, float> Attack()
    {
        float distanceToTarget = Vector3.Distance(transform.position, _target.transform.position);
        _outlineMeshRenderer.material = _baseMaterial;
        _outlineMeshRenderer.gameObject.SetActive(false);

        if (GroundEnemyType == GroundEnemyTypes.Shooter && distanceToTarget > MeleeDistance)
        {
            float randomNum = UnityEngine.Random.Range(0.0f, 1.0f);
            if (randomNum <= DefendChance)
            {
                IsDefending = true;
                _outlineMeshRenderer.material = ShieldMaterial;
                _outlineMeshRenderer.gameObject.SetActive(true);
                //Debug.Log("Defending");   
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

                if (ShootAudioClip)
                    _enemyAudioSource.PlayOneShot(ShootAudioClip);

                //Debug.Log("Shooting");
            }
        }
        else if (distanceToTarget < MeleeDistance)
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, 2.0f, 1 << LayerMask.NameToLayer("Player"));
            if (colliders.Length > 0)
            {
                PlayerController pc = colliders[0].GetComponentInParent<PlayerController>();
                if (pc)
                {
                    pc.HurtPlayer(AttackDamage);
                }
            }

            if (MeleeAudioClip)
                _enemyAudioSource.PlayOneShot(MeleeAudioClip);

            MeleeWeapon.SetActive(true);
            //Debug.Log("Melee");
        }

        _currAttackTime = 0.0f;
        return new (true, 0.0f);
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
