using System;
using UnityEngine;

public enum NozzleType
{
    Standard,
    Mist,
    Jet,
}

public class HoseLoadout : MonoBehaviour
{
    [SerializeField] private NozzleType startingNozzle = NozzleType.Standard;

    private HoseWeapon[] _nozzles;

    public HoseWeapon Current { get; private set; }
    public NozzleType CurrentType { get; private set; }
    
    public event Action<NozzleType> Changed;

    private void Awake()
    {
        _nozzles = GetComponents<HoseWeapon>();
        if (_nozzles.Length == 0)
        {
            Debug.LogError($"{name} has a HoseLoadout but no nozzle components.", this);
            return;
        }

        if (!Equip(startingNozzle))
        {
            foreach (NozzleType type in Enum.GetValues(typeof(NozzleType)))
                if (Equip(type)) break;
        }
    }
    
    public bool Has(NozzleType type) => Find(type) != null;
    
    public bool Equip(NozzleType type)
    {
        HoseWeapon target = Find(type);
        if (target == null) return false;

        foreach (HoseWeapon nozzle in _nozzles)
            nozzle.enabled = nozzle == target;

        bool changed = Current != target;
        Current = target;
        CurrentType = type;
        if (changed) Changed?.Invoke(type);
        return true;
    }

    private HoseWeapon Find(NozzleType type)
    {
        if (_nozzles == null) return null;
        Type wanted = ComponentTypeFor(type);
        foreach (HoseWeapon nozzle in _nozzles)
            if (nozzle != null && nozzle.GetType() == wanted) return nozzle;
        return null;
    }
    
    private static Type ComponentTypeFor(NozzleType type)
    {
        switch (type)
        {
            case NozzleType.Standard: return typeof(StandardHose);
            case NozzleType.Mist: return typeof(MistHose);
            case NozzleType.Jet: return typeof(JetHose);
            default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
}
