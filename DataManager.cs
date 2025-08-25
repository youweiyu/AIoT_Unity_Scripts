using System.Collections;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance;

    // 当前传感器数据
    public float temperature = 25.0f;    // ℃
    public float humidity = 85.0f;       // %
    public float ultraviolet = 3.0f;     // UV指数
    public float smoke = 20.0f;          // ppm

    // 漂移目标值（用于平滑变化）
    private float targetTemperature;
    private float targetHumidity;
    private float targetUltraviolet;
    private float targetSmoke;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 初始化目标值
        targetTemperature = temperature;
        targetHumidity = humidity;
        targetUltraviolet = ultraviolet;
        targetSmoke = smoke;

        StartCoroutine(UpdateSensorData());
    }

    IEnumerator UpdateSensorData()
    {
        while (true)
        {
            // 缓慢漂移：目标值每次变化很小
            targetTemperature += Random.Range(-0.02f, 0.02f);
            targetHumidity += Random.Range(-0.1f, 0.1f);
            targetUltraviolet += Random.Range(-0.02f, 0.02f);
            targetSmoke += Random.Range(-0.5f, 0.5f);

            // 限制目标值范围
            targetTemperature = Mathf.Clamp(targetTemperature, 24f, 26f);
            targetHumidity = Mathf.Clamp(targetHumidity, 83f, 87f);
            targetUltraviolet = Mathf.Clamp(targetUltraviolet, 2f, 4f);
            targetSmoke = Mathf.Clamp(targetSmoke, 18f, 22f);

            // 当前值向目标值平滑靠近 + 小随机噪声
            temperature = Mathf.Lerp(temperature, targetTemperature, 0.1f) + Random.Range(-0.1f, 0.1f);
            humidity = Mathf.Lerp(humidity, targetHumidity, 0.1f) + Random.Range(-0.1f, 0.1f);
            ultraviolet = Mathf.Lerp(ultraviolet, targetUltraviolet, 0.1f) + Random.Range(-0.05f, 0.05f);
            smoke = Mathf.Lerp(smoke, targetSmoke, 0.2f) + Random.Range(-0.5f, 0.5f);

            // 最终限制范围
            temperature = Mathf.Clamp(temperature, 24f, 26f);
            humidity = Mathf.Clamp(humidity, 82f, 88f);
            ultraviolet = Mathf.Clamp(ultraviolet, 2f, 4f);
            smoke = Mathf.Clamp(smoke, 15f, 25f);

            // 通知 UI
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateUI(temperature, humidity, ultraviolet, smoke);
            }

            yield return new WaitForSeconds(2f);
        }
    }
}
