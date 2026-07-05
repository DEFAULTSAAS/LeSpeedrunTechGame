using UnityEngine;

public interface IWeapon
{
    public float Damage {get;}
    public float ProjectileSpeed {get;}
    public float FireDelay {get;}
    public float LastFireTime {get; set;}
    public Transform ProjectileSpawnPoint {get; set;}
    public ProjectileTypes ProjectileType {get;}

    public void TryFire(Vector3 inFireDir, Vector3 inTrajectoryPos, Vector3 inTrajectoryDir, Transform inTarget = null)
    {
        if ((Time.time - LastFireTime) > FireDelay)
        {
            Fire(inFireDir, inTrajectoryPos, inTrajectoryDir, inTarget);
            LastFireTime = Time.time;
        }
    }
    public void Fire(Vector3 inFireDir, Vector3 inTrajectoryPos, Vector3 inTrajectoryDir, Transform inTarget = null);
}

public class BlasterWeapon : MonoBehaviour, IWeapon
{
    [field : SerializeField] public float Damage {get; private set;}
    [field : SerializeField] public float ProjectileSpeed {get; private set;}
    [field : SerializeField] public float FireDelay {get; private set;}
    public float LastFireTime {get; set;}
    [field : SerializeField] public Transform ProjectileSpawnPoint {get; set;}
    [field : SerializeField] public ProjectileTypes ProjectileType {get;}

    public GameObject ProjectilePrefab;

    private IWeapon _weapon;
    private AudioSource _weaponSound;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _weapon = this;
        _weaponSound = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Fire(Vector3 inFireDir, Vector3 inTrajectoryPos, Vector3 inTrajectoryDir, Transform inTarget)
    {
        _weaponSound.pitch = Random.Range(0.9f, 1.1f);
        _weaponSound.Play();

        GameObject projectileGameObj = Instantiate(ProjectilePrefab, ProjectileSpawnPoint.position, Quaternion.FromToRotation(Vector3.forward, inFireDir));
        Projectile projectile = projectileGameObj.GetComponent<Projectile>();
        
        projectile.TrajectoryPos = inTrajectoryPos;
        projectile.TrajectoryDir = inTrajectoryDir;
        projectile.Target = inTarget;
        projectile.Damage = Damage;
        projectile.Speed = ProjectileSpeed;
    }
}
