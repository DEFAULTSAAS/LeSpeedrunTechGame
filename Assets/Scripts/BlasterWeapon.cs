using UnityEngine;

public interface IWeapon
{
    public float LastFireTime {get; set;}
    public void TryFire();
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
