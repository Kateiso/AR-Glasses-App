using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

public sealed class EnvironmentProbe : MonoBehaviour
{
    public Transform head;
    public Material beaconMaterial;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(2);
        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beacon.name = "World Locked Display Check";
        beacon.transform.position = head.position + head.forward * 2f;
        beacon.transform.localScale = Vector3.one * 0.25f;
        beacon.GetComponent<Renderer>().sharedMaterial = beaconMaterial;
        Destroy(beacon.GetComponent<Collider>());
        for (int sample = 0; sample < 3; sample++)
        {
            var displays = new List<XRDisplaySubsystem>();
            var inputs = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(displays);
            SubsystemManager.GetSubsystems(inputs);
            var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
            Debug.Log($"AR_ENV_CHECK loader={loader?.name ?? "none"} displayRunning={displays.Exists(d => d.running)} inputRunning={inputs.Exists(i => i.running)} headPosition={head.position} headRotation={head.rotation.eulerAngles}");
            yield return new WaitForSeconds(5);
        }
    }
}
