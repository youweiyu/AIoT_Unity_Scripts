using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

public class AIAssistant : MonoBehaviour
{
    [Header("UI组件")]
    public Button aiAssistantButton;
    public RawImage cameraPreview;
    public TMP_Text speciesText;
    public TMP_Text introductionText;
    public TMP_Text analysisText;

    [Header("ip设置")]
    public string esp32IP = "192.168.223.238";
    public int esp32Port = 8080;

    public string analysisAPI = "";
    public float timeout = 30f;

    [Header("视觉反馈")]
    public Color normalColor = Color.white;
    public Color processingColor = Color.yellow;
    public Color successColor = Color.green;
    public Color errorColor = Color.red;
    public Color uploadSuccessColor = Color.blue; // 上传成功提示颜色
    public float highlightDuration = 0.5f;

    // ===== Coze API 配置 =====
    const string COZE_API_KEY = "pat_amzSXxLKssT22HvzRswyVMKoco0R4QfKSdT1nwX09JTzoGKHV3qzaAxJwI8U7Cz6";
    const string COZE_FILE_UPLOAD_URL = "https://api.coze.cn/v1/files/upload";
    const string COZE_CHAT_BASE = "https://api.coze.cn/v3";
    const string COZE_BOT_ID = "7531000023556046875";

    // ===== TCP 接收 =====
    private TcpClient client;
    private NetworkStream stream;
    private Thread receiveThread;
    private volatile bool running = false;
    private readonly ConcurrentQueue<byte[]> frameQueue = new ConcurrentQueue<byte[]>();

    // ===== 纹理/截图 =====
    private Texture2D cameraTexture;
    private Texture2D currentScreenshot;
    private bool isProcessing = false;

    void Start()
    {
        cameraTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        cameraPreview.texture = cameraTexture;

        // 水平翻转摄像头画面
        cameraPreview.uvRect = new Rect(1, 0, -1, 1);

        aiAssistantButton.onClick.AddListener(OnAIAssistantClick);
        UpdateButtonColor(normalColor);

        ConnectToESP32();
    }

    void ConnectToESP32()
    {
        try
        {
            client = new TcpClient();
            client.ReceiveTimeout = 3000;
            client.NoDelay = true;
            client.Connect(esp32IP, esp32Port);
            stream = client.GetStream();

            running = true;
            receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            receiveThread.Start();

            Debug.Log("已连接到 ESP32 摄像头");
        }
        catch (Exception e)
        {
            Debug.LogError("连接失败: " + e.Message);
            ShowError("连接 ESP32 失败");
        }
    }

    void ReceiveLoop()
    {
        try
        {
            while (running)
            {
                byte[] lenBuf = ReadExact(4);
                if (lenBuf == null) break;

                uint frameLen = (uint)(lenBuf[0] << 24 | lenBuf[1] << 16 | lenBuf[2] << 8 | lenBuf[3]);
                if (frameLen == 0 || frameLen > 2_000_000) continue;

                byte[] frameData = ReadExact((int)frameLen);
                if (frameData == null) break;

                if (frameQueue.Count == 0) frameQueue.Enqueue(frameData);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("接收错误: " + e.Message);
        }
    }

    byte[] ReadExact(int length)
    {
        byte[] buffer = new byte[length];
        int offset = 0;
        while (offset < length)
        {
            int read = 0;
            try
            {
                read = stream.Read(buffer, offset, length - offset);
            }
            catch
            {
                return null;
            }
            if (read <= 0) return null;
            offset += read;
        }
        return buffer;
    }

    void Update()
    {
        if (frameQueue.TryDequeue(out byte[] frameData))
        {
            if (cameraTexture.LoadImage(frameData))
            {
                cameraPreview.texture = cameraTexture;
            }
        }
    }

    void OnAIAssistantClick()
    {
        if (isProcessing) return;
        StartCoroutine(AnalyzeCurrentImage());
    }

    IEnumerator AnalyzeCurrentImage()
    {
        isProcessing = true;
        UpdateButtonColor(processingColor);

        yield return StartCoroutine(CaptureScreenshot());

        yield return StartCoroutine(CozeAnalyzeScreenshot());

        isProcessing = false;
    }

    IEnumerator CaptureScreenshot()
    {
        Texture sourceTexture = cameraPreview.texture;
        if (sourceTexture == null)
        {
            ShowError("错误：没有摄像头画面");
            yield break;
        }

        if (currentScreenshot != null) { Destroy(currentScreenshot); currentScreenshot = null; }

        if (sourceTexture is Texture2D src2D)
        {
            currentScreenshot = new Texture2D(src2D.width, src2D.height, TextureFormat.RGB24, false);
            currentScreenshot.SetPixels(src2D.GetPixels());
            currentScreenshot.Apply();
        }
        else
        {
            ShowError("错误：不支持的纹理类型（期望 Texture2D）");
            yield break;
        }

        yield return null;
    }

    IEnumerator CozeAnalyzeScreenshot()
    {
        if (currentScreenshot == null)
        {
            ShowError("没有可发送的图片");
            yield break;
        }

        string fileId = null;
        yield return StartCoroutine(CozeUploadImage(currentScreenshot, id => fileId = id));
        if (string.IsNullOrEmpty(fileId))
        {
            ShowError("图片上传失败");
            yield break;
        }

        UpdateButtonColor(uploadSuccessColor);

        string conversationId = null;
        string chatId = null;
        yield return StartCoroutine(CozeStartChat(fileId, (cid, chid) => { conversationId = cid; chatId = chid; }));
        if (string.IsNullOrEmpty(chatId))
        {
            ShowError("AI 请求失败");
            yield break;
        }

        bool done = false;
        yield return StartCoroutine(CozeCheckStatus(conversationId, chatId, ok => done = ok));
        if (!done)
        {
            ShowError("AI 分析超时");
            yield break;
        }

        yield return StartCoroutine(CozeFetchResult(conversationId, chatId));
    }

    IEnumerator CozeUploadImage(Texture2D image, Action<string> onDone)
    {
        byte[] imgBytes = image.EncodeToJPG(90);
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", imgBytes, "screenshot.jpg", "image/jpeg");

        using (UnityWebRequest req = UnityWebRequest.Post(COZE_FILE_UPLOAD_URL, form))
        {
            req.timeout = Mathf.CeilToInt(timeout);
            req.SetRequestHeader("Authorization", $"Bearer {COZE_API_KEY}");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<FileUploadResponse>(req.downloadHandler.text);
                if (resp != null && resp.code == 0 && resp.data != null)
                    onDone?.Invoke(resp.data.id);
                else
                    onDone?.Invoke(null);
            }
            else
            {
                Debug.LogError("上传失败: " + req.error);
                onDone?.Invoke(null);
            }
        }
    }

    IEnumerator CozeStartChat(string fileId, Action<string, string> onDone)
    {
        string prompt = "你是一个菌类专家，正在对蘑菇的种类进行分析，我将传给你一张蘑菇的图片，你需要判断蘑菇的种类和生长阶段。请严格按JSON格式输出结果，包含以下字段：1. species_name（蘑菇种类中文名称），2. introduction（简要介绍，50字左右），3. growth_analysis（生长情况分析，包括生长阶段、健康状态等）。注意：只返回纯JSON对象，不要添加额外文本说明。";
        string contentArrayJson = "[{\"type\":\"text\",\"text\":\"" + EscapeJson(prompt) + "\"},{\"type\":\"image\",\"file_id\":\"" + EscapeJson(fileId) + "\"}]";

        string payload = "{"
            + "\"bot_id\":\"" + COZE_BOT_ID + "\","
            + "\"user_id\":\"123\","
            + "\"stream\":false,"
            + "\"auto_save_history\":true,"
            + "\"additional_messages\":[{"
                + "\"role\":\"user\","
                + "\"content\":" + JsonString(contentArrayJson) + ","
                + "\"content_type\":\"object_string\""
            + "}]"
        + "}";

        using (UnityWebRequest req = new UnityWebRequest($"{COZE_CHAT_BASE}/chat", "POST"))
        {
            req.timeout = Mathf.CeilToInt(timeout);
            byte[] body = Encoding.UTF8.GetBytes(payload);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {COZE_API_KEY}");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<ChatStartResponse>(req.downloadHandler.text);
                if (resp != null && resp.data != null)
                    onDone?.Invoke(resp.data.conversation_id, resp.data.id);
                else
                    onDone?.Invoke(null, null);
            }
            else
            {
                Debug.LogError("StartChat 失败: " + req.error);
                onDone?.Invoke(null, null);
            }
        }
    }

    IEnumerator CozeCheckStatus(string conversationId, string chatId, Action<bool> onDone)
    {
        string url = $"{COZE_CHAT_BASE}/chat/retrieve?conversation_id={conversationId}&chat_id={chatId}";
        float start = Time.time;
        bool ok = false;

        while (Time.time - start < timeout)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = Mathf.CeilToInt(timeout);
                req.SetRequestHeader("Authorization", $"Bearer {COZE_API_KEY}");
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonUtility.FromJson<ChatStatusResponse>(req.downloadHandler.text);
                    if (resp != null && resp.data != null && resp.data.status == "completed")
                    {
                        ok = true;
                        break;
                    }
                }
            }
            yield return new WaitForSeconds(1.0f);
        }

        onDone?.Invoke(ok);
    }

    IEnumerator CozeFetchResult(string conversationId, string chatId)
    {
        string url = $"{COZE_CHAT_BASE}/chat/message/list?conversation_id={conversationId}&chat_id={chatId}";
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = Mathf.CeilToInt(timeout);
            req.SetRequestHeader("Authorization", $"Bearer {COZE_API_KEY}");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<ChatMessageListResponse>(req.downloadHandler.text);
                if (resp == null || resp.data == null || resp.data.Length == 0)
                {
                    ShowError("无分析结果");
                    yield break;
                }

                string raw = resp.data[0].content;
                string jsonObject = ExtractFirstJsonObject(raw);
                if (string.IsNullOrEmpty(jsonObject))
                {
                    ShowError("解析失败（未找到 JSON）");
                    yield break;
                }

                try
                {
                    var result = JsonUtility.FromJson<MushroomResult>(jsonObject);
                    speciesText.text = $"种类: {result.species_name}";
                    introductionText.text = $"简介: {result.introduction}";
                    analysisText.text = $"生长分析: {result.growth_analysis}";

                    StartCoroutine(HighlightText(speciesText));
                    StartCoroutine(HighlightText(introductionText));
                    StartCoroutine(HighlightText(analysisText));
                    UpdateButtonColor(successColor);

                    StartCoroutine(ResetButtonColor());
                }
                catch (Exception ex)
                {
                    Debug.LogError("JSON 解析异常: " + ex.Message);
                    ShowError("解析失败");
                }
            }
            else
            {
                Debug.LogError("FetchResult 失败: " + req.error);
                ShowError("获取结果失败");
            }
        }
    }

    IEnumerator HighlightText(TMP_Text text)
    {
        Color originalColor = text.color;
        text.color = successColor;
        yield return new WaitForSeconds(highlightDuration);
        text.color = originalColor;
    }

    void ShowError(string message)
    {
        speciesText.text = "分析失败";
        introductionText.text = message;
        analysisText.text = "请重试或检查连接";
        UpdateButtonColor(errorColor);
        StartCoroutine(ResetButtonColor());
    }

    IEnumerator ResetButtonColor()
    {
        yield return new WaitForSeconds(2f);
        UpdateButtonColor(normalColor);
    }

    void UpdateButtonColor(Color color)
    {
        Image buttonImage = aiAssistantButton.GetComponent<Image>();
        if (buttonImage != null) buttonImage.color = color;
    }

    void OnApplicationQuit()
    {
        running = false;
        try { stream?.Close(); } catch { }
        try { client?.Close(); } catch { }
    }

    // ===== 工具 & 数据结构 =====
    static string JsonString(string plain) => "\"" + EscapeJson(plain) + "\"";
    static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    static string ExtractFirstJsonObject(string input)
    {
        if (string.IsNullOrEmpty(input)) return null;
        int start = -1, depth = 0;
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '{')
            {
                if (depth == 0) start = i;
                depth++;
            }
            else if (input[i] == '}')
            {
                if (depth > 0) depth--;
                if (depth == 0 && start != -1)
                {
                    return input.Substring(start, i - start + 1);
                }
            }
        }
        return null;
    }

    [Serializable] private class FileUploadResponse { public int code; public FileData data; }
    [Serializable] private class FileData { public string id; public string file_name; public long bytes; }

    [Serializable] private class ChatStartResponse { public ChatStartData data; }
    [Serializable] private class ChatStartData { public string conversation_id; public string id; }

    [Serializable] private class ChatStatusResponse { public ChatStatusData data; }
    [Serializable] private class ChatStatusData { public string status; }

    [Serializable] private class ChatMessageListResponse { public ChatMessageData[] data; }
    [Serializable] private class ChatMessageData { public string content; }

    [Serializable] private class MushroomResult
    {
        public string species_name;
        public string introduction;
        public string growth_analysis;
    }
}
