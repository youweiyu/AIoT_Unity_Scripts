using UnityEngine;
using UnityEngine.InputSystem;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public class ServoUIController_UDLR : MonoBehaviour
{
    public string serverIP = "192.168.223.238";
    public int serverPort = 8082;

    private TcpClient tcpClient;
    private NetworkStream stream;
    private bool connected = false;

    public int servoTopID = 9;
    public int servoTopMin = -5;
    public int servoTopMax = 50;

    public int servoBaseID = 10;
    public int servoBaseMin = 30;
    public int servoBaseMax = 150;

    public int stepAngle = 1;

    private int currentTopAngle = 20;
    private int currentBaseAngle = 90;

    // 上一次按键状态
    private bool upPrev, downPrev, leftPrev, rightPrev;

    private void Start()
    {
        Connect();
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
            Debug.Log($"成功连接舵机服务器 {serverIP}:{serverPort}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"连接舵机服务器失败: {e.Message}");
            connected = false;
        }
    }

    private void Update()
    {
        var gamepad = Gamepad.current;
        var keyboard = Keyboard.current;

        bool upPressed = false;
        bool downPressed = false;
        bool leftPressed = false;
        bool rightPressed = false;

        if (gamepad != null)
        {
            upPressed |= gamepad.dpad.up.isPressed;
            downPressed |= gamepad.dpad.down.isPressed;
            leftPressed |= gamepad.dpad.left.isPressed;
            rightPressed |= gamepad.dpad.right.isPressed;
        }

        if (keyboard != null)
        {
            upPressed |= keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed;
            downPressed |= keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed;
            leftPressed |= keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed;
            rightPressed |= keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed;
        }

        // 仅在按下瞬间触发
        if (upPressed && !upPrev) { currentTopAngle += stepAngle; SendServoCommand(servoTopID, currentTopAngle); }
        if (downPressed && !downPrev) { currentTopAngle -= stepAngle; SendServoCommand(servoTopID, currentTopAngle); }
        if (leftPressed && !leftPrev) { currentBaseAngle -= stepAngle; SendServoCommand(servoBaseID, currentBaseAngle); }
        if (rightPressed && !rightPrev) { currentBaseAngle += stepAngle; SendServoCommand(servoBaseID, currentBaseAngle); }

        upPrev = upPressed;
        downPrev = downPressed;
        leftPrev = leftPressed;
        rightPrev = rightPressed;

        currentTopAngle = Mathf.Clamp(currentTopAngle, servoTopMin, servoTopMax);
        currentBaseAngle = Mathf.Clamp(currentBaseAngle, servoBaseMin, servoBaseMax);
    }

    private async void SendServoCommand(int servoID, int angle)
    {
        if (!connected || stream == null)
        {
            Debug.LogWarning("舵机未连接，尝试重连...");
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
            Debug.LogError($"发送舵机命令失败: {e.Message}");
            connected = false;
            Connect();
        }
    }

    private void OnApplicationQuit()
    {
        if (stream != null) stream.Close();
        if (tcpClient != null) tcpClient.Close();
        connected = false;
    }
}
