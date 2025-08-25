using UnityEngine;
using UnityEngine.UI;

public class StaticImageTest : MonoBehaviour
{
    public RawImage targetDisplay;
    public Texture2D testTexture;  // 在Inspector拖入一张贴图

    void Start()
    {
        targetDisplay.texture = testTexture;
    }
}
