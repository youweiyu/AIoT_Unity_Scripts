using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

public class Pico4DirectJoystickTCP : MonoBehaviour
{
    public string esp32IP = "192.168.223.156";
    public int esp32Port = 8080;

    private TcpClient client;
    private NetworkStream stream;

    void Start()
    {
        ConnectToESP32();
    }

    void Update()
    {
        if (client == null || !client.Connected) return;

        var gamepad = Gamepad.current;
        if (gamepad == null) return;

        // 左摇杆
        float ly = gamepad.leftStick.y.ReadValue();  // -1下 0中 1上
        float lx = gamepad.leftStick.x.ReadValue();  // -1左 0中 1右
        // 右摇杆
        float ry = gamepad.rightStick.y.ReadValue();
        float rx = gamepad.rightStick.x.ReadValue();

        // 水平映射舵机2 (水平)
        float servo2Angle = MapStickToServo(lx, rx, 20f, 90f, 160f);

        // 垂直映射舵机1 (竖直)
        float servo1Angle = MapStickToServo(ly, ry, 20f, 40f, 90f);

        // 构造 JSON 并发送
        string json = $"{{\"servo1\":{(int)servo1Angle},\"servo2\":{(int)servo2Angle}}}\n";
        SendMessage(json);
    }

    float MapStickToServo(float stick1, float stick2, float min, float mid, float max)
    {
        // 两个摇杆取平均
        float val = (stick1 + stick2) / 2f;

        // 线性映射
        if (val > 0)
            return Mathf.Lerp(mid, max, val);  // 0~1映射到 mid~max
        else
            return Mathf.Lerp(mid, min, -val); // -1~0映射到 min~mid
    }

    void ConnectToESP32()
    {
        try
        {
            client = new TcpClient();
            client.Connect(esp32IP, esp32Port);
            stream = client.GetStream();
            Debug.Log("Connected to ESP32 TCP server");
        }
        catch (System.Exception e)
        {
            Debug.LogError("TCP连接失败: " + e.Message);
        }
    }

    new void SendMessage(string message)
    {
        if (stream == null) return;
        byte[] data = Encoding.ASCII.GetBytes(message);
        stream.Write(data, 0, data.Length);
    }

    private void OnApplicationQuit()
    {
        if (stream != null) stream.Close();
        if (client != null) client.Close();
    }
}
