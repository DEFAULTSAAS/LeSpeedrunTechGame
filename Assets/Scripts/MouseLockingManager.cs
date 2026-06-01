using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLockingManager : MonoBehaviour
{
    private InputAction CancelInputAction;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        CancelInputAction = InputSystem.actions.FindAction("Cancel");
    }

    // Update is called once per frame
    void Update()
    {
        if (CancelInputAction.WasPerformedThisFrame())
        {
            switch (Cursor.lockState)
            {
                case CursorLockMode.None: Cursor.lockState = CursorLockMode.Locked; break;
                case CursorLockMode.Locked: Cursor.lockState = CursorLockMode.None; break;
            }
        }
    }
}
