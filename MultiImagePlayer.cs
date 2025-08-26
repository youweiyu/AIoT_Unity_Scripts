using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class ImagePlayerGroup
{
    public string folderName;       // 对应 Resources 下的子目录，如 "player1"
    public RawImage targetImage;    // 显示图片的 UI
    public Button prevButton;       // 前一张按钮
    public Button nextButton;       // 下一张按钮

    [HideInInspector] public Texture[] frames;
    [HideInInspector] public int index = 0;
}

public class MultiImagePlayer : MonoBehaviour
{
    [Header("三个播放器组")]
    public ImagePlayerGroup[] players = new ImagePlayerGroup[3];

    void Start()
    {
        foreach (var player in players)
        {
            // 加载图片
            player.frames = Resources.LoadAll<Texture>(player.folderName);

            if (player.frames.Length == 0)
            {
                Debug.LogError("没有找到图片: Resources/" + player.folderName);
                continue;
            }

            // 显示第一张
            player.index = 0;
            player.targetImage.texture = player.frames[player.index];

            // 绑定按钮事件
            player.prevButton.onClick.AddListener(() => ShowPrev(player));
            player.nextButton.onClick.AddListener(() => ShowNext(player));
        }
    }

    void ShowPrev(ImagePlayerGroup player)
    {
        if (player.frames.Length == 0) return;
        player.index = (player.index - 1 + player.frames.Length) % player.frames.Length;
        player.targetImage.texture = player.frames[player.index];
    }

    void ShowNext(ImagePlayerGroup player)
    {
        if (player.frames.Length == 0) return;
        player.index = (player.index + 1) % player.frames.Length;
        player.targetImage.texture = player.frames[player.index];
    }
}
