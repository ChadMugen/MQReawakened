﻿using Microsoft.Extensions.Logging;
using Server.Base.Timers.Services;
using Server.Reawakened.Core.Configs;
using Server.Reawakened.Network.Protocols;
using Server.Reawakened.Players;
using Server.Reawakened.Players.Extensions;
using Server.Reawakened.XMLs.Bundles;
using Server.Reawakened.XMLs.Bundles.Base;
using Timer = Server.Base.Timers.Timer;

namespace Protocols.External._h__HotbarHandler;

public class RemoveSlot : ExternalProtocol
{
    public override string ProtocolName => "hr";

    public WorldStatistics WorldStatistics { get; set; }
    public PetAbilities PetAbilities { get; set; }
    public ServerRConfig ServerRConfig { get; set; }
    public ItemRConfig ItemConfig { get; set; }
    public TimerThread TimerThread { get; set; }
    public ILogger<RemoveSlot> Logger { get; set; }

    public override void Run(string[] message)
    {
        var hotbarSlotId = int.Parse(message[5]);

        if (!Player.Character.Hotbar.HotbarButtons.TryGetValue(hotbarSlotId, out var hotbarItem))
        {
            Logger.LogWarning("{characterName} has not yet unlocked slot #{hotbarSlotNum}.)",
                Player.CharacterName, hotbarSlotId++);
            return;
        }

        Player.SetEmptySlot(hotbarSlotId, ItemConfig);

        if (Player.Character.Pets.TryGetValue(hotbarItem.ItemId.ToString(), out var pet) &&
            PetAbilities.PetAbilityData.TryGetValue(int.Parse(pet.PetId), out var petAbilityParams))
            pet.SpawnPet(Player, pet.PetId, false, petAbilityParams,
                false, WorldStatistics, ServerRConfig, TimerThread);

        SendXt("hr", Player.Character.Hotbar);
    }
}
