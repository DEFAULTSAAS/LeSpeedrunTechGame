using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MouseLockingManager : MonoBehaviour
{
    public CameraController MainCamera;
    public GameObject MainGUIPanel;
    public GameObject TileMapGUIPanel;
    public GameObject HealthBar;
    public Slider MouseXSlider;
    public Slider MouseYSlider;
    public TextMeshProUGUI HoriSpeedCounterText;
    public TextMeshProUGUI VertSpeedCounterText;
    public Toggle InvertHoriButton;
    public Toggle InvertVertButton;
    public Image[] TileImages;

    private InputAction _cancelInputAction;
    private Vector2 _defaultIAF;
    private Vector2 _defaultLS;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _cancelInputAction = InputSystem.actions.FindAction("Cancel");
        _defaultIAF = MainCamera.InputAxisFactorsTPS;
        _defaultLS = MainCamera.InputAxisFactorsTPS;

        MouseXSlider.value = _defaultLS.x;
        MouseYSlider.value = _defaultLS.y;

        if (PlayerPrefs.GetFloat("MouseXSpeed") != 0.0f)
            PlayerPrefs.SetFloat("MouseXSpeed", _defaultLS.x);
        if (PlayerPrefs.GetFloat("MouseYSpeed") != 0.0f)
            PlayerPrefs.SetFloat("MouseYSpeed", _defaultLS.y);

        if (PlayerPrefs.GetInt("InvertHori") == 0)
            PlayerPrefs.SetInt("InvertHori", Convert.ToInt32(InvertHoriButton.isOn));
        if (PlayerPrefs.GetInt("InvertVert") == 0)
            PlayerPrefs.SetInt("InvertVert", Convert.ToInt32(InvertVertButton.isOn));

        MouseXSlider.value = PlayerPrefs.GetFloat("MouseXSpeed");
        MouseYSlider.value = PlayerPrefs.GetFloat("MouseYSpeed");

        InvertHoriButton.isOn = Convert.ToBoolean(PlayerPrefs.GetInt("InvertHori"));
        InvertVertButton.isOn = Convert.ToBoolean(PlayerPrefs.GetInt("InvertVert"));
    }

    // Update is called once per frame
    void Update()
    {
        if (MainGUIPanel.activeSelf)
        {
            HealthBar.SetActive(false);
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            MainCamera.InputAxisFactorsTPS.x = !InvertHoriButton.isOn ? _defaultIAF.x : _defaultIAF.x * -1.0f;
            MainCamera.InputAxisFactorsTPS.y = !InvertHoriButton.isOn ? _defaultIAF.y : _defaultIAF.y * -1.0f;

            MainCamera.LookSpeedTPS.x = MouseXSlider.value;
            MainCamera.LookSpeedFPS.x = MouseXSlider.value;

            MainCamera.LookSpeedTPS.y = MouseYSlider.value;
            MainCamera.LookSpeedFPS.y = MouseYSlider.value;
            
            PlayerPrefs.SetFloat("MouseXSpeed", MouseXSlider.value);
            PlayerPrefs.SetFloat("MouseYSpeed", MouseYSlider.value);
            
            PlayerPrefs.SetInt("InvertHori", Convert.ToInt32(InvertHoriButton.isOn));
            PlayerPrefs.SetInt("InvertVert", Convert.ToInt32(InvertVertButton.isOn));
            Cursor.lockState = CursorLockMode.Locked;   
        }

        HoriSpeedCounterText.text = MouseXSlider.value.ToString();
        VertSpeedCounterText.text = MouseYSlider.value.ToString();

        if (_cancelInputAction.WasPerformedThisFrame())
        {
            MainGUIPanel.SetActive(!MainGUIPanel.activeSelf);
            TileMapGUIPanel.SetActive(MainGUIPanel.activeSelf);
        }
    }

    public static void QuitGame()
    {
        Application.Quit();
    }
}
