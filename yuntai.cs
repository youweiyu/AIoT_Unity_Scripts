using UnityEngine;
using UnityEngine.InputSystem;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine.XR;

using XRCommonUsages = UnityEngine.XR.CommonUsages;  // 给 XR CommonUsages 起别名

public class ServoUIController_UDLR : MonoBehaviour
{
    [Header("TCP 设置")]
    public string serverIP = "192.168.223.238";
    public int serverPort = 8082;

    [Header("舵机参数")]
    public int servoTopID = 9;
    public int servoTopMin = -3;
    public int servoTopMax = 50;

    public int servoBaseID = 10;
    public int servoBaseMin = 30;
    public int servoBaseMax = 150;

    [Header("初始复位角度")]
    public int initTopAngle = -3;
    public int initBaseAngle = 60;

    [Header("控制参数")]
    public int stepAngle = 1;              // 按键单步移动
    public int joystickStepAngle = 20;     // 摇杆单次移动角度
    public float joystickDeadzone = 0.7f;  // 摇杆触发阈值

    [Header("心跳设置")]
    [Tooltip("心跳间隔时间（秒）")]
    public float heartbeatInterval = 2f;

    private TcpClient tcpClient;
    private NetworkStream stream;
    private bool connected = false;

    private int currentTopAngle;
    private int currentBaseAngle;

    // 记录摇杆上次的方向，避免连续触发
    private bool stickUpPressed = false;
    private bool stickDownPressed = false;
    private bool stickLeftPressed = false;
    private bool stickRightPressed = false;

    private void Start()
    {
        currentTopAngle = initTopAngle;
        currentBaseAngle = initBaseAngle;

        Connect();
        StartCoroutine(HeartbeatCoroutine());
    }

    private async void Connect()
    {
        try
        {
            tcpClient?.Close();
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(serverIP, serverPort);
            stream = tcpClient.GetStream();
            connected = true;
            Debug.Log($"✅ 成功连接舵机服务器 {serverIP}:{serverPort}");

            // 复位舵机
            SendServoCommand(servoTopID, currentTopAngle);
            SendServoCommand(servoBaseID, currentBaseAngle);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 连接舵机服务器失败: {e.Message}");
            connected = false;
        }
    }

    private void Update()
    {
        var keyboard = Keyboard.current;

        // --- Keyboard 输入 ---
        if (keyboard != null)
        {
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
            {
                currentTopAngle += stepAngle;
                currentTopAngle = Mathf.Clamp(currentTopAngle, servoTopMin, servoTopMax);
                SendServoCommand(servoTopID, currentTopAngle);
            }
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
            {
                currentTopAngle -= stepAngle;
                currentTopAngle = Mathf.Clamp(currentTopAngle, servoTopMin, servoTopMax);
                SendServoCommand(servoTopID, currentTopAngle);
            }
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
            {
                currentBaseAngle -= stepAngle;
                currentBaseAngle = Mathf.Clamp(currentBaseAngle, servoBaseMin, servoBaseMax);
                SendServoCommand(servoBaseID, currentBaseAngle);
            }
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
            {
                currentBaseAngle += stepAngle;
                currentBaseAngle = Mathf.Clamp(currentBaseAngle, servoBaseMin, servoBaseMax);
                SendServoCommand(servoBaseID, currentBaseAngle);
            }
        }

        // --- XR Controller 输入（Pico4 手柄翻转摇杆） ---
        var leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHand.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out Vector2 leftStick))
        {
            // 上（翻转）
            if (leftStick.y > joystickDeadzone && !stickUpPressed)
            {
                currentTopAngle -= joystickStepAngle;
                currentTopAngle = Mathf.Clamp(currentTopAngle, servoTopMin, servoTopMax);
                SendServoCommand(servoTopID, currentTopAngle);
                stickUpPressed = true;
            }
            else if (leftStick.y <= joystickDeadzone)
            {
                stickUpPressed = false;
            }

            // 下（翻转）
            if (leftStick.y < -joystickDeadzone && !stickDownPressed)
            {
                currentTopAngle += joystickStepAngle;
                currentTopAngle = Mathf.Clamp(currentTopAngle, servoTopMin, servoTopMax);
                SendServoCommand(servoTopID, currentTopAngle);
                stickDownPressed = true;
            }
            else if (leftStick.y >= -joystickDeadzone)
            {
                stickDownPressed = false;
            }

            // 右（翻转）
            if (leftStick.x > joystickDeadzone && !stickRightPressed)
            {
                currentBaseAngle -= joystickStepAngle;
                currentBaseAngle = Mathf.Clamp(currentBaseAngle, servoBaseMin, servoBaseMax);
                SendServoCommand(servoBaseID, currentBaseAngle);
                stickRightPressed = true;
            }
            else if (leftStick.x <= joystickDeadzone)
            {
                stickRightPressed = false;
            }

            // 左（翻转）
            if (leftStick.x < -joystickDeadzone && !stickLeftPressed)
            {
                currentBaseAngle += joystickStepAngle;
                currentBaseAngle = Mathf.Clamp(currentBaseAngle, servoBaseMin, servoBaseMax);
                SendServoCommand(servoBaseID, currentBaseAngle);
                stickLeftPressed = true;
            }
            else if (leftStick.x >= -joystickDeadzone)
            {
                stickLeftPressed = false;
            }
        }
    }

    private async void SendServoCommand(int servoID, int angle)
    {
        if (!connected || stream == null)
        {
            Debug.LogWarning("⚠️ 舵机未连接，尝试重连...");
            await Task.Delay(100);
            Connect();
            return;
        }

        try
        {
            string command = $"{servoID} {angle}\n";
            byte[] data = Encoding.UTF8.GetBytes(command);
            await stream.WriteAsync(data, 0, data.Length);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 发送舵机命令失败: {e.Message}");
            connected = false;
            Connect();
        }
    }

    private IEnumerator HeartbeatCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(heartbeatInterval);

            if (connected && stream != null)
            {
                try
                {
                    byte[] data = Encoding.UTF8.GetBytes("heartbeat");
                    stream.Write(data, 0, data.Length);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"⚠️ 心跳失败: {e.Message}");
                    connected = false;
                    Connect();
                }
            }
        }
    }

    private void OnApplicationQuit()
    {
        if (stream != null) stream.Close();
        if (tcpClient != null) tcpClient.Close();
        connected = false;
    }
}
