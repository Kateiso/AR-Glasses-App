using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.XREAL;

// Read-only capability check. This does not open a camera or claim access to raw gray sensors.
public sealed class AirCapabilityProbe : MonoBehaviour
{
    IEnumerator Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        for (int attempt = 0; attempt < 30; attempt++)
        {
            yield return new WaitForSecondsRealtime(2);
            var device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked) continue;
            bool rgb = XREALPlugin.IsHMDFeatureSupported(XREALSupportedFeature.XREAL_FEATURE_RGB_CAMERA);
            bool position = XREALPlugin.IsHMDFeatureSupported(XREALSupportedFeature.XREAL_FEATURE_PERCEPTION_HEAD_TRACKING_POSITION);
            Debug.Log($"AR_WINDOW_CAPABILITY tracked={tracked} rgbSupported={rgb} positionalTracking={position} windowDetector=notImplemented rawGrayAccess=notVerified");
            yield break;
        }
        Debug.Log("AR_WINDOW_CAPABILITY unavailable: no tracked headset after 60 seconds");
#else
        yield break;
#endif
    }
}
