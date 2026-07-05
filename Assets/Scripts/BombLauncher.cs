using UnityEngine;

public class BombLauncher : MonoBehaviour, IWeapon
{
    [field : SerializeField] public float Damage {get; private set;}
    [field : SerializeField] public float ProjectileSpeed {get; private set;}
    [field : SerializeField] public float FireDelay {get; private set;}
    public float LastFireTime {get; set;}
    [field : SerializeField] public Transform ProjectileSpawnPoint {get; set;}
    [field : SerializeField] public ProjectileTypes ProjectileType {get; private set; }

    public Animator WeaponAnimator;
    public GameObject ProjectilePrefab;

    private Transform _targetPos;
    private AudioSource _weaponSound;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _targetPos = new GameObject().transform;
        _weaponSound = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Fire(Vector3 inFireDir, Vector3 inTrajectoryPos, Vector3 inTrajectoryDir, Transform inTarget)
    {
        WeaponAnimator.ResetControllerState();
        WeaponAnimator.Play("FireBombLauncher");
        _weaponSound.Play();

        GameObject projectileGameObj = Instantiate(ProjectilePrefab, ProjectileSpawnPoint.position, Quaternion.FromToRotation(Vector3.forward, ProjectileSpawnPoint.forward));
        Projectile projectile = projectileGameObj.GetComponent<Projectile>();
        
        if (!float.IsPositiveInfinity(inFireDir.x))
        {
            _targetPos.position = inFireDir;
            projectile.Target = _targetPos;
        }
        else
            projectile.Target = null;

        projectile.TrajectoryPos = inTrajectoryPos;
        projectile.TrajectoryDir = inTrajectoryDir;
        projectile.Damage = Damage;
        projectile.Speed = (inTrajectoryDir.x > ProjectileSpeed) ? inTrajectoryDir.x : ProjectileSpeed;
    }
}
