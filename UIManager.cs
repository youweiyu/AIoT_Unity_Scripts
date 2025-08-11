using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    // 单例实例
    public static UIManager Instance;

    public TextMeshProUGUI tempText;
    public TextMeshProUGUI humidityText;
    public TextMeshProUGUI co2Text;
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
        tempText = transform.Find("UIPanel/TempText").GetComponent<TextMeshProUGUI>();
        humidityText = transform.Find("UIPanel/HumidityText").GetComponent<TextMeshProUGUI>();
        co2Text = transform.Find("UIPanel/CO2Text").GetComponent<TextMeshProUGUI>();

        // 初始显示（建议用 try-catch 防止启动顺序问题）
        if (DataManager.Instance != null)
        {
            UpdateUI(DataManager.Instance.temperature, DataManager.Instance.humidity);
        }

        quitButton = transform.Find("UIPanel/QuitButton").GetComponent<Button>();
        quitButton.onClick.AddListener(QuitApplication);
    }

    public void UpdateUI(float temp, float hum, float co2 = -1f)
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

        if (co2Text != null && co2 >= 0)
        {
        co2Text.text = $"CO2浓度: {co2:F0} ppm";
        // 简单颜色判断
        if (co2 > 1000)
            co2Text.color = Color.red;
        else if (co2 > 800)
            co2Text.color = new Color(1f, 0.65f, 0f); // orange
        else
            co2Text.color = Color.green;
        }
    }


    void Update()
    {
        // 使UI始终面向用户
        // Transform camera = Camera.main.transform;
        // transform.position = camera.position + camera.forward * 2f;
        // transform.rotation = Quaternion.LookRotation(transform.position - camera.position);
    }
    void QuitApplication()
    {
        Application.Quit();
    }
}
