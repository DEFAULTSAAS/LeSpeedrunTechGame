using UnityEngine;

public interface IWeapon
{
    public float FireDelay {get;}
    public float LastFireTime {get; set;}
    public void TryFire()
    {
        if ((Time.time - LastFireTime) > FireDelay)
        {
            Fire();
            LastFireTime = Time.time;
        }
    }
    public void Fire();
}

public class BlasterWeapon : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
