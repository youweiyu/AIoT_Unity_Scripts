// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;
// using System;
// using System.Net.Sockets;
// using System.Threading;
// using System.Collections.Concurrent;
// using UnityEngine.Networking;
// using System.Collections;

// public class ESP32AIAssistant : MonoBehaviour
// {
//     [Header("UI组件")]
//     public Button aiAssistantButton;
//     public RawImage cameraPreview;
//     public TMP_Text speciesText;
//     public TMP_Text introductionText;
//     public TMP_Text analysisText;

//     [Header("ESP32设置")]
//     public string esp32IP = "192.168.223.59";
//     public int esp32Port = 8080;

//     [Header("分析API设置")]
//     public string analysisAPI = "";
//     public float timeout = 30f;

//     [Header("视觉反馈")]
//     public Color normalColor = Color.white;
//     public Color processingColor = Color.yellow;
//     public Color successColor = Color.green;
//     public Color errorColor = Color.red;
//     public float highlightDuration = 0.5f;

//     // TCP & 接收线程
//     private TcpClient client;
//     private NetworkStream stream;
//     private Thread receiveThread;
//     private volatile bool running = false;
//     private ConcurrentQueue<byte[]> frameQueue = new ConcurrentQueue<byte[]>();

//     private Texture2D cameraTexture;
//     private Texture2D currentScreenshot;
//     private bool isProcessing = false;

//     void Start()
//     {
//         cameraTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
//         cameraPreview.texture = cameraTexture;

//         aiAssistantButton.onClick.AddListener(OnAIAssistantClick);
//         UpdateButtonColor(normalColor);

//         ConnectToESP32();
//     }

//     void ConnectToESP32()
//     {
//         try
//         {
//             client = new TcpClient();
//             client.ReceiveTimeout = 3000;
//             client.Connect(esp32IP, esp32Port);
//             stream = client.GetStream();

//             running = true;
//             receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
//             receiveThread.Start();

//             Debug.Log("已连接到 ESP32 摄像头");
//         }
//         catch (Exception e)
//         {
//             Debug.LogError("连接失败: " + e.Message);
//         }
//     }

//     void ReceiveLoop()
//     {
//         try
//         {
//             while (running)
//             {
//                 byte[] lenBuf = ReadExact(4);
//                 if (lenBuf == null) break;

//                 uint frameLen = (uint)(lenBuf[0] << 24 | lenBuf[1] << 16 | lenBuf[2] << 8 | lenBuf[3]);
//                 byte[] frameData = ReadExact((int)frameLen);
//                 if (frameData == null) break;

//                 if (frameQueue.Count == 0) frameQueue.Enqueue(frameData);
//             }
//         }
//         catch (Exception e)
//         {
//             Debug.LogError("接收错误: " + e.Message);
//         }
//     }

//     byte[] ReadExact(int length)
//     {
//         byte[] buffer = new byte[length];
//         int offset = 0;
//         while (offset < length)
//         {
//             int read = stream.Read(buffer, offset, length - offset);
//             if (read <= 0) return null;
//             offset += read;
//         }
//         return buffer;
//     }

//     void Update()
//     {
//         // 显示最新帧
//         if (frameQueue.TryDequeue(out byte[] frameData))
//         {
//             if (cameraTexture.LoadImage(frameData))
//             {
//                 cameraPreview.texture = cameraTexture;
//             }
//         }
//     }

//     void OnAIAssistantClick()
//     {
//         if (isProcessing) return;
//         StartCoroutine(AnalyzeCurrentImage());
//     }

//     IEnumerator AnalyzeCurrentImage()
//     {
//         isProcessing = true;
//         UpdateButtonColor(processingColor);

//         yield return StartCoroutine(CaptureScreenshot());

//         yield return StartCoroutine(SendAnalysisRequest());

//         isProcessing = false;
//     }

//     IEnumerator CaptureScreenshot()
//     {
//         Texture sourceTexture = cameraPreview.texture;
//         if (sourceTexture == null)
//         {
//             ShowError("错误：没有摄像头画面");
//             yield break;
//         }

//         currentScreenshot = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGB24, false);

//         if (sourceTexture is Texture2D)
//         {
//             currentScreenshot.SetPixels(((Texture2D)sourceTexture).GetPixels());
//             currentScreenshot.Apply();
//         }
//         else
//         {
//             ShowError("错误：不支持的纹理类型");
//             yield break;
//         }

//         yield return null;
//     }

//     IEnumerator SendAnalysisRequest()
//     {
//         byte[] imageBytes = currentScreenshot.EncodeToJPG();

//         WWWForm form = new WWWForm();
//         form.AddBinaryData("image", imageBytes, "screenshot.jpg", "image/jpeg");

//         using (UnityWebRequest request = UnityWebRequest.Post(analysisAPI, form))
//         {
//             request.timeout = (int)timeout;
//             var op = request.SendWebRequest();
//             float startTime = Time.time;

//             while (!op.isDone)
//             {
//                 if (Time.time - startTime > timeout)
//                 {
//                     request.Abort();
//                     ShowError("请求超时");
//                     yield break;
//                 }
//                 yield return null;
//             }

//             if (request.result != UnityWebRequest.Result.Success)
//             {
//                 ShowError($"分析失败: {request.error}");
//             }
//             else
//             {
//                 try
//                 {
//                     ProcessAnalysisResponse(request.downloadHandler.text);
//                     UpdateButtonColor(successColor);
//                 }
//                 catch (Exception ex)
//                 {
//                     ShowError($"解析失败: {ex.Message}");
//                 }
//             }
//         }

//         if (currentScreenshot != null)
//         {
//             Destroy(currentScreenshot);
//             currentScreenshot = null;
//         }
//     }

//     void ProcessAnalysisResponse(string jsonResponse)
//     {
//         AnalysisResponse response = JsonUtility.FromJson<AnalysisResponse>(jsonResponse);
//         speciesText.text = $"种类: {response.species_name}";
//         introductionText.text = $"简介: {response.introduction}";
//         analysisText.text = $"生长分析: {response.growth_analysis}";

//         StartCoroutine(HighlightText(speciesText));
//         StartCoroutine(HighlightText(introductionText));
//         StartCoroutine(HighlightText(analysisText));
//     }

//     IEnumerator HighlightText(TMP_Text text)
//     {
//         Color originalColor = text.color;
//         text.color = successColor;
//         yield return new WaitForSeconds(highlightDuration);
//         text.color = originalColor;
//     }

//     void ShowError(string message)
//     {
//         speciesText.text = "分析失败";
//         introductionText.text = message;
//         analysisText.text = "请重试或检查连接";
//         UpdateButtonColor(errorColor);
//         StartCoroutine(ResetButtonColor());
//     }

//     IEnumerator ResetButtonColor()
//     {
//         yield return new WaitForSeconds(2f);
//         UpdateButtonColor(normalColor);
//     }

//     void UpdateButtonColor(Color color)
//     {
//         Image buttonImage = aiAssistantButton.GetComponent<Image>();
//         if (buttonImage != null) buttonImage.color = color;
//     }

//     void OnApplicationQuit()
//     {
//         running = false;
//         if (stream != null) stream.Close();
//         if (client != null) client.Close();
//     }

//     [Serializable]
//     private class AnalysisResponse
//     {
//         public string species_name;
//         public string introduction;
//         public string growth_analysis;
//     }
// }
