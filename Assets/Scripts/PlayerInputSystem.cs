using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerActions
{
    Jump,
    LongJump,
    Backflip,
    LeftSide,
    RightSide,
    ForwardLeftSide,
    ForwardRightSide,
    Slam,
    Charge,
}

public struct InputActionState
{
    public string ActionName;
    public float ActionTime;
    public Vector2 AxisValue;
    public bool HasChanged;
    public bool IsPersistent;
}

public class PlayerInputSystem : MonoBehaviour
{
    public static PlayerInputSystem MainPISInstance;

    // The amount of time in seconds, before a input action state is removed and no longer processed for player actions.
    public float InputActionStateRemovalDelay = 0.0167f; 
    public string MoveInputActionName = "Move";

    public string[] PersistentInputActions = new string[]{"Move"};

    private List<InputActionState> _inputActionStates = new(); 
    private List<PlayerActions> _playerActionBuffer = new();

    void Awake()
    {
        if (MainPISInstance == null)
            MainPISInstance = this;
        else if (MainPISInstance != this)
            Destroy(gameObject);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        _inputActionStates.RemoveAll(inputActionState => (Time.time - inputActionState.ActionTime) > 
                                     InputActionStateRemovalDelay && !inputActionState.IsPersistent);
    }

    public void ProcessInputActionStates()
    {
        
    }

    public void HandleInputCallback(InputAction.CallbackContext context)
    {
        switch(context.phase)
        {
            case InputActionPhase.Performed:
            {
                bool actionStateFound = false;
                for (int i = 0; i < _inputActionStates.Count; i++)
                {
                    if (_inputActionStates[i].ActionName != context.action.name)
                        continue;
                    actionStateFound = true;
                    
                    InputActionState inputActionState = _inputActionStates[i];
                    inputActionState.ActionTime = Time.time;
                    inputActionState.AxisValue = (context.action.expectedControlType == "Vector2") ? 
                                                  context.action.ReadValue<Vector2>() : Vector2.one * float.NegativeInfinity;
                    inputActionState.HasChanged = GetIsPersistent(context.action.name);

                    _inputActionStates[i] = inputActionState;
                }

                if (!actionStateFound)
                {
                    InputActionState inputActionState;
                    inputActionState.ActionName = context.action.name;
                    inputActionState.ActionTime = Time.time;
                    inputActionState.AxisValue = (context.action.expectedControlType == "Vector2") ? 
                                                  context.action.ReadValue<Vector2>() : Vector2.one * float.NegativeInfinity;
                    inputActionState.HasChanged = false;
                    inputActionState.IsPersistent = GetIsPersistent(context.action.name);

                    _inputActionStates.Add(inputActionState);
                }
            } break;
            case InputActionPhase.Canceled:
            {
                for (int i = 0; i < _inputActionStates.Count; i++)
                {
                    if (_inputActionStates[i].ActionName == context.action.name)
                    {
                        _inputActionStates.RemoveAt(i);
                        break;   
                    }    
                }
            } break;
        }
    }

    private bool GetIsPersistent(string inActionName)
    {
        return PersistentInputActions.Contains(inActionName);
    }
}
