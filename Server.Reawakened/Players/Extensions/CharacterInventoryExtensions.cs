﻿using A2m.Server;
using Microsoft.Extensions.Logging;
using PetDefines;
using Server.Base.Timers.Extensions;
using Server.Base.Timers.Services;
using Server.Reawakened.Core.Configs;
using Server.Reawakened.Core.Enums;
using Server.Reawakened.Database.Characters;
using Server.Reawakened.Network.Extensions;
using Server.Reawakened.Players.Helpers;
using Server.Reawakened.Players.Models.Character;
using Server.Reawakened.Players.Models.Pets;
using Server.Reawakened.Rooms.Extensions;
using Server.Reawakened.XMLs.Bundles.Base;

namespace Server.Reawakened.Players.Extensions;

public static class CharacterInventoryExtensions
{
    public static void HandleItemEffect(this Player player, ItemDescription usedItem,
        TimerThread timerThread, ItemRConfig config, ServerRConfig serverRConfig, ILogger<PlayerStatus> logger)
    {
        var effect = usedItem.ItemEffects.FirstOrDefault();

        if (usedItem.ItemEffects.Count > 0)
            player.Room.SendSyncEvent(new StatusEffect_SyncEvent(player.GameObjectId.ToString(), player.Room.Time,
                                (int)effect.Type, effect.Value, effect.Duration, true, usedItem.PrefabName, false));

        switch (effect.Type)
        {
            case ItemEffectType.PetRegainEnergy:
                if (!player.Character.Pets.TryGetValue(player.GetEquippedPetId(serverRConfig), out var pet))
                {
                    logger.LogWarning("Couldn't find equipped pet for {characterName}", player.CharacterName);
                    return;
                }
                pet.GainEnergy(player, effect != null ? effect.Value : 0);
                break;
            case ItemEffectType.Healing:
            case ItemEffectType.HealthBoost:
            case ItemEffectType.IncreaseHealing:
            case ItemEffectType.Regeneration:
                if (player.Character.CurrentLife >= player.Character.MaxLife)
                    return;

                player.HealCharacter(usedItem, timerThread, config, effect.Type);
                break;
            case ItemEffectType.IncreaseAirDamage:
            case ItemEffectType.IncreaseAllResist:
            case ItemEffectType.Shield:
            case ItemEffectType.WaterBreathing:
            case ItemEffectType.BananaMultiplier:
                player.TempData.BananaBoostsElixir = true;
                timerThread.DelayCall(SetBananaElixirTimer, player, TimeSpan.FromMinutes(30), TimeSpan.Zero, 1);
                break;
            case ItemEffectType.ExperienceMultiplier:
                player.TempData.ReputationBoostsElixir = true;
                timerThread.DelayCall(SetXpElixirTimer, player, TimeSpan.FromMinutes(30), TimeSpan.Zero, 1);
                break;
            case ItemEffectType.Defence:
                break;
            case ItemEffectType.Invisibility:
                player.TempData.Invisible = true;
                player.TemporaryInvisibility(usedItem.ItemEffects[(int)ItemFilterCategory.Consumables].Duration, timerThread);
                break;
            case ItemEffectType.Invalid:
            case ItemEffectType.Unknown:
            case ItemEffectType.Unknown_61:
            case ItemEffectType.Unknown_70:
            case ItemEffectType.Unknown_74:
            default:
                logger.LogError("Unknown ItemEffectType of ({effectType}) for item {usedItemName}", effect.Type, usedItem.PrefabName);
                return;
        }

        logger.LogInformation("Applied ItemEffectType of ({effectType}) from item {usedItemName} for _player {playerName}", effect.Type, usedItem.PrefabName, player.CharacterName);
    }

    public static void SetBananaElixirTimer(object playerObj)
    {
        var player = (Player)playerObj;

        if (player == null)
            return;

        if (player.TempData == null)
            return;

        player.TempData.BananaBoostsElixir = false;
    }

    public static void SetXpElixirTimer(object playerObj)
    {
        var player = (Player)playerObj;

        if (player == null)
            return;

        if (player.TempData == null)
            return;

        player.TempData.ReputationBoostsElixir = false;
    }

    public static bool TryGetItem(this CharacterModel characterData, int itemId, out ItemModel outItem) =>
        characterData.Inventory.Items.TryGetValue(itemId, out outItem);

    public static void RemoveItem(this Player player, ItemDescription item, int count, ItemCatalog itemCatalog, ItemRConfig config)
    {
        var characterData = player.Character;

        if (!characterData.TryGetItem(item.ItemId, out var gottenItem))
            return;

        gottenItem.Count -= count;

        if (gottenItem.Count <= 0)
            player.SetEmptySlot(item.ItemId, config);

        player.CheckObjective(ObjectiveEnum.Inventorycheck, gottenItem.ItemId.ToString(), item.PrefabName, gottenItem.Count, itemCatalog);
    }

    public static void AddItem(this Player player, ItemDescription item, int count, ItemCatalog itemCatalog)
    {
        if (!itemCatalog.CanAddItem(item))
            return;

        var characterData = player.Character;

        if (!characterData.Inventory.Items.ContainsKey(item.ItemId))
            characterData.Inventory.Items.Add(item.ItemId, new ItemModel
            {
                ItemId = item.ItemId,
                Count = 0,
                BindingCount = item.BindingCount,
                DelayUseExpiry = item.DelayUseExpiry
            });

        if (!characterData.TryGetItem(item.ItemId, out var gottenItem))
            return;

        gottenItem.Count += count;

        player.CheckObjective(ObjectiveEnum.Inventorycheck, gottenItem.ItemId.ToString(), item.PrefabName, gottenItem.Count, itemCatalog);
    }

    public static void AddKit(this CharacterModel characterData, List<ItemDescription> items, int count)
    {
        foreach (var item in items)
        {
            if (item != null)
                if (characterData.Inventory.Items.TryGetValue(item.ItemId, out var gottenKit))
                    gottenKit.Count += count;
                else
                    characterData.Inventory.Items.Add(item.ItemId, new ItemModel
                    {
                        ItemId = item.ItemId,
                        Count = count,
                        BindingCount = item.BindingCount,
                        DelayUseExpiry = item.DelayUseExpiry
                    });
        }
    }

    public static string GetItemListString(this InventoryModel inventory)
    {
        var sb = new SeparatedStringBuilder('|');

        foreach (var item in inventory.Items)
            sb.Append(item.Value.ToString());

        return sb.ToString();
    }

    public static void SendUpdatedInventory(this Player player, bool fromEquippedUpdate = false)
    {
        player.SendXt(
            "ip",
            player.Character.Inventory.GetItemListString(),
            fromEquippedUpdate
        );

        foreach (var item in player.Character.Inventory.Items)
            if (item.Value.Count <= 0)
                player.Character.Inventory.Items.Remove(item.Key);
    }

    public static void UseItemFromHotBar(this Player player, int itemId, ItemCatalog itemCatalog, ItemRConfig config)
    {
        var itemDescription = itemCatalog.GetItemFromId(itemId);

        player.RemoveItem(itemDescription, 1, itemCatalog, config);
        player.SendXt("hu", player.Character.Hotbar);
        player.SendUpdatedInventory();
    }

    public static void SetHotbarSlot(this Player player, int slotId, ItemModel itemModel, ItemRConfig config)
    {
        var hotbar = player.Character.Hotbar;

        foreach (var hotbarSlot in hotbar.HotbarButtons.Where
            (slot => slot.Key != slotId && slot.Value.ItemId == itemModel.ItemId))
            player.SetEmptySlot(hotbarSlot.Key, config);

        hotbar.HotbarButtons.TryAdd(slotId, itemModel);
        hotbar.HotbarButtons[slotId] = itemModel;
    }

    public static void SetEmptySlot(this Player player, int slotId, ItemRConfig config)
    {
        var hotbar = player.Character.Hotbar;

        if (!hotbar.HotbarButtons.ContainsKey(slotId))
            return;

        hotbar.HotbarButtons[slotId] = config.EmptySlot;
    }

    public static void EquipPet(this Player player, PetAbilityParams petAbilityParams,
        WorldStatistics worldStatistics, ServerRConfig serverRConfig, TimerThread timerThread)
    {
        if (player == null || !player.Character.Hotbar.HotbarButtons.ContainsKey(serverRConfig.PetHotbarIndex))
            return;

        var petId = player.GetEquippedPetId(serverRConfig);
        var refillCurrentEnergy = false;

        if (!player.Character.Pets.TryGetValue(petId, out var currentPet))
        {
            player.Character.Pets.Add(petId, currentPet = new PetModel());
            refillCurrentEnergy = true;
        }

        currentPet.SpawnPet(player, petId, true, petAbilityParams,
            refillCurrentEnergy, worldStatistics, serverRConfig, timerThread);
    }

    public static string GetEquippedPetId(this Player player, ServerRConfig serverRConfig) =>
        player.Character.Hotbar.HotbarButtons.TryGetValue
        (serverRConfig.PetHotbarIndex, out var petItem) ? petItem.ItemId.ToString() : 0.ToString();

    public static int GetMaxPetEnergy(this Player player, WorldStatistics worldStatistics, ServerRConfig config) =>
        // Needed due to pets not having abilities/energy in early 2012
        config.GameVersion < GameVersion.vLate2012
            ? 0
            : worldStatistics.Statistics[ItemEffectType.PetEnergyValue][WorldStatisticsGroup.Pet][player.Character.GlobalLevel];
}
