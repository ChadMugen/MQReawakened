﻿using A2m.Server;
using Microsoft.Extensions.Logging;
using Server.Base.Timers.Extensions;
using Server.Base.Timers.Services;
using Server.Reawakened.Configs;
using Server.Reawakened.Entities.Components;
using Server.Reawakened.Entities.Entity;
using Server.Reawakened.Entities.Enums;
using Server.Reawakened.Network.Extensions;
using Server.Reawakened.Network.Protocols;
using Server.Reawakened.Players;
using Server.Reawakened.Players.Extensions;
using Server.Reawakened.Players.Models;
using Server.Reawakened.Rooms.Extensions;
using Server.Reawakened.Rooms.Models.Entities;
using Server.Reawakened.Rooms.Models.Planes;
using Server.Reawakened.XMLs.Bundles;
using Server.Reawakened.XMLs.BundlesInternal;
namespace Protocols.External._h__HotbarHandler;
public class UseSlot : ExternalProtocol
{
    public override string ProtocolName => "hu";
    public ILogger<UseSlot> Logger { get; set; }
    public ItemCatalog ItemCatalog { get; set; }
    public QuestCatalog QuestCatalog { get; set; }
    public InternalObjective ObjectiveCatalog { get; set; }
    public ServerRConfig ServerRConfig { get; set; }
    public TimerThread TimerThread { get; set; }
    public override void Run(string[] message)
    {
        var hotbarSlotId = int.Parse(message[5]);
        var targetUserId = int.Parse(message[6]);
        var position = new Vector3Model()
        {
            X = Convert.ToSingle(message[7]),
            Y = Convert.ToSingle(message[8]),
            Z = Convert.ToSingle(message[9])
        };
        Logger.LogDebug("Player used hotbar slot {hotbarId} on {userId} at coordinates {position}",
            hotbarSlotId, targetUserId, position);
        var direction = Player.TempData.Direction;
        var slotItem = Player.Character.Data.Hotbar.HotbarButtons[hotbarSlotId];
        var usedItem = ItemCatalog.GetItemFromId(slotItem.ItemId);
        switch (usedItem.ItemActionType)
        {
            case ItemActionType.Drop:
                HandleDrop(usedItem, position, direction);
                break;
            case ItemActionType.Throw:
                HandleRangedWeapon(usedItem, position, direction);
                break;
            case ItemActionType.Genericusing:
            case ItemActionType.Drink:
            case ItemActionType.Eat:
                HandleConsumable(usedItem, Logger, hotbarSlotId);
                break;
            case ItemActionType.Melee:
                HandleMeleeWeapon(usedItem, position, direction);
                break;
            case ItemActionType.Pet:
                HandlePet(usedItem);
                break;
            case ItemActionType.Relic:
                HandleRelic(usedItem);
                break;
            default:
                Logger.LogError("Could not find how to handle item action type {ItemAction} for user {UserId}",
                    usedItem.ItemActionType, targetUserId);
                break;
        }
    }
    private void HandlePet(ItemDescription usedItem)
    {
        Player.SendXt("ZE", Player.UserId, usedItem.ItemId, 1);
        Player.Character.Data.PetItemId = usedItem.ItemId;
    }
    private void HandleRelic(ItemDescription usedItem)
    {
        StatusEffect_SyncEvent itemEffect = null;
        foreach (var effect in usedItem.ItemEffects)
            itemEffect = new StatusEffect_SyncEvent(Player.GameObjectId.ToString(), Player.Room.Time,
                    (int)effect.Type, effect.Value, effect.Duration, true, usedItem.PrefabName, false);

        Player.SendSyncEventToPlayer(itemEffect);
    }

    private void HandleDrop(ItemDescription usedItem, Vector3Model position, int direction)
    {
        var isLeft = direction > 0;
        var dropDirection = isLeft ? 1 : -1;
        var platform = new GameObjectModel();
        var planeName = position.Z > 10 ? "Plane1" : "Plane0";
        position.Z = 0;
        var dropItemData = new DroppedItemData()
        {
            DropDirection = dropDirection,
            Position = position,
            UsedItem = usedItem
        };
        TimerThread.DelayCall(DropItem, dropItemData, TimeSpan.FromMilliseconds(1000), TimeSpan.Zero, 1);
    }
    private class DroppedItemData()
    {
        public int DropDirection { get; set; }
        public ItemDescription UsedItem { get; set; }
        public Vector3Model Position { get; set; }
    }
    private void DropItem(object data)
    {
        var dropData = (DroppedItemData)data;
        var dropItem = new LaunchItem_SyncEvent(Player.GameObjectId.ToString(), Player.Room.Time,
            Player.TempData.Position.X + dropData.DropDirection, Player.TempData.Position.Y, Player.TempData.Position.Z,
            0, 0, 3, 0, dropData.UsedItem.PrefabName);
        Player.Room.SendSyncEvent(dropItem);
        foreach (var entity in Player.Room.Entities)
        {
            foreach (var component in entity.Value
                .Where(comp => Vector3Model.Distance(dropData.Position, comp.Position) <= 5.4f))
            {
                var prefabName = component.PrefabName;
                var objectId = component.Id;
                if (component is HazardControllerComp or BreakableEventControllerComp)
                {
                    var bombData = new BombData()
                    {
                        PrefabName = prefabName,
                        Component = component,
                        ObjectId = objectId
                    };
                    TimerThread.DelayCall(ExplodeBomb, bombData, TimeSpan.FromMilliseconds(2650), TimeSpan.Zero, 1);
                }
            }
        }
    }
    private class BombData()
    {
        public string PrefabName { get; set; }
        public int ObjectId { get; set; }
        public BaseComponent Component { get; set; }
    }
    private void ExplodeBomb(object data)
    {
        var bData = (BombData)data;
        Logger.LogInformation("Found close hazard {PrefabName} with Id {ObjectId}", bData.PrefabName, bData.ObjectId);
        if (bData.Component is BreakableEventControllerComp breakableObjEntity)
            breakableObjEntity.Destroy(Player);
        else if (bData.Component is InterObjStatusComp enemyEntity)
            enemyEntity.SendDamageEvent(Player);
    }

    private void HandleConsumable(ItemDescription usedItem, ILogger<UseSlot> logger, int hotbarSlotId)
    {
        HandleItemEffectBuff(usedItem, logger);

        var removeFromHotbar = true;

        if (usedItem.InventoryCategoryID is
            ItemFilterCategory.WeaponAndAbilities or
            ItemFilterCategory.Pets or
            ItemFilterCategory.Keys or
            ItemFilterCategory.QuestItems)
            removeFromHotbar = false;

        if (removeFromHotbar)
            RemoveFromHotbar(Player.Character, usedItem, hotbarSlotId);
    }

    public void HandleItemEffectBuff(ItemDescription usedItem, ILogger<UseSlot> logger)
    {
        var effect = usedItem.ItemEffects.FirstOrDefault();

        Player.Room.SendSyncEvent(new StatusEffect_SyncEvent(Player.GameObjectId.ToString(), Player.Room.Time,
                            (int)effect.Type, effect.Value, effect.Duration, true, usedItem.PrefabName, true));

        var effectCategory = usedItem.ItemEffects.FirstOrDefault().Type;

        switch (effectCategory)
        {
            case ItemEffectType.Healing:
            case ItemEffectType.HealthBoost:
            case ItemEffectType.IncreaseHealing:
            case ItemEffectType.Regeneration:
                Player.HealCharacter(usedItem, effectCategory);
                break;
            case ItemEffectType.IncreaseAirDamage:
            case ItemEffectType.IncreaseAllResist:
            case ItemEffectType.Shield:
            case ItemEffectType.WaterBreathing:
            case ItemEffectType.PetRegainEnergy:
            case ItemEffectType.PetEnergyValue:
            case ItemEffectType.BananaMultiplier:
            case ItemEffectType.ExperienceMultiplier:
            case ItemEffectType.Defence:
                break;
            case ItemEffectType.Invalid:
            case ItemEffectType.Unknown:
            case ItemEffectType.Unknown_61:
            case ItemEffectType.Unknown_70:
            case ItemEffectType.Unknown_74:
            default:
                logger.LogError("Unknown ItemEffectType of ({effectType}) for item {usedItemName}", effectCategory, usedItem.PrefabName);
                break;
        }
    }

    private void HandleRangedWeapon(ItemDescription usedItem, Vector3Model position, int direction)
    {
        var rand = new System.Random();
        var prjId = Math.Abs(rand.Next());
        while (Player.Room.GameObjectIds.Contains(prjId))
            prjId = Math.Abs(rand.Next());
        var prj = new ProjectileEntity(Player, prjId, position.X, position.Y, position.Z, direction, 3, usedItem);
        Player.Room.Projectiles.Add(prjId, prj);
    }
    private void HandleMeleeWeapon(ItemDescription usedItem, Vector3Model position, int direction)
    {
        var monsters = new List<GameObjectModel>();
        var planeName = position.Z > 10 ? "Plane1" : "Plane0";
        var hitEvent = new Melee_SyncEvent(Player.GameObjectId.ToString(), Player.Room.Time, position.X, position.Y, position.Z, direction, 1, 1, 0, usedItem.PrefabName);
        Player.Room.SendSyncEvent(hitEvent);
        position.Z = 0;
        var meleeWeapon = ItemCatalog.GetItemFromId(usedItem.ItemId);
        var weaponPrefabName = meleeWeapon.PrefabName;
        var meleeSyncEvent = new Melee_SyncEvent(Player.GameObjectId.ToString(), Player.Room.Time,
            position.X, position.Y, position.Z, 0, 0, 100, 0, weaponPrefabName);
        Player.Room.SendSyncEvent(meleeSyncEvent);
        foreach (var obj in
                 Player.Room.Planes[planeName].GameObjects.Values
                     .Where(obj => Vector3Model.Distance(position, obj.ObjectInfo.Position) <= 3.4f)
                )
        {
            var isLeft = direction > 0;
            if (isLeft)
            {
                if (obj.ObjectInfo.Position.X < position.X)
                    continue;
            }
            else
            {
                if (obj.ObjectInfo.Position.X > position.X)
                    continue;
            }
            var objectId = obj.ObjectInfo.ObjectId;
            var prefabName = obj.ObjectInfo.PrefabName;
            if (Player.Room.Entities.TryGetValue(objectId, out var entityComponents))
                foreach (var component in entityComponents)
                    if (component is TriggerCoopControllerComp triggerCoopEntity)
                        triggerCoopEntity.TriggerInteraction(ActivationType.NormalDamage, Player);
                    else if (component is BreakableEventControllerComp breakableObjEntity)
                        breakableObjEntity.Destroy(Player);
                    else if (component is InterObjStatusComp enemyEntity)
                        enemyEntity.SendDamageEvent(Player);
        }
    }
    private void RemoveFromHotbar(CharacterModel character, ItemDescription item, int hotbarSlotId)
    {
        character.Data.Inventory.Items[item.ItemId].Count--;
        if (character.Data.Inventory.Items[item.ItemId].Count <= 0)
        {
            character.Data.Hotbar.HotbarButtons.Remove(hotbarSlotId);
            SendXt("hu", character.Data.Hotbar);
            character.Data.Inventory.Items[item.ItemId].Count = -1;
        }
        Player.SendUpdatedInventory(false);
    }
}
