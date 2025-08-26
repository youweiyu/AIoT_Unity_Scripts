using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // 新 Input System
using System.Net.Sockets;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR;
using System.Threading.Tasks;

using XRDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

public class CarUIController : MonoBehaviour
{
    [Header("控制按钮")]
    public Button forwardButton;
    public Button backwardButton;
    public Button leftButton;
    public Button rightButton;
    public Button stopButton;

    [Header("视觉反馈")]
    public Color normalColor = Color.white;
    public Color activeColor = Color.green;
    public float highlightDuration = 0.3f;

    [Header("TCP 设置")]
    public string serverIP = "192.168.223.238";
    public int serverPort = 8081;

    [Header("心跳设置")]
    [Tooltip("心跳间隔时间（秒）")]
    public float heartbeatInterval = 5f;   // Inspector 可调心跳间隔

    private TcpClient tcpClient;
    private NetworkStream stream;
    private bool connected = false;
    private Coroutine highlightCoroutine;
    private Coroutine heartbeatCoroutine;   // 心跳协程

    private List<XRDevice> rightHandDevices = new List<XRDevice>();
    private List<XRDevice> leftHandDevices = new List<XRDevice>();

    async void Start()
    {
        await Connect();

        // UI 按钮绑定
        forwardButton.onClick.AddListener(() => SendCommand("1"));
        backwardButton.onClick.AddListener(() => SendCommand("2"));
        leftButton.onClick.AddListener(() => SendCommand("3"));
        rightButton.onClick.AddListener(() => SendCommand("4"));
        stopButton.onClick.AddListener(() => SendCommand("0"));
    }

    void Update()
    {
        // 键盘控制（新 Input System）
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) SendCommand("1");
            if (keyboard.sKey.isPressed) SendCommand("2");
            if (keyboard.aKey.isPressed) SendCommand("3");
            if (keyboard.dKey.isPressed) SendCommand("4");
            if (keyboard.spaceKey.isPressed) SendCommand("0");
        }

        // 获取手柄设备
        if (rightHandDevices.Count == 0) InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightHandDevices);
        if (leftHandDevices.Count == 0) InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftHandDevices);

        // Pico 4 Ultra ABXY 映射
        if (GetButton(rightHandDevices, XRCommonUsages.primaryButton)) SendCommand("1");   // A 前进
        if (GetButton(rightHandDevices, XRCommonUsages.secondaryButton)) SendCommand("2"); // B 后退
        if (GetButton(leftHandDevices, XRCommonUsages.primaryButton)) SendCommand("3");    // X 左转
        if (GetButton(leftHandDevices, XRCommonUsages.secondaryButton)) SendCommand("4");  // Y 右转

        // 左右 Grip 控制停止
        if (GetButton(rightHandDevices, XRCommonUsages.gripButton) ||
            GetButton(leftHandDevices, XRCommonUsages.gripButton))
        {
            SendCommand("0"); // stop
        }
    }

    private bool GetButton(List<XRDevice> devices, InputFeatureUsage<bool> button)
    {
        foreach (var d in devices)
            if (d.TryGetFeatureValue(button, out bool pressed) && pressed) return true;
        return false;
    }

    private async Task<bool> Connect()
    {
        try
        {
            tcpClient?.Close();
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(serverIP, serverPort);
            stream = tcpClient.GetStream();
            connected = true;
            Debug.Log($"成功连接到小车控制服务器 {serverIP}:{serverPort}");

            // 启动心跳协程
            if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = StartCoroutine(HeartbeatLoop());

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"连接服务器失败: {e.Message}");
            connected = false;
            return false;
        }
    }

    // 发送命令，不等待服务器响应
    public async void SendCommand(string command)
    {
        if (!connected || stream == null)
        {
            Debug.LogWarning("未连接到服务器，尝试重连...");
            if (!await Connect()) return;
        }

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(command + "\n");
            await stream.WriteAsync(data, 0, data.Length);

            // 按钮高亮
            if (highlightCoroutine != null) StopCoroutine(highlightCoroutine);
            highlightCoroutine = StartCoroutine(HighlightButton(command));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"发送命令失败: {e.Message}");
            connected = false;
            await Connect();
        }
    }

    private IEnumerator HighlightButton(string command)
    {
        Button b = command switch
        {
            "1" => forwardButton,
            "2" => backwardButton,
            "3" => leftButton,
            "4" => rightButton,
            "0" => stopButton,
            _ => null
        };
        if (b == null) yield break;

        var img = b.GetComponent<Image>();
        Color orig = img.color;
        img.color = activeColor;
        yield return new WaitForSeconds(highlightDuration);
        img.color = orig;
    }

    private IEnumerator HeartbeatLoop()
    {
        while (connected && stream != null)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes("HEARTBEAT\n");
                stream.Write(data, 0, data.Length);
                // Debug.Log("发送心跳包");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"心跳发送失败: {e.Message}");
                connected = false;
                _ = Connect(); // 尝试重连
                yield break;
            }
            yield return new WaitForSeconds(heartbeatInterval); // 使用 Inspector 参数
        }
    }

    private void OnDestroy()
    {
        forwardButton.onClick.RemoveAllListeners();
        backwardButton.onClick.RemoveAllListeners();
        leftButton.onClick.RemoveAllListeners();
        rightButton.onClick.RemoveAllListeners();
        stopButton.onClick.RemoveAllListeners();

        if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);

        stream?.Close();
        tcpClient?.Close();
        connected = false;
        Debug.Log("已断开与服务器的连接");
    }
}
