using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class SimpleHttpPost : MonoBehaviour
{
    private string url = "http://118.178.110.103/car/control";

    void Start()
    {
        StartCoroutine(SendPostRequest());
    }

    IEnumerator SendPostRequest()
    {
        // 你的命令数据，示例是发送 {"command":"forward"}
        var jsonData = "{\"command\":\"forward\"}";
        byte[] postData = System.Text.Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(postData);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 5;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("POST请求成功，响应：" + request.downloadHandler.text);
            }
            else
            {
                Debug.LogError("POST请求失败，错误：" + request.error);
            }
        }
    }
}
