using System.Collections;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    // 单例实例
    public static DataManager Instance;

    // 当前传感器数据
    public float temperature = 25.0f;
    public float humidity = 85.0f;
    public float co2 = 800.0f;

    void Awake()
    {
        // 初始化单例
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); // 避免重复
        }
    }

    void Start()
    {
        StartCoroutine(UpdateSensorData());
    }

    IEnumerator UpdateSensorData()
    {
        while (true)
        {
        // 模拟温湿度变化
        temperature += Random.Range(-0.5f, 0.5f);
        humidity += Random.Range(-2f, 2f);

        // 模拟 CO2 浓度变化
        co2 += Random.Range(-50f, 50f);

        // 限制在合理范围
        temperature = Mathf.Clamp(temperature, 20f, 30f);
        humidity = Mathf.Clamp(humidity, 70f, 95f);
        co2 = Mathf.Clamp(co2, 400f, 2000f); // 常见范围：400~2000 ppm

        // 通知 UI 更新
            if (UIManager.Instance != null)
            {
            UIManager.Instance.UpdateUI(temperature, humidity, co2);
            }

        yield return new WaitForSeconds(2f); // 每2秒更新一次
        }
    }

}
