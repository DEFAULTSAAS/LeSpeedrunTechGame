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
    public float Speed = 5.0f;
    public float Damage = 5.0f;
    public float BombTime = 1.5f;
    public float BombHeight = 2.0f;
    public float Timeout = 10.0f;

    private Vector3 _spawnTrajDiff;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        TimeSpawned = Time.time;
        SpawnPos = transform.position;
        _spawnTrajDiff = TrajectoryPos - transform.position;
    }

    private float _currBombTime = 0.0f;
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
                    targetPos += _spawnTrajDiff;
                    targetPos = SpawnPos + targetPos;
                    transform.position += (targetPos - transform.position) * Speed * dt;
                }
            } break;
            case ProjectileTypes.Bomb:
            {
                if (Target != null)
                {
                    transform.position += (Target.position - transform.position).normalized * Speed * dt;
                }
                else
                {
                    transform.position += transform.forward * Speed * dt;   
                }
                
                if (_currBombTime <= BombTime)
                {
                    float adjustedDT = dt / BombTime;
                    transform.position += Vector3.up * 
                                          PlayerController.CalcCurveVelocity(BombHeightCurve, _currBombTime / BombTime, adjustedDT) * 
                                          BombHeight * 
                                          dt;    
                }
                else
                    transform.position += Vector3.up * Physics.gravity.y * dt;
                _currBombTime += dt;
            } break;
        }
    }
}
