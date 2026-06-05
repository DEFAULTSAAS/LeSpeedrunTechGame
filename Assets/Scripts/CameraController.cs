using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public struct HighOrderBezier
{
    public Vector3 StartPoint;
    public Vector3 EndPoint;
    public List<Vector3> ControlPoints;

    // Adapted from https://en.wikipedia.org/wiki/De_Casteljau's_algorithm
    public Vector3 SamplePos(float inT)
    {
        List<Vector3> samplePoints = new (ControlPoints);
        samplePoints.Insert(0, StartPoint);
        samplePoints.Add(EndPoint);

        int n = samplePoints.Count;
        for (int i = 1; i < n; i++)
        {
            for (int j = 0; j < (n - i); j++)
            {
                samplePoints[j] = Vector3.Lerp(samplePoints[j], samplePoints[j + 1], inT);
            }
        }

        return samplePoints[0];
    }

    public float CalcApproxLength(int inNumSamples)
    {
        float accLength = 0.0f;
        Vector3 prevPoint = StartPoint;

        for (int i = 1; i < inNumSamples; i++)
        {
            Vector3 sampledPoint = SamplePos(i / 64.0f);
            accLength += (prevPoint - sampledPoint).magnitude;
            prevPoint = sampledPoint;
        }

        return accLength;        
    } 
}

public class CameraController : MonoBehaviour
{
    public Transform Target;
    
    public Vector2 LookSpeedFPS = new (10.0f, 10.0f);
    public Vector2 LookSpeedTPS = new (10.0f, 10.0f);
    public Vector2 InputAxisFactorsFPS = new Vector2(1.0f, -1.0f); // For adjust look inversion and such.
    public Vector2 InputAxisFactorsTPS = new Vector2(1.0f, -1.0f);

    public float MoveSpeed = 10.0f;
    public float MoveSpeedRampUp = 1.0f;
    public float TargetOrbitRadius = 4.0f;
    public float CollisionSphereRadius = 0.5f;
    public float SphereRadiusIncreaseFactor = 1.25f; // How much to increase sphere cast radius by.
    public float SphereHitInfluenceCutoffAngle = 35.0f;
    public int MaxNumSphereCasts = 64;
    
    public bool EnableFreeFly;
    public bool FollowTarget;

    private InputAction _moveInputAction;
    private InputAction _lookInputAction;

    private List<Vector3> _cameraSphereCastHits = new();
    private List<Vector3> _cameraSphereCastDirs = new();
    private float _cameraSphereCastRadius = 1.0f;
    private Quaternion _cameraOrientation = Quaternion.identity; // Orientation of camera around the player.

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _moveInputAction = InputSystem.actions.FindAction("Move");
        _lookInputAction = InputSystem.actions.FindAction("Look");
        _cameraSphereCastHits.Capacity = MaxNumSphereCasts;

        // Adapted from https://extremelearning.com.au/how-to-evenly-distribute-points-on-a-sphere-more-effectively-than-the-canonical-fibonacci-lattice/
        for (int i = 0; i < MaxNumSphereCasts; i++)
        {
            float theta = 2.0f * Mathf.PI * (i / GoldenRatio);
            float phi = Mathf.Acos(1.0f - 2.0f * ((i + 0.5f) / MaxNumSphereCasts));
            
            Vector3 dir;
            dir.x = Mathf.Cos(theta) * Mathf.Sin(phi);
            dir.y = Mathf.Sin(theta) * Mathf.Sin(phi);
            dir.z = Mathf.Cos(phi);

            _cameraSphereCastDirs.Add(dir);
        }

        float maxNearestAngle = 0.0f;
        for (int i = 0; i < _cameraSphereCastDirs.Count; i++)
        {
            Vector3 dir = _cameraSphereCastDirs[i];
            float maxDot = -1.0f;

            for (int j = 0; j < _cameraSphereCastDirs.Count; j++)
            {
                if (i == j)
                    continue;

                float dot = Vector3.Dot(dir, _cameraSphereCastDirs[j]);
                if (dot > maxDot)
                    maxDot = dot;
            }

            float angle = Mathf.Acos(maxDot);
            if (angle > maxNearestAngle)
                maxNearestAngle = angle; 
        }

        _cameraSphereCastRadius = maxNearestAngle * 0.6f;
        _cameraSphereCastRadius *= SphereRadiusIncreaseFactor;
    }

    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;

        if (EnableFreeFly)
        {
            UpdateFreeCamera(dt);
            return;
        }
        if (!Target)
            return;

        Vector2 lookInput = _lookInputAction.ReadValue<Vector2>() * InputAxisFactorsTPS;
        lookInput *= LookSpeedTPS * dt;

        _cameraOrientation = Quaternion.AngleAxis(lookInput.x, Vector3.up) * 
                             Quaternion.AngleAxis(lookInput.y, _cameraOrientation * Vector3.right) * 
                             _cameraOrientation;
        
        
        transform.rotation = _cameraOrientation; //Quaternion.LookRotation((Target.position - transform.position).normalized, Vector3.up);
    }

    static readonly float GoldenRatio = (1.0f + Mathf.Sqrt(5.0f)) * 0.5f;
    void FixedUpdate()
    {
        if (!Target || EnableFreeFly)
        {
            return;
        }

        _cameraSphereCastHits.Clear();
        foreach (Vector3 dir in _cameraSphereCastDirs)
        {
            RaycastHit hit;
            bool isHit = Physics.SphereCast(Target.position, _cameraSphereCastRadius, dir, out hit, TargetOrbitRadius, LayerMask.NameToLayer("Camera"));

            if (isHit)
                _cameraSphereCastHits.Add(hit.point - Target.position);
        }
    }

    Vector3 GetAdjustedCameraPos()
    {
        Vector3 cameraDir = _cameraOrientation * -Vector3.forward;
        if (_cameraSphereCastHits.Count == 0)
            return cameraDir * TargetOrbitRadius;



        return Vector3.zero;
    }

    void UpdateFreeCamera(float inDeltaTime)
    {
        Vector2 lookInput = _lookInputAction.ReadValue<Vector2>() * InputAxisFactorsFPS;
        lookInput *= LookSpeedFPS * inDeltaTime;
        transform.rotation = Quaternion.AngleAxis(lookInput.x, Vector3.up) * Quaternion.AngleAxis(lookInput.y, transform.right) * transform.rotation;

        Vector2 moveInput = _moveInputAction.ReadValue<Vector2>();
        Vector3 forwardDir = transform.forward * moveInput.y + transform.right * moveInput.x;
        
        forwardDir.Normalize();
        transform.position += forwardDir * MoveSpeed * inDeltaTime;
    }

    public static Vector3 GetCentreControlPoint(Vector3 inFrom, Vector3 inTo, Quaternion inStartRot, Quaternion inInputRot)
    {
        Vector3 inFromUnit = inFrom.normalized;
        Vector3 inToUnit = inTo.normalized;
        
        Vector3 toFrom = inFrom - inTo;
        Vector3 toFromTangent = Vector3.ProjectOnPlane(toFrom, inToUnit).normalized;

        float angle = Mathf.Acos(Vector3.Dot(inFromUnit, inToUnit));
        if ((Mathf.Rad2Deg * angle) > 120.0f)
        {
            Vector3 norm = Quaternion.Slerp(inStartRot, inInputRot, 0.5f) * inFromUnit;
            return norm * toFrom.magnitude * 0.5f;
        }
        
        float denom = Vector3.Dot(inFromUnit, toFromTangent);        
        if (Mathf.Abs(denom) <= 0.0001f)
        {
            Vector3 norm = Quaternion.Slerp(inStartRot, inInputRot, 0.5f) * inFromUnit;
            return norm * toFrom.magnitude * 0.5f;
        }

        float d = Vector3.Dot(inFromUnit, toFrom) / denom;
        return inTo + (toFromTangent * d);
    }

    // Adapted from https://github.com/godotengine/godot-proposals/issues/8906
    public static Quaternion GetRotationAroundAxis(Quaternion inRot, Vector3 inAxis)
    {
        Vector3 projection = Vector3.ProjectOnPlane(new Vector3(inRot.x, inRot.y, inRot.z), inAxis);
        return new Quaternion(projection.x, projection.y, projection.z, inRot.w).normalized;
    }

    public static Vector3 CustomLerp(Vector3 inPosOne, Vector3 inPosTwo, float inDelta, float inMinDist = 0.001f)
    {
        Vector3 toTwo = inPosTwo - inPosOne;
        float toTwoDist = toTwo.magnitude;
        
        if (inDelta >= toTwoDist || toTwoDist <= inMinDist)
            return inPosTwo;

        toTwo.Normalize();
        return inPosOne + (toTwo * inDelta);
    }
}
