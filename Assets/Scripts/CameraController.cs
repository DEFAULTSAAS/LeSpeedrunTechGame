using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public struct CameraOrbitInfo
{
    public Vector3 Pos;
    public Vector3 Norm;
    public Vector3 Tgnt;
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
    public float LookInterpolationSpeed = 1.0f;
    public float CameraOrbitTangentFactor = 0.4f;
    
    public bool EnableFreeFly;
    public bool FollowTarget;

    private InputAction _moveInputAction;
    private InputAction _lookInputAction;
    private Vector2 _lookInputAcc;

    CameraOrbitInfo _targetCameraOrbit;
    CameraOrbitInfo _currCameraOrbit;

    private Quaternion _cameraOrientation = Quaternion.identity; // Orientation of camera around the player.
    private Spline _cameraSpline = new Spline();
    private float _cameraSplineSampleT;

    private float _currCameraSplineLength;
    private float _currCameraOrbitRadius;
    private bool _tooCloseToPlayer;

    private List<Vector2> _lookInputVals;

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

        Vector2 lookInput = _lookInputAction.ReadValue<Vector2>() * InputAxisFactorsTPS;
        lookInput *= LookSpeedTPS * dt;
        
        _lookInputVals.Add(lookInput);
        _lookInputAcc += lookInput;

        _cameraSplineSampleT += MoveInterpolationSpeed * dt;
        transform.position = _cameraSpline.EvaluatePosition(Mathf.Clamp01(_cameraSplineSampleT));
        transform.rotation = Quaternion.LookRotation((Target.position - transform.position).normalized, Vector3.up);
    }

    void FixedUpdate()
    {
        if (!Target || EnableFreeFly)
        {
            _lookInputAcc = Vector2.zero;
            return;
        }

        _currCameraOrbit.Norm = (transform.position - Target.position).normalized;
        _currCameraOrbit.Pos = Target.position + (_currCameraOrbitRadius * _currCameraOrbit.Norm);

        _cameraOrientation = Quaternion.AngleAxis(_lookInputAcc.x, Vector3.up) * Quaternion.AngleAxis(_lookInputAcc.y, _cameraOrientation * Vector3.right) * _cameraOrientation;
        _targetCameraOrbit.Norm = _cameraOrientation * -Vector3.forward;
        _targetCameraOrbit.Pos = Target.position + (TargetOrbitRadius * _targetCameraOrbit.Norm);

        Vector3 posDelta = _targetCameraOrbit.Pos - _currCameraOrbit.Pos;
        float TgntLength = posDelta.magnitude * CameraOrbitTangentFactor;

        _currCameraOrbit.Tgnt = Vector3.ProjectOnPlane(posDelta, _currCameraOrbit.Norm).normalized;
        _targetCameraOrbit.Tgnt = Vector3.ProjectOnPlane(posDelta, _targetCameraOrbit.Norm).normalized;

        _cameraSpline.Clear();
        _cameraSpline.Add(_currCameraOrbit.Pos);
        _cameraSpline.Add(Target.position + Vector3.Slerp(_currCameraOrbit.Norm, _targetCameraOrbit.Norm, 0.5f) * _currCameraOrbitRadius);
        _cameraSpline.Add(_targetCameraOrbit.Pos);

        for (int i = 0; i < 64; i++)
        {
            Vector3 startPos = _cameraSpline.EvaluatePosition(i / 64.0f);
            Vector3 endPos = _cameraSpline.EvaluatePosition((i + 1) / 64.0f);

            Debug.DrawLine(startPos, endPos, Color.red);
        }       
        Debug.DrawLine(_targetCameraOrbit.Pos, _targetCameraOrbit.Pos + (Vector3.up * 3.0f), Color.darkBlue);

        _lookInputVals.Clear();
        _cameraSplineSampleT = 0.0f;
        //CurrCameraSplineLength = CurveUtility.ApproximateLength(CameraSpline);
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
