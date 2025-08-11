using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

public class MJPEGStreamer : MonoBehaviour
{
    [Header("流设置")]
    public string streamURL = "https://jianxia.xyz/camera/get";
    public RawImage targetDisplay;
    public float frameRate = 15f;
    public bool autoReconnect = true;
    public float reconnectDelay = 3f;

    [Header("调试设置")]
    public bool enableDebugLogs = true;

    private Texture2D texture;
    private bool isStreaming = false;
    private UnityWebRequest request;

    void Start()
    {
        ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
        StartCoroutine(StreamRoutine());
    }

    IEnumerator StreamRoutine()
    {
        while (true)
        {
            if (!isStreaming)
            {
                yield return StartCoroutine(StreamMJPEG());
            }
            yield return new WaitForSeconds(1f);
        }
    }

    IEnumerator StreamMJPEG()
    {
        isStreaming = true;
        texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        targetDisplay.texture = texture;

        if (enableDebugLogs)
            Debug.Log($"开始连接：{streamURL}");

        request = UnityWebRequest.Get(streamURL);
        request.certificateHandler = new BypassCertificate(); // ✳️ 关键：跳过SSL验证
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = 10;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"连接失败：{request.error}");
            if (autoReconnect)
            {
                yield return new WaitForSeconds(reconnectDelay);
                isStreaming = false;
            }
            yield break;
        }

        byte[] buffer = new byte[0];
        while (!request.isDone && isStreaming)
        {
            byte[] data = request.downloadHandler.data;
            if (data.Length > buffer.Length)
            {
                // 追加新数据
                byte[] newBytes = new byte[data.Length - buffer.Length];
                System.Buffer.BlockCopy(data, buffer.Length, newBytes, 0, newBytes.Length);
                buffer = data;

                // 尝试提取JPEG帧
                ExtractAndDisplayJPEG(newBytes);
            }
            yield return new WaitForSeconds(1f / frameRate);
        }

        if (enableDebugLogs)
            Debug.Log("连接断开");
        isStreaming = false;
    }

    void ExtractAndDisplayJPEG(byte[] data)
    {
        int start = FindJpegStart(data);
        int end = FindJpegEnd(data, start);

        if (start >= 0 && end > start)
        {
            int length = end - start + 2;
            byte[] jpeg = new byte[length];
            System.Buffer.BlockCopy(data, start, jpeg, 0, length);

            try
            {
                texture.LoadImage(jpeg);
                targetDisplay.texture = texture;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"图像解码失败：{e.Message}");
            }
        }
    }

    int FindJpegStart(byte[] data)
    {
        for (int i = 0; i < data.Length - 1; i++)
        {
            if (data[i] == 0xFF && data[i + 1] == 0xD8)
                return i;
        }
        return -1;
    }

    int FindJpegEnd(byte[] data, int start)
    {
        for (int i = start + 2; i < data.Length - 1; i++)
        {
            if (data[i] == 0xFF && data[i + 1] == 0xD9)
                return i;
        }
        return -1;
    }

    private void OnDestroy()
    {
        isStreaming = false;
        if (request != null)
        {
            request.Abort();
            request.Dispose();
        }
        if (texture != null)
        {
            Destroy(texture);
        }
    }

    // ✳️ 补充：跳过 SSL 验证的类
    private class BypassCertificate : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData) => true;
    }
}
