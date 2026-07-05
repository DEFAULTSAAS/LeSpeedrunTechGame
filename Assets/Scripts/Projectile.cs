using UnityEngine;

public enum ProjectileTypes
{
    Bullet,
    Bomb
}

public class Projectile : MonoBehaviour
{
    public float TimeSpawned { get; private set; }
    public Vector3 TrajectoryPos { get; set; }
    public Vector3 TrajectoryDir { get; set; }
    public Vector3 SpawnPos { get; set; }

    public ProjectileTypes ProjectileType;
    public Transform Target = null;
    public AnimationCurve BombHeightCurve;
    public GameObject ExplosionPrefab;
    public AudioClip ExplosionSoundClip;
    public float Speed = 5.0f;
    public float Damage = 5.0f;
    public float BombTime = 1.5f;
    public float BombHeight = 2.0f;
    public float Timeout = 10.0f;

    private Transform _visRot;
    private Vector3 _spawnTrajDiff;
    private Vector3 _targetPos;
    private float _currBombTime;
    private float _currBombHeight;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        TimeSpawned = Time.time;
        SpawnPos = transform.position;

        _currBombTime = BombTime;
        _currBombHeight = BombHeight;        
        _spawnTrajDiff = TrajectoryPos - transform.position;
        _targetPos = Target ? Target.transform.position : Vector3.zero;

        if (Target)
        {
            Vector3 horiVec = _targetPos - SpawnPos;
            horiVec.y = 0.0f;

            float horiDist = horiVec.magnitude;
            float bombHoriDist = Speed * BombTime;
            if (horiDist < bombHoriDist)
            {
                _currBombTime = horiDist / Speed;
                
                float reductionFactor = _currBombTime / BombTime;
                _currBombHeight *= reductionFactor;
            }
        }

        _visRot = transform.GetChild(0);
        if (_visRot && ProjectileType == ProjectileTypes.Bomb)
        {
            _visRot.forward = new Vector3(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f)).normalized;
        }
    }

    private float _currBombTimeAcc = 0.0f;
    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;

        if ((Time.time - TimeSpawned) > Timeout)
        {
            Destroy(gameObject);
            return;
        }

        switch (ProjectileType)
        {
            case ProjectileTypes.Bullet:
            {
                if (Target != null)
                {
                    Vector3 moveDir = (Target.position - transform.position).normalized;
                    transform.position += moveDir * Speed * dt;
                    transform.rotation = Quaternion.FromToRotation(Vector3.forward, moveDir);
                }
                else
                {
                    transform.position += transform.forward * Speed * dt;
                    
                    Vector3 targetPos = Vector3.Project(transform.position - SpawnPos, TrajectoryDir);
                    targetPos = SpawnPos + targetPos;
                    targetPos += _spawnTrajDiff;
                    transform.position += (targetPos - transform.position) * Speed * dt;

                    Debug.Log(targetPos - transform.position);
                }
            } break;
            case ProjectileTypes.Bomb:
            {
                if (Target != null)
                {
                    transform.position += (_targetPos - transform.position).normalized * Speed * dt;
                }
                else
                {
                    transform.position += transform.forward * Speed * dt;   
                }
                
                if (_currBombTimeAcc <= _currBombTime)
                {
                    float adjustedDT = dt / _currBombTime;
                    transform.position += Vector3.up * 
                                          PlayerController.CalcCurveVelocity(BombHeightCurve, _currBombTimeAcc / _currBombTime, adjustedDT) * 
                                          _currBombHeight * 
                                          dt;    
                }
                else
                    transform.position += Vector3.up * Physics.gravity.y * dt;
                _currBombTimeAcc += dt;
            } break;
        }

        if (_visRot && ProjectileType == ProjectileTypes.Bomb)
        {
            _visRot.rotation = Quaternion.AngleAxis(180.0f * dt, _visRot.forward) * _visRot.rotation;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (ExplosionSoundClip)
        {
            GameObject gameObject = new();
            gameObject.transform.position = transform.position;
            
            AudioSource explosionSound = gameObject.AddComponent<AudioSource>();
            explosionSound.clip = ExplosionSoundClip;
            explosionSound.volume = 0.2f;
            explosionSound.minDistance = 5.0f;
            explosionSound.Play();

            Destroy(gameObject, 4.0f);
        }

        if (ProjectileType == ProjectileTypes.Bomb)
        {
            Collider[] playerCollider = Physics.OverlapSphere(transform.position, 1.25f, 1 << LayerMask.NameToLayer("Player"));
            if (playerCollider.Length > 0)
            {
                PlayerController pc = playerCollider[0].gameObject.transform.parent.GetComponent<PlayerController>();
                if (pc && (Time.time - TimeSpawned) <= 0.125)
                {
                    pc.RedirectPlayerVelocity();
                }
            }

            Collider[] enemyColliders = Physics.OverlapSphere(transform.position, TrajectoryPos.x, 1 << LayerMask.NameToLayer("Enemy")); 
            foreach (Collider collider in enemyColliders)
            {
                IEnemy enemy = collider.gameObject.GetComponent<IEnemy>();
                if (enemy != null)
                {
                    enemy.DamageEnemy(Damage);
                }
            }

            if (ExplosionPrefab)
            {
                GameObject explosion = Instantiate(ExplosionPrefab, transform.position, Quaternion.identity);
                explosion.transform.localScale = Vector3.one * 0.5f;
                Destroy(explosion, 4.0f);
            }
            Destroy(gameObject);
        }
    }
}
