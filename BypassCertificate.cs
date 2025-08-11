using UnityEngine.Networking;

// 自定义证书处理程序，用于忽略SSL证书验证
public class BypassCertificate : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        // 始终接受证书
        return true;
    }
}
