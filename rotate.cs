using UnityEngine;

public class SelfRotateZ : MonoBehaviour
{
    [Header("旋转速度 (度/秒)")]
    public float speed = 50f;  // 每秒旋转角度

    void Update()
    {
        // 绕自身Z轴旋转
        transform.Rotate(Vector3.forward * speed * Time.deltaTime, Space.Self);
    }
}
