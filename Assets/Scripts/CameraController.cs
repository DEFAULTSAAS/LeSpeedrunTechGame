using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.UIElements;

public struct CameraOrbitInfo
{
    public Vector3 Pos;
    public Vector3 Norm;
    public Vector3 Tgnt;
}

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
    public Vector2 InputAxisFactorsTPS = new Vector2(-1.0f, 1.0f);

    public float MoveSpeed = 10.0f;
    public float MoveSpeedRampUp = 1.0f;
    public float TargetOrbitRadius = 4.0f;
    public float CollisionSphereRadius = 0.5f;
    public float MoveInterpolationSpeed = 1.0f;
    
    public bool EnableFreeFly;
    public bool FollowTarget;

    private InputAction _moveInputAction;
    private InputAction _lookInputAction;
    private Vector2 _lookInputAcc;

    private CameraOrbitInfo _targetCameraOrbit;
    private CameraOrbitInfo _currCameraOrbit;
    private Quaternion _cameraOrientation = Quaternion.identity; // Orientation of camera around the player.
    
    private List<HighOrderBezier> _cameraMovementCurves = new();
    private float _currCameraCurveLength;
    private float _currCameraCurveT = 0.0f;
    
    private float _currCameraOrbitRadius;
    private bool _tooCloseToPlayer;

    private List<Vector2> _lookInputVals = new();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _moveInputAction = InputSystem.actions.FindAction("Move");
        _lookInputAction = InputSystem.actions.FindAction("Look");
        _currCameraOrbitRadius = TargetOrbitRadius;
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

        if (_cameraMovementCurves.Count > 0 && _currCameraCurveT < 1.0f)
        {
            transform.position = Target.position + _cameraMovementCurves[0].SamplePos(_currCameraCurveT);
            _currCameraCurveT += (MoveInterpolationSpeed / _currCameraCurveLength) * dt; 
        }
        if (_cameraMovementCurves.Count > 0 && _currCameraCurveT >= 1.0f)
        {
            _cameraMovementCurves.RemoveAt(0);
            _currCameraCurveT = 0.0f;
            
            if (_cameraMovementCurves.Count > 0)
                _currCameraCurveLength = _cameraMovementCurves[0].CalcApproxLength(64);
        }
        if (_cameraMovementCurves.Count == 0)
            transform.position = _targetCameraOrbit.Pos;

        Vector2 lookInput = _lookInputAction.ReadValue<Vector2>() * InputAxisFactorsTPS;
        lookInput *= LookSpeedTPS * dt;
        
        _lookInputVals.Add(lookInput);
        _lookInputAcc += lookInput;

        _currCameraCurveT += MoveInterpolationSpeed * dt;
        //transform.position = _cameraSpline.EvaluatePosition(Mathf.Clamp01(_cameraSplineSampleT));
        transform.rotation = Quaternion.LookRotation((Target.position - transform.position).normalized, Vector3.up);
    }

    void FixedUpdate()
    {
        if (!Target || EnableFreeFly)
        {
            _lookInputAcc = Vector2.zero;
            return;
        }

        HighOrderBezier newCameraCurve;
        newCameraCurve.ControlPoints = new();

        if (_cameraMovementCurves.Count > 0)
            newCameraCurve.StartPoint = _cameraMovementCurves[_cameraMovementCurves.Count - 1].EndPoint;
        else
            newCameraCurve.StartPoint = transform.position - Target.position;

        Quaternion projectedOrientation = _cameraOrientation;
        foreach (Vector2 lookInput in _lookInputVals)
        {
            RaycastHit info;
            projectedOrientation = Quaternion.AngleAxis(lookInput.x, Vector3.up) * 
                                   Quaternion.AngleAxis(lookInput.y, projectedOrientation * Vector3.right) * 
                                   projectedOrientation;

            Vector3 controlPoint = projectedOrientation * -Vector3.forward;
            bool isHit = 
                Physics.SphereCast(new Ray(Target.position, controlPoint), CollisionSphereRadius, out info, 
                                   TargetOrbitRadius, LayerMask.NameToLayer("Camera"));

            float orbitDist = TargetOrbitRadius;
            if (isHit)
                orbitDist = (info.point - Target.position).magnitude;

            newCameraCurve.ControlPoints.Add(controlPoint * orbitDist);
        }

        _cameraOrientation = Quaternion.AngleAxis(_lookInputAcc.x, Vector3.up) * Quaternion.AngleAxis(_lookInputAcc.y, _cameraOrientation * Vector3.right) * _cameraOrientation;
        _targetCameraOrbit.Norm = _cameraOrientation * -Vector3.forward;
        {
            RaycastHit info;
            bool isHit = 
                Physics.SphereCast(new Ray(Target.position, _targetCameraOrbit.Norm), CollisionSphereRadius, out info, 
                                   TargetOrbitRadius, LayerMask.NameToLayer("Camera"));

            float orbitDist = TargetOrbitRadius;
            if (isHit)
                orbitDist = (info.point - Target.position).magnitude;

            newCameraCurve.EndPoint = orbitDist * _targetCameraOrbit.Norm;
            _targetCameraOrbit.Pos = Target.position + newCameraCurve.EndPoint;
        }

        if (_cameraMovementCurves.Count == 0)
        {
            _currCameraCurveLength = newCameraCurve.CalcApproxLength(64);
            _currCameraCurveT = 0.0f;   
        }

        float startEndMag = (newCameraCurve.EndPoint - newCameraCurve.StartPoint).magnitude;
        if (startEndMag > 0.01f)
        {
            if (newCameraCurve.ControlPoints.Count == 0)
            {
                Debug.Log("I have been triggered dummy");
                Vector3 middlePos = Vector3.Lerp(newCameraCurve.StartPoint, newCameraCurve.EndPoint, 0.5f);
                Vector3 middleNorm = Vector3.Lerp(newCameraCurve.StartPoint.normalized, newCameraCurve.EndPoint.normalized, 0.5f);

                startEndMag *= 0.5f;
                Vector3 rayOrigin = Target.position + newCameraCurve.StartPoint + middlePos;

                RaycastHit info;
                bool isHit = 
                    Physics.SphereCast(new Ray(rayOrigin, middleNorm), 
                    CollisionSphereRadius, out info, startEndMag, LayerMask.NameToLayer("Camera"));

                if (isHit)
                    startEndMag = (info.point - rayOrigin).magnitude;
                newCameraCurve.ControlPoints.Add(middlePos + (middleNorm * startEndMag));
            }

            _cameraMovementCurves.Add(newCameraCurve);   
        }

        if (_cameraMovementCurves.Count > 0)
        {
            for (int i = 0; i < 64; i++)
            {
                Vector3 startPos = _cameraMovementCurves[0].SamplePos(i / 64.0f);
                Vector3 endPos = _cameraMovementCurves[0].SamplePos((i + 1) / 64.0f);

                Debug.DrawLine(startPos, endPos, Color.red);
            }

            foreach (Vector3 controlPoint in _cameraMovementCurves[0].ControlPoints)
            {
                Debug.DrawLine(controlPoint, controlPoint + Vector3.up, Color.limeGreen);
                Debug.Log(controlPoint);
            }   
        }
        Debug.DrawLine(_targetCameraOrbit.Pos, _targetCameraOrbit.Pos + (Vector3.up * 3.0f), Color.darkBlue);

        _lookInputVals.Clear();
        _lookInputAcc = Vector2.zero;
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

    static Vector3 CustomLerp(Vector3 inPosOne, Vector3 inPosTwo, float inDelta, float inMinDist = 0.001f)
    {
        Vector3 toTwo = inPosTwo - inPosOne;
        float toTwoDist = toTwo.magnitude;
        
        if (inDelta >= toTwoDist || toTwoDist <= inMinDist)
            return inPosTwo;

        toTwo.Normalize();
        return inPosOne + (toTwo * inDelta);
    }
}
