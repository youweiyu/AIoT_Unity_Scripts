using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

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
    public float highlightDuration = 0.5f;

    [Header("网络设置")]
    public bool useHttpFallback = true;
    public int requestTimeout = 10;
    public bool enableDebugLogs = true;

    private const string API_URL = "https://jianxia.xyz/car/control";

    private Coroutine commandCoroutine;
    private Coroutine highlightCoroutine;

    void Start()
    {
        SetupSSL();

        // 先解绑，防止重复绑定
        forwardButton.onClick.RemoveAllListeners();
        backwardButton.onClick.RemoveAllListeners();
        leftButton.onClick.RemoveAllListeners();
        rightButton.onClick.RemoveAllListeners();
        stopButton.onClick.RemoveAllListeners();

        forwardButton.onClick.AddListener(() => SendCommand("forward"));
        backwardButton.onClick.AddListener(() => SendCommand("backward"));
        leftButton.onClick.AddListener(() => SendCommand("left"));
        rightButton.onClick.AddListener(() => SendCommand("right"));
        stopButton.onClick.AddListener(() => SendCommand("stop"));
    }

    private void SetupSSL()
    {
        try
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, errors) => true;
            ServicePointManager.DefaultConnectionLimit = 100;
            ServicePointManager.Expect100Continue = false;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"SSL设置失败: {e.Message}，将尝试HTTP回退");
        }
    }


    public void SendCommand(string command)
    {
        if (string.IsNullOrEmpty(command))
        {
            Debug.LogWarning("命令为空，忽略发送");
            return;
        }

        // 保证同一时间只有一个命令协程
        if (commandCoroutine != null)
            StopCoroutine(commandCoroutine);
        commandCoroutine = StartCoroutine(PostCommandCoroutine(command));

        // 按钮高亮防抖
        if (highlightCoroutine != null)
            StopCoroutine(highlightCoroutine);
        highlightCoroutine = StartCoroutine(HighlightButton(command));
    }

    IEnumerator PostCommandCoroutine(string command)
    {
        string currentURL = API_URL;
        CommandData data = new CommandData { command = command };
        string json = string.Empty;

        try
        {
            json = JsonUtility.ToJson(data);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"JSON序列化失败: {e.Message}");
            yield break;
        }

        if (enableDebugLogs)
            Debug.Log($"发送命令: {command}, JSON: {json}");

        bool success = false;
        using (UnityWebRequest request = CreateRequest(currentURL, json))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                success = true;
                HandleSuccessResponse(command, request.downloadHandler.text);
            }
            else
            {
                HandleErrorResponse(command, request, currentURL);

                if (useHttpFallback && currentURL.StartsWith("https://"))
                {
                    currentURL = API_URL.Replace("https://", "http://");
                    if (enableDebugLogs)
                        Debug.Log($"HTTPS失败，尝试HTTP: {currentURL}");

                    using (UnityWebRequest httpRequest = CreateRequest(currentURL, json))
                    {
                        yield return httpRequest.SendWebRequest();

                        if (httpRequest.result == UnityWebRequest.Result.Success)
                        {
                            success = true;
                            HandleSuccessResponse(command, httpRequest.downloadHandler.text);
                        }
                        else
                        {
                            HandleErrorResponse(command, httpRequest, currentURL);
                        }
                    }
                }
            }
        }

        if (!success && enableDebugLogs)
        {
            Debug.LogError($"所有连接尝试失败，命令 '{command}' 发送失败");
        }
    }

    private UnityWebRequest CreateRequest(string url, string json)
    {
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Accept", "application/json");
        request.SetRequestHeader("User-Agent", "Unity-CarController/1.0");
        request.timeout = requestTimeout;

        if (url.StartsWith("https://"))
        {
            request.certificateHandler = new BypassCertificate();
        }
        return request;
    }

    private void HandleSuccessResponse(string command, string response)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"命令发送成功: {command}");
            Debug.Log($"服务器响应: {response}");
        }
        // 可扩展：解析响应、更新UI等
    }

    private void HandleErrorResponse(string command, UnityWebRequest request, string url)
    {
        Debug.LogError($"命令发送失败: {command}");
        Debug.LogError($"URL: {url}");
        Debug.LogError($"错误: {request.error}");
        Debug.LogError($"响应码: {request.responseCode}");

        if (enableDebugLogs && request.downloadHandler != null)
        {
            Debug.LogError($"响应内容: {request.downloadHandler.text}");
        }

        if (request.error != null)
        {
            if (request.error.Contains("SSL") || request.error.Contains("certificate"))
                Debug.LogWarning("SSL证书错误，建议启用HTTP回退");
            else if (request.error.Contains("timeout"))
                Debug.LogWarning("请求超时，考虑增加timeout时间或检查网络连接");
            else if (request.error.Contains("Cannot resolve"))
                Debug.LogWarning("域名解析失败，检查网络连接和DNS设置");
        }
    }

    IEnumerator HighlightButton(string command)
    {
        Button activeButton = command switch
        {
            "forward" => forwardButton,
            "backward" => backwardButton,
            "left" => leftButton,
            "right" => rightButton,
            "stop" => stopButton,
            _ => null
        };

        if (activeButton == null) yield break;

        Image buttonImage = activeButton.GetComponent<Image>();
        if (buttonImage == null) yield break;

        Color originalColor = buttonImage.color;
        buttonImage.color = activeColor;
        yield return new WaitForSeconds(highlightDuration);
        buttonImage.color = originalColor;
    }

    void OnDestroy()
    {
        StopAllCoroutines();
        // 解绑事件，防止内存泄漏
        forwardButton.onClick.RemoveAllListeners();
        backwardButton.onClick.RemoveAllListeners();
        leftButton.onClick.RemoveAllListeners();
        rightButton.onClick.RemoveAllListeners();
        stopButton.onClick.RemoveAllListeners();
    }

    [System.Serializable]
    private class CommandData
    {
        public string command;
    }

    // 跳过SSL证书验证
    private class BypassCertificate : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData) => true;
    }
}
