using System.Collections;
using UnityEngine;
using Cinemachine;
using Cinemachine.Utility;

public class SimpleFollowRecenter : MonoBehaviour
{
    CinemachineFreeLook vcam;
 
    void Start()
    {
        vcam = GetComponent<CinemachineFreeLook>();
    }

    public IEnumerator RevertCam()
    {
        vcam.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
        yield return new WaitForFixedUpdate();
        vcam.m_BindingMode = CinemachineTransposer.BindingMode.SimpleFollowWithWorldUp;
    }
}