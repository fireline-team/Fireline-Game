using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraTargetSetter : MonoBehaviour
{
    
    
    CinemachineTargetGroup targetGroup;

    private void Awake()
    {
        targetGroup = FindAnyObjectByType<CinemachineTargetGroup>();
    }




    public void OnPlayerJoined(PlayerInput playerInput)
    {
        SetTarget(playerInput.transform);
    }
    
    public void SetTarget(Transform target)
    {
        targetGroup.AddMember(target, 1f, 0.75f);
    }
    
    
    
   
}
