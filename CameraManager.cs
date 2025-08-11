using UnityEngine;
using Unity.XR.PXR;

public class CameraManager : MonoBehaviour
{

    void Start()
    {
        PXR_Manager.EnableVideoSeeThrough = true;
    }

}
