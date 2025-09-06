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

    [Header("TCP 设置")]
    public string serverIP = "192.168.223.238";
    public int serverPort = 8081;

    [Header("心跳设置")]
    [Tooltip("心跳间隔时间（秒）")]
    public float heartbeatInterval = 5f;

    private TcpClient tcpClient;
    private NetworkStream stream;
    private bool connected = false;
    private Coroutine heartbeatCoroutine;

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

        ResetAllButtonColors();
    }

    void Update()
    {
        // 键盘控制
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) SendCommand("1"); // 前进
            if (keyboard.sKey.isPressed) SendCommand("2"); // 后退
            if (keyboard.aKey.isPressed) SendCommand("3"); // 左转
            if (keyboard.dKey.isPressed) SendCommand("4"); // 右转
            if (keyboard.spaceKey.isPressed) SendCommand("0"); // 停止
        }

        // 获取手柄设备
        if (rightHandDevices.Count == 0) InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightHandDevices);
        if (leftHandDevices.Count == 0) InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftHandDevices);

        // Pico 4 Ultra ABXY 映射（修改后的）
        if (GetButton(rightHandDevices, XRCommonUsages.primaryButton)) SendCommand("1");   // 右手 A → 前进
        if (GetButton(leftHandDevices, XRCommonUsages.primaryButton)) SendCommand("2");    // 左手 X → 后退
        if (GetButton(leftHandDevices, XRCommonUsages.secondaryButton)) SendCommand("3");  // 左手 Y → 左转
        if (GetButton(rightHandDevices, XRCommonUsages.secondaryButton)) SendCommand("4"); // 右手 B → 右转

        // Grip 停止
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

            HighlightButton(command);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"发送命令失败: {e.Message}");
            connected = false;
            await Connect();
        }
    }

    private void HighlightButton(string command)
    {
        ResetAllButtonColors();

        Button b = command switch
        {
            "1" => forwardButton,
            "2" => backwardButton,
            "3" => leftButton,
            "4" => rightButton,
            "0" => stopButton,
            _ => null
        };

        if (b != null)
        {
            var img = b.GetComponent<Image>();
            img.color = activeColor;
        }
    }

    private void ResetAllButtonColors()
    {
        forwardButton.GetComponent<Image>().color = normalColor;
        backwardButton.GetComponent<Image>().color = normalColor;
        leftButton.GetComponent<Image>().color = normalColor;
        rightButton.GetComponent<Image>().color = normalColor;
        stopButton.GetComponent<Image>().color = normalColor;
    }

    private IEnumerator HeartbeatLoop()
    {
        while (connected && stream != null)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes("heartbeat");
                stream.Write(data, 0, data.Length);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"心跳发送失败: {e.Message}");
                connected = false;
                _ = Connect();
                yield break;
            }
            yield return new WaitForSeconds(heartbeatInterval);
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
