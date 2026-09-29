using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraTargetSetter : MonoBehaviour
{
    
    
    CinemachineTargetGroup targetGroup;

    private void Awake()
    {
        targetGroup = GetComponentInChildren<CinemachineTargetGroup>();
    }




    public void OnPlayerJoined(PlayerInput playerInput)
    {
        SetTarget(playerInput.transform);
    }
    
    public void SetTarget(Transform target)
    {
        targetGroup.AddMember(target, 1f, 0.75f);
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        targetGroup = GetComponentInChildren<CinemachineTargetGroup>();
        
    }
    
    // Update is called once per frame
    void Update()
    {
        
    }
}
