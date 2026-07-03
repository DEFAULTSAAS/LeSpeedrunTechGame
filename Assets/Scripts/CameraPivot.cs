using UnityEngine;

public class CameraPivot : MonoBehaviour
{
    public bool InStrafeMode {get; set;} = false;

    public Transform Target;
    public Vector3 StrafingOffset = new Vector3(0.0f, 0.5f, 0.0f);
    public float MoveSpeed = 10.0f;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        transform.SetParent(null);
    }

    // Update is called once per frame
    void LateUpdate()
    {
        float dt = Time.deltaTime;
        Vector3 targetPos = InStrafeMode ? Target.position + StrafingOffset : Target.position;
        transform.position += (targetPos - transform.position) * MoveSpeed * dt;  
    }
}
