using System;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public bool isPause;
    public bool isWebGlBuild;
    public bool lockDevice = false;
    public TypeDevice principalDevice;
    public TypeDevice _currentDevice;
    public event Action<TypeDevice, TypeDevice> OnDeviceChanged;
    public TypeDevice currentDevice
    {
        get => _currentDevice;
        set
        {
            if (_currentDevice != value)
            {
                _currentDevice = value;
                OnDeviceChanged?.Invoke(principalDevice, _currentDevice);
            }
        }
    }
    public bool _startGame;
    public Action<bool> OnStartGame;
    public InputAction pauseButton;
    public string currentScene;
    public bool startGame
    {
        get => _startGame;
        set
        {
            if (_startGame != value)
            {
                _startGame = value;
                OnStartGame?.Invoke(_startGame);
            }
        }
    }
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            currentScene = SceneManager.GetActiveScene().name;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void OnEnable()
    {
        pauseButton.performed += PauseHandle;
        pauseButton.Enable();
    }
    void OnDisable()
    {
        pauseButton.performed -= PauseHandle;
    }
    void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        Physics2D.simulationMode = SimulationMode2D.Script;
        Physics2D.autoSyncTransforms = false;
        currentDevice = principalDevice;
        if (ManagementLoaderScene.Instance) ManagementLoaderScene.Instance.OnFinishOpenAnimation += () => { startGame = true; };
    }
    void LateUpdate()
    {
        CheckCurrentDevice();
    }
    public void PauseHandle(InputAction.CallbackContext context)
    {

    }
    public void UnloadAdditiveScene(TypeScene typeScene, TypeLoader typeLoader)
    {
        _ = UnloadAdditiveScene(typeScene, typeLoader, null, null);
    }
    public void UnloadAdditiveScene(TypeScene typeScene, GameManagerHelper.IScene sceneData, GameObject lastButtonSelected)
    {
        _ = UnloadAdditiveScene(typeScene, TypeLoader.None, sceneData, lastButtonSelected);
    }
    public async Awaitable UnloadAdditiveScene(TypeScene typeScene, TypeLoader typeLoader, GameManagerHelper.IScene sceneData, GameObject lastButtonSelected)
    {
        
    }
    public async Awaitable LoadScene(TypeScene typeScene, LoadSceneMode loadSceneMode = LoadSceneMode.Single, TypeLoader typeLoader = TypeLoader.WithProgressBar, bool consertLastScene = false)
    {
        
    }
    #region Device Validation
    void CheckCurrentDevice()
    {
        if (lockDevice) return;
        if (!isWebGlBuild)
        {
            if (ValidateDeviceIsMobile())
            {
                currentDevice = TypeDevice.MOBILE;
            }
            else if (ValidateIsGamepad())
            {
                currentDevice = TypeDevice.GAMEPAD;
            }
            else if (ValidateDeviceIsPc())
            {
                currentDevice = TypeDevice.PC;
            }
        }
        else
        {
            currentDevice = TypeDevice.PC;
        }
    }
    bool ValidateDeviceIsPc()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return false;
        bool validateAnyPcInput =
            keyboard.anyKey.wasPressedThisFrame ||
            mouse.leftButton.wasPressedThisFrame ||
            mouse.rightButton.wasPressedThisFrame ||
            mouse.scroll.ReadValue() != Vector2.zero ||
            mouse.delta.ReadValue() != Vector2.zero;
        return validateAnyPcInput;
    }
    bool ValidateIsGamepad()
    {
        var gamePad = Gamepad.current;
        if (gamePad == null || Gamepad.all.Count == 0 || !IsRealGamepadConnected()) return false;
        bool validateAnyGamepadInput =
            gamePad.buttonSouth.wasPressedThisFrame ||
            gamePad.buttonNorth.wasPressedThisFrame ||
            gamePad.buttonEast.wasPressedThisFrame ||
            gamePad.buttonWest.wasPressedThisFrame ||
            gamePad.leftStick.ReadValue().magnitude > 0.1f ||
            gamePad.rightStick.ReadValue().magnitude > 0.1f ||
            gamePad.dpad.ReadValue().magnitude > 0.1f ||
            gamePad.leftTrigger.wasPressedThisFrame ||
            gamePad.rightTrigger.wasPressedThisFrame;
        return gamePad != null && validateAnyGamepadInput && !ValidateDeviceIsPc();
    }
    bool IsRealGamepadConnected()
    {
        return Gamepad.all.Any(g =>
            g.displayName != "Gamepad" &&
            g.enabled &&
            g.wasUpdatedThisFrame
        );
    }
    bool ValidateDeviceIsMobile()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen == null) return false;
        foreach (var touch in touchscreen.touches)
        {
            if (touch.press.isPressed || touch.press.wasPressedThisFrame || touch.delta.ReadValue().magnitude > 0.01f)
                return true;
        }
        return false;
    }
    #endregion
    public enum TypeScene
    {
        HomeScene = 0,
        OptionsScene = 1,
        GameScene = 2,
        CreditsScene = 3,
        Reload = 4,
        Exit = 5,
        GameOverScene = 6,
        BattleScene = 7,
        CityScene = 8,
        DialogScene = 9,
        TestScene = 10,
        TavernScene = 11
    }
    public enum TypeLoader
    {
        None = 0,
        WithProgressBar = 1,
        BlackOut = 2
    }
    public enum TypeDevice
    {
        None,
        PC,
        GAMEPAD,
        MOBILE,
    }
}
