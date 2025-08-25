using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    // 单例实例
    public static UIManager Instance;

    // 文本组件
    public TextMeshProUGUI tempText;
    public TextMeshProUGUI humidityText;
    public TextMeshProUGUI uvText;
    public TextMeshProUGUI smokeText;
    public Button quitButton;
    
    void Awake()
    {
        // 设置单例
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); // 防止重复
        }
    }

    void Start()
    {
        // 获取文本组件（确保路径正确）
        tempText = transform.Find("UIPanel/DataGrip/TempText").GetComponent<TextMeshProUGUI>();
        humidityText = transform.Find("UIPanel/DataGrip/HumidityText").GetComponent<TextMeshProUGUI>();
        uvText = transform.Find("UIPanel/DataGrip/UVText").GetComponent<TextMeshProUGUI>();
        smokeText = transform.Find("UIPanel/DataGrip/SmokeText").GetComponent<TextMeshProUGUI>();

        // 初始显示（防止启动顺序问题）
        if (DataManager.Instance != null)
        {
            UpdateUI(DataManager.Instance.temperature, DataManager.Instance.humidity, 0f, 0f);
        }

        quitButton = transform.Find("UIPanel/QuitButton").GetComponent<Button>();
        quitButton.onClick.AddListener(QuitApplication);
    }

    // 更新UI，四个参数，紫外线和烟雾默认值可选
    public void UpdateUI(float temp, float hum, float uv = 0f, float smoke = 0f)
    {
        if (tempText != null)
        {
            tempText.text = $"温度: {temp:F1}°C";
            tempText.color = temp > 26f ? Color.red : Color.green;
        }

        if (humidityText != null)
        {
            humidityText.text = $"湿度: {hum:F1}%";
            humidityText.color = hum < 80f ? Color.yellow : Color.green;
        }

        if (uvText != null)
        {
            uvText.text = $"紫外线强度: {uv:F1}";
            uvText.color = uv > 5f ? Color.red : Color.green; // 简单阈值判断
        }

        if (smokeText != null)
        {
            smokeText.text = $"烟雾浓度: {smoke:F0}";
            if (smoke > 200) smokeText.color = Color.red;
            else if (smoke > 100) smokeText.color = new Color(1f, 0.65f, 0f); // 橙色
            else smokeText.color = Color.green;
        }
    }

    void QuitApplication()
    {
        Application.Quit();
    }
}
