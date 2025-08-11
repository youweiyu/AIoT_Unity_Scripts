using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System;

public class AIAssistant : MonoBehaviour
{
    [Header("UI组件")]
    public Button aiAssistantButton;
    public RawImage cameraPreview;
    public TMP_Text speciesText;
    public TMP_Text introductionText;
    public TMP_Text analysisText;
    
    [Header("API设置")]
    public string analysisAPI = "https://your-api-server.com/analyze";
    public float timeout = 15f;

    [Header("视觉反馈")]
    public Color normalColor = Color.white;
    public Color processingColor = Color.yellow;
    public Color successColor = Color.green;
    public Color errorColor = Color.red;
    public float highlightDuration = 0.5f;
    
    private Texture2D currentScreenshot;
    private bool isProcessing = false;
    
    void Start()
    {
        // 设置按钮事件
        aiAssistantButton.onClick.AddListener(OnAIAssistantClick);
        
        // 初始状态
        UpdateButtonColor(normalColor);
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
        
        // 1. 获取当前图像
        yield return StartCoroutine(CaptureScreenshot());
        
        // 2. 发送分析请求
        yield return StartCoroutine(SendAnalysisRequest());
        
        isProcessing = false;
    }
    
    IEnumerator CaptureScreenshot()
    {
        // 获取当前预览纹理
        Texture sourceTexture = cameraPreview.texture;
        
        if (sourceTexture == null)
        {
            Debug.LogError("没有可用的摄像头预览");
            ShowError("错误：没有摄像头画面");
            yield break;
        }
        
        // 创建临时纹理
        currentScreenshot = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGB24, false);
        
        // 处理不同类型的纹理
        if (sourceTexture is RenderTexture)
        {
            // 从RenderTexture读取
            RenderTexture.active = (RenderTexture)sourceTexture;
            currentScreenshot.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0);
            currentScreenshot.Apply();
            RenderTexture.active = null;
        }
        else if (sourceTexture is Texture2D)
        {
            // 直接复制Texture2D
            Graphics.CopyTexture(sourceTexture, currentScreenshot);
        }
        else
        {
            Debug.LogError("不支持的纹理类型: " + sourceTexture.GetType());
            ShowError("错误：不支持的图像格式");
            yield break;
        }
        
        yield return null;
    }
    
    IEnumerator SendAnalysisRequest()
    {
        // 将纹理转换为JPEG
        byte[] imageBytes = currentScreenshot.EncodeToJPG();
        
        // 创建表单数据
        WWWForm form = new WWWForm();
        form.AddBinaryData("image", imageBytes, "screenshot.jpg", "image/jpeg");
        
        // 创建请求
        using (UnityWebRequest request = UnityWebRequest.Post(analysisAPI, form))
        {
            // 设置超时
            request.timeout = (int)timeout;
            
            // 添加自定义头
            request.SetRequestHeader("Authorization", "Bearer YOUR_API_KEY");
            
            // 发送请求
            var operation = request.SendWebRequest();
            float startTime = Time.time;
            
            // 等待请求完成
            while (!operation.isDone)
            {
                // 超时检查
                if (Time.time - startTime > timeout)
                {
                    request.Abort();
                    ShowError("请求超时");
                    yield break;
                }
                yield return null;
            }
            
            // 处理响应
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"分析错误: {request.error}");
                ShowError($"分析失败: {request.error}");
            }
            else
            {
                // 解析JSON响应
                try
                {
                    ProcessAnalysisResponse(request.downloadHandler.text);
                    UpdateButtonColor(successColor);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"解析错误: {ex.Message}");
                    ShowError($"解析失败: {ex.Message}");
                }
            }
        }
        
        // 清理资源
        if (currentScreenshot != null)
        {
            Destroy(currentScreenshot);
            currentScreenshot = null;
        }
    }
    
    void ProcessAnalysisResponse(string jsonResponse)
    {
        // 解析JSON
        AnalysisResponse response = JsonUtility.FromJson<AnalysisResponse>(jsonResponse);
        
        // 更新UI
        speciesText.text = $"<b>种类:</b> {response.species_name}";
        introductionText.text = $"<b>简介:</b> {response.introduction}";
        analysisText.text = $"<b>生长分析:</b> {response.growth_analysis}";
        
        // 添加动画效果
        StartCoroutine(HighlightText(speciesText));
        StartCoroutine(HighlightText(introductionText));
        StartCoroutine(HighlightText(analysisText));
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
        
        // 重置按钮颜色
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
        if (buttonImage != null)
        {
            buttonImage.color = color;
        }
    }
    
    // JSON响应数据结构
    [System.Serializable]
    private class AnalysisResponse
    {
        public string species_name;
        public string introduction;
        public string growth_analysis;
    }
}
