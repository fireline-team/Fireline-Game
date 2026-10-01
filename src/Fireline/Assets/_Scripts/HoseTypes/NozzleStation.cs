using UnityEngine;

public class NozzleStation : Interactable
{
    [SerializeField] private NozzleType nozzle = NozzleType.Mist;

    public NozzleType Nozzle => nozzle;

    public override string GetPrompt(PlayerInteractor player) => $"Equip {nozzle} nozzle";

    public override bool CanInteract(PlayerInteractor player)
    {
        HoseLoadout loadout = player.Loadout;
        return loadout != null && loadout.Has(nozzle) && loadout.CurrentType != nozzle;
    }

    public override void Interact(PlayerInteractor player) => player.Loadout.Equip(nozzle);
}
