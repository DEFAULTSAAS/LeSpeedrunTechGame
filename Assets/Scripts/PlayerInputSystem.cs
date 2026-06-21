using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerInputActionTypes
{
    None,
    Move,
    Jump,
    Crouch,
    Strafing
}

public enum PlayerActions
{
    None,
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
    public PlayerInputActionTypes ActionType;
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
    public string JumpInputActionName = "Jump";
    public string CrouchInputActionName = "Crouch";
    public string StrafingInputActionName = "Sprint";

    public PlayerInputActionTypes[] PersistentInputActions = new PlayerInputActionTypes[]{PlayerInputActionTypes.Move};

    private Dictionary<string, PlayerInputActionTypes> _actionNameToActionType = new();
    private List<Tuple<PlayerInputActionTypes[], Func<List<InputActionState>, PlayerActions>>> _inputActionsToPlayerAction = new(); 
    private List<PlayerInputActionTypes> _playerInputActionTypes = new();
    private List<InputActionState> _inputActionStates = new(); 
    private List<PlayerActions> _playerActionBuffer = new();
    
    void Awake()
    {
        if (MainPISInstance == null)
            MainPISInstance = this;
        else if (MainPISInstance != this)
            Destroy(gameObject);

        _actionNameToActionType[MoveInputActionName] = PlayerInputActionTypes.Move;
        _actionNameToActionType[JumpInputActionName] = PlayerInputActionTypes.Jump;
        _actionNameToActionType[CrouchInputActionName] = PlayerInputActionTypes.Crouch;
        _actionNameToActionType[StrafingInputActionName] = PlayerInputActionTypes.Strafing;

        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Jump}, (_) => {return PlayerActions.Jump;}));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move, 
                                                   PlayerInputActionTypes.Jump, 
                                                   PlayerInputActionTypes.Crouch}, 
                                                   (states) => {
            if (_inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)].AxisValue.y > 0.0f)
                return PlayerActions.LongJump;
            return PlayerActions.None;                                           
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move, 
                                                   PlayerInputActionTypes.Jump, 
                                                   PlayerInputActionTypes.Crouch}, 
                                                   (states) => {
            if (_inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)].AxisValue.y < 0.0f)
                return PlayerActions.Backflip;
            return PlayerActions.None;                                           
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move, 
                                                   PlayerInputActionTypes.Jump, 
                                                   PlayerInputActionTypes.Strafing}, 
                                                   (states) => {
            if (_inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)].AxisValue.y < 0.0f)
                return PlayerActions.Backflip;
            return PlayerActions.None;                                           
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move,
                                                   PlayerInputActionTypes.Jump,
                                                   PlayerInputActionTypes.Crouch},
                                                   (states) => {
            InputActionState state = _inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)];
            if (state.AxisValue.x < 0.0f && state.AxisValue.y == 0.0f)
                return PlayerActions.LeftSide;
            return PlayerActions.None;                                       
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move,
                                                   PlayerInputActionTypes.Jump,
                                                   PlayerInputActionTypes.Strafing},
                                                   (states) => {
            InputActionState state = _inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)];
            if (state.AxisValue.x < 0.0f && state.AxisValue.y == 0.0f)
                return PlayerActions.LeftSide;
            return PlayerActions.None;                                       
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move,
                                                   PlayerInputActionTypes.Jump,
                                                   PlayerInputActionTypes.Crouch},
                                                   (states) => {
            InputActionState state = _inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)];
            if (state.AxisValue.x > 0.0f && state.AxisValue.y == 0.0f)
                return PlayerActions.RightSide;
            return PlayerActions.None;                                       
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move,
                                                   PlayerInputActionTypes.Jump,
                                                   PlayerInputActionTypes.Strafing}, 
                                                   (states) => {
            InputActionState state = _inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)];
            if (state.AxisValue.x > 0.0f && state.AxisValue.y == 0.0f)
                return PlayerActions.RightSide;
            return PlayerActions.None;                                       
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move,
                                                   PlayerInputActionTypes.Jump,
                                                   PlayerInputActionTypes.Crouch},
                                                   (states) => {
            InputActionState state = _inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)];
            if (state.AxisValue.x < 0.0f && state.AxisValue.y > 0.0f && state.HasChanged)
                return PlayerActions.ForwardLeftSide;
            return PlayerActions.None;                                       
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move,
                                                   PlayerInputActionTypes.Jump,
                                                   PlayerInputActionTypes.Strafing}, 
                                                   (states) => {
            InputActionState state = _inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)];
            if (state.AxisValue.x < 0.0f && state.AxisValue.y > 0.0f)
                return PlayerActions.ForwardLeftSide;
            return PlayerActions.None;                                       
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move,
                                                   PlayerInputActionTypes.Jump,
                                                   PlayerInputActionTypes.Crouch}, 
                                                   (states) => {
            InputActionState state = _inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)];
            if (state.AxisValue.x > 0.0f && state.AxisValue.y > 0.0f && state.HasChanged)
                return PlayerActions.ForwardRightSide;
            return PlayerActions.None;                                       
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move,
                                                   PlayerInputActionTypes.Jump,
                                                   PlayerInputActionTypes.Strafing},
                                                   (states) => {
            InputActionState state = _inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)];
            if (state.AxisValue.x > 0.0f && state.AxisValue.y > 0.0f)
                return PlayerActions.ForwardRightSide;
            return PlayerActions.None;                                       
        }));
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
        _playerActionBuffer.Clear();
        _playerInputActionTypes.Clear();
        _inputActionStates.Sort((a, b) => {return a.ActionType.CompareTo(b.ActionType);});

        foreach (InputActionState state in _inputActionStates)
            _playerInputActionTypes.Add(state.ActionType);

        foreach (var dualAction in _inputActionsToPlayerAction)
        {
            if (_playerInputActionTypes.Count < dualAction.Item1.Length)
                continue;

            int counter = 0;
            for (int i = 0; i < dualAction.Item1.Length; i++)
            {
                if (dualAction.Item1[i] == _playerInputActionTypes[i])
                    counter++;
            }

            PlayerActions playerAction = dualAction.Item2(_inputActionStates);
            if (counter == dualAction.Item1.Length && playerAction != PlayerActions.None)
                _playerActionBuffer.Add(playerAction);
        }
        _playerActionBuffer.Distinct();
        _playerActionBuffer.Sort((a, b) => { return b.CompareTo(a); });

        for (int i = 0; i < _inputActionStates.Count; i++)
        {
            InputActionState inputActionState = _inputActionStates[i];
            inputActionState.HasChanged = false;
            _inputActionStates[i] = inputActionState;
        }

        if (_playerActionBuffer.Count > 0)
        {
            Debug.Log(_playerActionBuffer[0]);
        }
        //Debug.Log(string.Join(',', _playerInputActionTypes));   
    }

    public void HandleInputCallback(InputAction.CallbackContext context)
    {
        PlayerInputActionTypes actionType = PlayerInputActionTypes.None;
        if (_actionNameToActionType.ContainsKey(context.action.name))
            actionType = _actionNameToActionType[context.action.name];
        else
            return;

        switch(context.phase)
        {
            case InputActionPhase.Performed:
            {
                bool actionStateFound = false;
                for (int i = 0; i < _inputActionStates.Count; i++)
                {
                    if (_inputActionStates[i].ActionType != actionType)
                        continue;
                    actionStateFound = true;
                    
                    InputActionState inputActionState = _inputActionStates[i];
                    inputActionState.ActionTime = Time.time;
                    inputActionState.AxisValue = (context.action.expectedControlType == "Vector2") ? 
                                                  context.action.ReadValue<Vector2>() : Vector2.one * float.NegativeInfinity;
                    inputActionState.HasChanged = GetIsPersistent(actionType);

                    _inputActionStates[i] = inputActionState;
                }

                if (!actionStateFound)
                {
                    InputActionState inputActionState;
                    inputActionState.ActionType = actionType;
                    inputActionState.ActionTime = Time.time;
                    inputActionState.AxisValue = (context.action.expectedControlType == "Vector2") ? 
                                                  context.action.ReadValue<Vector2>() : Vector2.one * float.NegativeInfinity;
                    inputActionState.HasChanged = false;
                    inputActionState.IsPersistent = GetIsPersistent(actionType);

                    _inputActionStates.Add(inputActionState);
                }
            } break;
            case InputActionPhase.Canceled:
            {
                for (int i = 0; i < _inputActionStates.Count; i++)
                {
                    if (_inputActionStates[i].ActionType == actionType)
                    {
                        _inputActionStates.RemoveAt(i);
                        break;   
                    }    
                }
            } break;
        }
    }

    private bool GetIsPersistent(PlayerInputActionTypes inActionType)
    {
        return PersistentInputActions.Contains(inActionType);
    }
}
