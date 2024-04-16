﻿using A2m.Server;
using Microsoft.Extensions.Logging;
using Server.Base.Timers.Services;
using Server.Reawakened.Entities.Colliders;
using Server.Reawakened.Entities.Colliders.Abstractions;
using Server.Reawakened.Entities.Components.GameObjects.Breakables.Interfaces;
using Server.Reawakened.Entities.Components.GameObjects.InterObjs.Interfaces;
using Server.Reawakened.Entities.Components.GameObjects.Spawners.Abstractions;
using Server.Reawakened.Players;
using Server.Reawakened.Players.Extensions;
using Server.Reawakened.Players.Helpers;
using Server.Reawakened.Rooms.Extensions;
using Server.Reawakened.Rooms.Models.Entities;
using Server.Reawakened.XMLs.Bundles.Base;
using Server.Reawakened.XMLs.Bundles.Internal;
using UnityEngine;
using Room = Server.Reawakened.Rooms.Room;

namespace Server.Reawakened.Entities.Components.GameObjects.Breakables;

public class BreakableEventControllerComp : Component<BreakableEventController>, IDestructible
{
    public ItemCatalog ItemCatalog { get; set; }
    public InternalLoot LootCatalog { get; set; }
    public TimerThread TimerThread { get; set; }
    public InternalAchievement InternalAchievement { get; set; }
    public ILogger<BreakableEventControllerComp> Logger { get; set; }

    public int MaxHealth { get; set; }
    public int NumberOfHits;
    public bool OverrideDeath;

    private int _health;
    private BaseSpawnerControllerComp _spawner;
    private IDamageable _damageable;

    public override void InitializeComponent()
    {
        NumberOfHits = 0;

        MaxHealth = 1;
        _health = MaxHealth;

        var box = new Rect(Rectangle.X, Rectangle.Y, Rectangle.Width, Rectangle.Height);
        var position = new Vector3(Position.X, Position.Y, Position.Z);

        Room.AddCollider(new BreakableCollider(Id, position, box, ParentPlane, Room));
    }

    public void PostInit()
    {
        var spawner = Room.GetEntityFromId<BaseSpawnerControllerComp>(Id);
        var damagable = Room.GetEntityFromId<IDamageable>(Id);

        if (spawner != null)
        {
            _spawner = spawner;
            MaxHealth = _spawner.Health;
            OverrideDeath = true;
        }

        if (damagable != null)
        {
            _damageable = damagable;

            MaxHealth = damagable.MaxHealth;
            _damageable.CurrentHealth = MaxHealth;
        }

        _health = MaxHealth;
    }

    public void Damage(int damage, Elemental damageType, Player origin)
    {
        if (Room.IsObjectKilled(Id))
            return;

        var damagable = Room.GetEntityFromId<IDamageable>(Id);

        if (damagable != null)
            _health -= damagable.GetDamageAmount(damage, damageType);
        else
            _health -= damage;

        NumberOfHits++;

        Logger.LogInformation("Damaged object: '{PrefabName}' ({Id})", PrefabName, Id);

        if (damagable is IBreakable breakable)
            if (breakable.NumberOfHitsToBreak > 0)
                if (NumberOfHits >= breakable.NumberOfHitsToBreak)
                    _health = 0;

        if (_damageable != null)
            _damageable.CurrentHealth = _health;

        if (_health < 0)
            _health = 0;

        origin.Room.SendSyncEvent(new AiHealth_SyncEvent(Id.ToString(), Room.Time, _health, damage, 0, 0, origin.CharacterName, false, true));

        if (_health <= 0)
        {
            origin.GrantLoot(Id, LootCatalog, ItemCatalog, InternalAchievement, Logger);
            origin.SendUpdatedInventory();

            Room.KillEntity(origin, Id);
        }
    }

    public override void NotifyCollision(NotifyCollision_SyncEvent notifyCollisionEvent, Player player) { }

    public void Destroy(Player player, Room room, string id)
    {
        player?.CheckObjective(ObjectiveEnum.Score, Id, PrefabName, 1, ItemCatalog);

        room.RemoveEnemy(id);
    }
}
