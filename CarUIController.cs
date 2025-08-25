using UnityEngine;
using UnityEngine.UI;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR;

public class CarUIController_TCP : MonoBehaviour
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
    public string serverIP = "192.168.223.156"; // ESP32 IP
    public int serverPort = 8080;

    private TcpClient tcpClient;
    private NetworkStream stream;
    private Coroutine highlightCoroutine;
    private List<InputDevice> rightHandDevices = new List<InputDevice>();
    private List<InputDevice> leftHandDevices = new List<InputDevice>();
    private bool isStopped = false;

    async void Start()
    {
        tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(serverIP, serverPort);
        stream = tcpClient.GetStream();

        forwardButton.onClick.AddListener(() => SendCommand("{\"act\":0}"));
        backwardButton.onClick.AddListener(() => SendCommand("{\"act\":1}"));
        rightButton.onClick.AddListener(() => SendCommand("{\"act\":2}"));
        leftButton.onClick.AddListener(() => SendCommand("{\"act\":3}"));
        stopButton.onClick.AddListener(() => SendCommand("{\"act\":6}"));
    }

    void Update()
    {
        if (rightHandDevices.Count == 0) InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightHandDevices);
        if (leftHandDevices.Count == 0) InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftHandDevices);

        if(GetButton(rightHandDevices, CommonUsages.primary2DAxisClick)){
            if(!isStopped){ isStopped=true; SendCommand("{\"act\":6}"); ResetButtonColors();}
            return;
        }

        bool anyDirection=false;
        if(GetButton(rightHandDevices, CommonUsages.primaryButton)) { SendCommand("{\"act\":0}"); anyDirection=true; }
        if(GetButton(leftHandDevices, CommonUsages.primaryButton)) { SendCommand("{\"act\":1}"); anyDirection=true; }
        if(GetButton(rightHandDevices, CommonUsages.secondaryButton)) { SendCommand("{\"act\":2}"); anyDirection=true; }
        if(GetButton(leftHandDevices, CommonUsages.secondaryButton)) { SendCommand("{\"act\":3}"); anyDirection=true; }
        if(anyDirection) isStopped=false;
    }

    private bool GetButton(List<InputDevice> devices, UnityEngine.XR.InputFeatureUsage<bool> button){
        foreach(var d in devices)
            if(d.TryGetFeatureValue(button, out bool pressed) && pressed) return true;
        return false;
    }

    private void ResetButtonColors(){
        forwardButton.GetComponent<Image>().color=normalColor;
        backwardButton.GetComponent<Image>().color=normalColor;
        leftButton.GetComponent<Image>().color=normalColor;
        rightButton.GetComponent<Image>().color=normalColor;
        stopButton.GetComponent<Image>().color=normalColor;
    }

    public void SendCommand(string json){
        if(stream==null) return;
        byte[] data = Encoding.UTF8.GetBytes(json+"\n");
        stream.Write(data,0,data.Length);

        if(highlightCoroutine!=null) StopCoroutine(highlightCoroutine);
        highlightCoroutine = StartCoroutine(HighlightButton(json));
    }

    IEnumerator HighlightButton(string json){
        string cmd="";
        if(json.Contains("\"act\":0")) cmd="forward";
        else if(json.Contains("\"act\":1")) cmd="backward";
        else if(json.Contains("\"act\":2")) cmd="right";
        else if(json.Contains("\"act\":3")) cmd="left";
        else if(json.Contains("\"act\":6")) cmd="stop";

        Button b = cmd switch{
            "forward"=>forwardButton,
            "backward"=>backwardButton,
            "left"=>leftButton,
            "right"=>rightButton,
            "stop"=>stopButton,
            _=>null
        };
        if(b==null) yield break;
        var img=b.GetComponent<Image>();
        Color orig=img.color;
        img.color=activeColor;
        yield return new WaitForSeconds(highlightDuration);
        img.color=orig;
    }

    private void OnDestroy(){
        forwardButton.onClick.RemoveAllListeners();
        backwardButton.onClick.RemoveAllListeners();
        leftButton.onClick.RemoveAllListeners();
        rightButton.onClick.RemoveAllListeners();
        stopButton.onClick.RemoveAllListeners();

        stream?.Close();
        tcpClient?.Close();
    }
}
