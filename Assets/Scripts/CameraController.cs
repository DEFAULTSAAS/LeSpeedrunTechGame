using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

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
    public float ReturnOutwardSpeed = 2.0f;
    public float TargetOrbitRadius = 4.0f;
    public float CollisionSphereRadius = 0.5f;
    public float BackupCollisionSphereRadius = 0.5f;
    public float SphereRadiusIncreaseFactor = 1.25f; // How much to increase sphere cast radius by.
    public float SphereHitDistanceFactor = 2.0f;
    public float SpherecastInterval = 60.0f;
    public int NumSphereHitDirRings = 9;
    
    public bool EnableFreeFly;
    public bool FollowTarget;

    private InputAction _moveInputAction;
    private InputAction _lookInputAction;

    private List<Vector3> _cameraSphereCastHits = new();
    private List<Vector3> _cameraSphereCastDirs = new();
    
    private float _currTargetOrbitRadius = 1.0f;
    private float _cameraSphereCastRadius = 1.0f;
    private float _angularSphereCastRadius = 1.0f;
    private float _inverseSpherecastInterval = 1.0f;
    private int _numSphereHitDirSegments = 16;

    private Quaternion _cameraOrientation = Quaternion.identity; // Orientation of camera around the player.

    private NativeArray<SpherecastCommand> _spherecastCommands;
    private NativeArray<RaycastHit> _spherecastResults;

    private NativeArray<SpherecastCommand> _backupSpherecastCommands;
    private NativeArray<RaycastHit> _backupSpherecastResults;

    private JobHandle _spherecastsJob;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _moveInputAction = InputSystem.actions.FindAction("Move");
        _lookInputAction = InputSystem.actions.FindAction("Look");
        
        _currTargetOrbitRadius = TargetOrbitRadius;
        _numSphereHitDirSegments = NumSphereHitDirRings * 2;
        _cameraSphereCastHits.Capacity = (_numSphereHitDirSegments * (NumSphereHitDirRings - 1)) + 2;
        
        _prevRadius = _currTargetOrbitRadius;
        _inverseSpherecastInterval /= SpherecastInterval;

        _spherecastCommands = new (_cameraSphereCastHits.Capacity, Allocator.Persistent);
        _spherecastResults = new (_cameraSphereCastHits.Capacity, Allocator.Persistent);

        _backupSpherecastCommands = new (_cameraSphereCastHits.Capacity, Allocator.Persistent);
        _backupSpherecastResults = new (_cameraSphereCastHits.Capacity, Allocator.Persistent);

        // Adapted from https://extremelearning.com.au/how-to-evenly-distribute-points-on-a-sphere-more-effectively-than-the-canonical-fibonacci-lattice/
        // for (int i = 0; i < MaxNumSphereCasts; i++)
        // {
        //     float theta = 2.0f * Mathf.PI * (i / GoldenRatio);
        //     float phi = Mathf.Acos(1.0f - 2.0f * ((i + 0.5f) / MaxNumSphereCasts));
            
        //     Vector3 dir;
        //     dir.x = Mathf.Cos(theta) * Mathf.Sin(phi);
        //     dir.y = Mathf.Sin(theta) * Mathf.Sin(phi);
        //     dir.z = Mathf.Cos(phi);

        //     _cameraSphereCastDirs.Add(dir);
        // }

        _cameraSphereCastDirs.Add(Vector3.up);
        for (int i = 0; i < (NumSphereHitDirRings - 1); i++)
        {
            float phi = Mathf.PI * ((i + 1) / (float)NumSphereHitDirRings);
            for (int j = 0; j < _numSphereHitDirSegments; j++)
            {
                float theta = 2.0f * Mathf.PI * (j / (float)_numSphereHitDirSegments);
                Vector3 dir;    
                
                dir.x = Mathf.Cos(theta) * Mathf.Sin(phi);
                dir.y = Mathf.Cos(phi);
                dir.z = Mathf.Sin(theta) * Mathf.Sin(phi);
                _cameraSphereCastDirs.Add(dir);
            }
        }
        _cameraSphereCastDirs.Add(-Vector3.up);

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

        _angularSphereCastRadius = maxNearestAngle * 0.6f;
        _cameraSphereCastRadius = _currTargetOrbitRadius * Mathf.Sin(_angularSphereCastRadius);
        _cameraSphereCastRadius *= SphereRadiusIncreaseFactor;

        Debug.Log(_cameraSphereCastRadius);
    }

    private float _spherecastCompletionTime = 0.0f;
    private bool _firstJobStarted = false;
    void Update()
    {
        for (;_spherecastsJob.IsCompleted && (Time.time - _spherecastCompletionTime) > _inverseSpherecastInterval;)
        {
            _spherecastsJob.Complete();
            if (!_firstJobStarted)
            {
                _firstJobStarted = true;
                
                RefreshSpherecastCommands();
                break;              
            }

            _cameraSphereCastHits.Clear();
            for (int i = 0; i < _spherecastResults.Length; i++)
            {
                RaycastHit hit = new();
                Vector3 dir = _cameraSphereCastDirs[i];

                // Backup sphere casts should always be smaller and more accurate,
                // so try and use them first.
                if (_backupSpherecastResults[i].collider != null)
                    hit = _backupSpherecastResults[i];
                else if (_spherecastResults[i].collider != null)
                    hit = _spherecastResults[i];

                if (hit.collider == null)
                    continue;

                //Debug.DrawLine(Target.position, Target.position + (dir * hit.distance), Color.blue);
                _cameraSphereCastHits.Add(dir * hit.distance);
            }

            _spherecastCompletionTime = Time.time;
            RefreshSpherecastCommands();
            break;
        }
    }

    private float _prevRadius;
    void LateUpdate()
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
        
        Quaternion aroundYRot = GetRotationAroundAxis(_cameraOrientation, Vector3.up);
        Vector3 forwardWithYRot = aroundYRot * Vector3.forward;
        Vector3 rotatedYAxis = _cameraOrientation * Vector3.up;

        float forwardDot = Vector3.Dot(_cameraOrientation * Vector3.forward, forwardWithYRot);
        float upDot = Vector3.Dot(rotatedYAxis, forwardWithYRot);

        Quaternion maxUpOrientation;
        if (upDot < 0.0f)
            maxUpOrientation = Quaternion.AngleAxis(-90.0f, aroundYRot * Vector3.right) * aroundYRot;
        else
            maxUpOrientation = Quaternion.AngleAxis(90.0f, aroundYRot * Vector3.right) * aroundYRot;

        if (forwardDot < 0.0f)
            _cameraOrientation = maxUpOrientation;   

        Vector3 adjustedCameraPos = GetAdjustedCameraPos();
        float targetRadius = adjustedCameraPos.magnitude;
        adjustedCameraPos = adjustedCameraPos.normalized;
        
        if (Physics.SphereCast(Target.position, 
                               CollisionSphereRadius, 
                               adjustedCameraPos, 
                               out RaycastHit hit, 
                               targetRadius, 
                               LayerMask.NameToLayer("Camera"), 
                               QueryTriggerInteraction.Ignore))
        {
            targetRadius = hit.distance;
        }
        if (targetRadius > _prevRadius)
        {
            targetRadius = Mathf.Lerp(_prevRadius, 
                                      targetRadius, 
                                      Mathf.SmoothStep(0.0f, 1.0f, ReturnOutwardSpeed * dt));
        }

        transform.position = Target.position + (adjustedCameraPos * targetRadius);
        transform.rotation = _cameraOrientation;

        _prevRadius = targetRadius;
        //Debug.Log(Vector3.Distance(transform.position, Target.position));
    }

    public static readonly float GoldenRatio = (1.0f + Mathf.Sqrt(5.0f)) * 0.5f;
    void FixedUpdate()
    {
        if (!Target || EnableFreeFly)
        {
            return;
        }

        // _cameraSphereCastHits.Clear();
        // foreach (Vector3 dir in _cameraSphereCastDirs)
        // {
        //     RaycastHit hit;
        //     bool isHit = Physics.SphereCast(Target.position, _cameraSphereCastRadius, dir, out hit, _currTargetOrbitRadius, LayerMask.NameToLayer("Camera"));
            
        //     Debug.DrawLine(Target.position, dir * _currTargetOrbitRadius);
        //     if (isHit)
        //     {
        //         _cameraSphereCastHits.Add(hit.point - Target.position);
        //         Debug.DrawLine(Target.position, hit.point, Color.blue);   
        //     }
        // }
    }

    void OnDestroy()
    {
        _spherecastCommands.Dispose();
        _spherecastResults.Dispose();

        _backupSpherecastCommands.Dispose();
        _backupSpherecastResults.Dispose();
    }

    public Quaternion GetCameraOrientation()
    {
        return _cameraOrientation;
    }

    private void RefreshSpherecastCommands()
    {
        _cameraSphereCastRadius = _currTargetOrbitRadius * Mathf.Sin(_angularSphereCastRadius);
        _cameraSphereCastRadius *= SphereRadiusIncreaseFactor;

        int counter = 0;
        foreach (Vector3 dir in _cameraSphereCastDirs)
        {
            SpherecastCommand spherecastCommand = new();
            SpherecastCommand backupSpherecastCommand;

            spherecastCommand.origin = Target.position;
            spherecastCommand.direction = dir;
            spherecastCommand.distance = _currTargetOrbitRadius;   
            spherecastCommand.radius = _cameraSphereCastRadius;
            spherecastCommand.queryParameters = new QueryParameters(LayerMask.NameToLayer("Camera"), 
                                                                   false, 
                                                                   QueryTriggerInteraction.Ignore);

            backupSpherecastCommand = spherecastCommand;
            backupSpherecastCommand.radius = BackupCollisionSphereRadius - 0.01f;

            _spherecastCommands[counter] = spherecastCommand;
            _backupSpherecastCommands[counter] = backupSpherecastCommand;

            counter++;
        }

        JobHandle spherecastsJob = SpherecastCommand.ScheduleBatch(_spherecastCommands, 
                                                                   _spherecastResults, 
                                                                   16);
        JobHandle backupSpherecastsJob = SpherecastCommand.ScheduleBatch(_backupSpherecastCommands, 
                                                                         _backupSpherecastResults, 
                                                                         16);

        _spherecastsJob = JobHandle.CombineDependencies(spherecastsJob, backupSpherecastsJob);
    }

    private Vector3 GetAdjustedCameraPos()
    {
        Vector3 cameraDir = _cameraOrientation * -Vector3.forward;
        if (_cameraSphereCastHits.Count == 0)
            return cameraDir * _currTargetOrbitRadius;

        float weightSum = 1.0f;
        float weightedRadiusSum = _currTargetOrbitRadius;
        
        foreach (Vector3 dir in _cameraSphereCastHits)
        {
            float dist = Vector3.Distance(cameraDir * _currTargetOrbitRadius, dir.normalized * _currTargetOrbitRadius);
            float weight = Mathf.Exp(-(dist * dist) / (SphereHitDistanceFactor * SphereHitDistanceFactor));

            weightSum += weight;
            weightedRadiusSum += dir.magnitude * weight;
        }

        return cameraDir * (weightedRadiusSum / weightSum);
    }

    private void UpdateFreeCamera(float inDeltaTime)
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
        Vector3 projection = Vector3.Project(new Vector3(inRot.x, inRot.y, inRot.z), inAxis);
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
