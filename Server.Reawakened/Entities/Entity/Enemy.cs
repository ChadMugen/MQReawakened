﻿using Server.Base.Core.Extensions;
using Server.Reawakened.Configs;
using Server.Reawakened.Entities.AIBehavior;
using Server.Reawakened.Entities.Components;
using Server.Reawakened.Entities.Entity.Utils;
using Server.Reawakened.Entities.Stats;
using Server.Reawakened.Players;
using Server.Reawakened.Players.Helpers;
using Server.Reawakened.Rooms;
using Server.Reawakened.Rooms.Extensions;
using Server.Reawakened.Rooms.Models.Entities;
using Server.Reawakened.Rooms.Models.Entities.ColliderType;
using Server.Reawakened.Rooms.Models.Planes;
using Server.Reawakened.XMLs.Bundles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;

namespace Server.Reawakened.Entities.Entity;

public abstract class Enemy : IDestructible
{
    public bool Init;
    public Room Room;
    public string Id;

    public Vector3 SpawnPosition;
    public Vector3 Position;
    public Rect DetectionRange;
    public EnemyCollider Hitbox;
    public string ParentPlane;
    public int Health;

    private float _negativeHeight;

    public BaseComponent Entity;
    public AIStatsGlobalComp Global;
    public AIStatsGenericComp Generic;
    public InterObjStatusComp Status;
    public EnemyControllerComp EnemyController;

    public GlobalProperties EnemyGlobalProps;
    public AIProcessData AiData;
    public AIBaseBehavior AiBehavior;
    public BehaviorModel BehaviorList;

    public AISyncEventHelper SyncBuilder;

    public Enemy(Room room, string entityId, BaseComponent baseEntity)
    {

        Room = room;
        Id = entityId;
        Health = 50;

        Entity = baseEntity;
        EnemyController = (EnemyControllerComp)baseEntity;
        var entityList = room.Entities.Values.SelectMany(s => s);
        foreach (var entity in entityList.Where(x => x.Id == Id))
        {
            if (entity is AIStatsGlobalComp global)
                Global = global;
            else if (entity is AIStatsGenericComp generic)
                Generic = generic;
            else if (entity is InterObjStatusComp status)
                Status = status;
        }

        ParentPlane = Entity.ParentPlane;
        Position = new Vector3(Entity.Position.X, Entity.Position.Y, Entity.Position.Z);
        if (ParentPlane == "Plane1")
            Position.z = 20;
        SpawnPosition = Position;

        EnemyGlobalProps = new GlobalProperties(
            Global.Global_DetectionLimitedByPatrolLine,
            Global.Global_BackDetectionRangeX,
            Global.Global_viewOffsetY,
            Global.Global_BackDetectionRangeUpY,
            Global.Global_BackDetectionRangeDownY,
            Global.Global_ShootOffsetX,
            Global.Global_ShootOffsetY,
            Global.Global_FrontDetectionRangeX,
            Global.Global_FrontDetectionRangeUpY,
            Global.Global_FrontDetectionRangeDownY,
            Global.Global_Script,
            Global.Global_ShootingProjectilePrefabName,
            Global.Global_DisableCollision,
            Global.Global_DetectionSourceOnPatrolLine,
            Global.Aggro_AttackBeyondPatrolLine
        );

        AiData = new AIProcessData();
        AiData.SetStats(EnemyGlobalProps);
        AiData.SyncInit_PosX = Position.x;
        AiData.SyncInit_PosY = Position.y;
        AiData.Sync_PosX = Position.x;
        AiData.Sync_PosY = Position.y;
        AiData.Intern_SpawnPosX = Position.x;
        AiData.Intern_SpawnPosY = Position.y;
        AiData.Intern_SpawnPosZ = Position.z;
        AiData.SyncInit_Dir = Generic.Patrol_ForceDirectionX;
        AiData.SyncInit_ProgressRatio = Generic.Patrol_InitialProgressRatio;

        _negativeHeight = 0;
        if (Entity.Scale.Y < 0)
            _negativeHeight = Entity.Rectangle.Height;

        Hitbox = new EnemyCollider(Id, new Vector3Model { X = Position.x, Y = Position.y - _negativeHeight, Z = Position.z }, 
            Entity.Rectangle.Width, Entity.Rectangle.Height, Entity.ParentPlane, Room);
        Room.Colliders.Add(Id, Hitbox);
    }

    public virtual void Initialize()
    {
        Init = true;
    }

    public virtual void Update()
    {
        if (!Init)
            Initialize();

        switch (AiBehavior)
        {
            //AIBehavior_Acting
            case AIBehavior_LookAround:
                HandleLookAround();
                break;
            case AIBehavior_Patrol:
                HandlePatrol();
                break;
            case AIBehavior_ComeBack:
                HandleComeBack();
                break;
            case AIBehavior_Aggro:
                HandleAggro();
                break;
            //AIBehavior_Shooting
            //AIBehavior_Projectile
            //AIBehavior_Bomber
            //AIBehavior_Grenadier
            //AIBehavior_Stomper
            //AIBehavior_Idle
            //AIBehavior_Stinger
            //AIBehavior_Spike
            //AIBehavior_GoTo
        }

        AiBehavior.Update(ref AiData, Room.Time);

        Position = new Vector3(AiData.Sync_PosX, AiData.Sync_PosY, Position.z);
        Entity.Position.X = Position.x;
        Entity.Position.Y = Position.y;
        Entity.Position.Z = Position.z;
        Hitbox.Position = new Vector3(AiData.Sync_PosX, AiData.Sync_PosY - _negativeHeight, Position.z);

    }

    public virtual string WriteBehaviorList()
    {
        EnemyBehaviorFactory BehaviorFactory;
        var outBehaviorList = string.Empty;
        List<string> behaviorList = [];

        SeparatedStringBuilder bList = new SeparatedStringBuilder('`');
        SeparatedStringBuilder bDefinesList = new SeparatedStringBuilder('|');

        foreach (var behavior in BehaviorList.BehaviorData)
        {
            bDefinesList.Append(behavior.Key);
            foreach (var behaviorData in behavior.Value.DataList)
            {
                BehaviorFactory
            }

        }

        return outBehaviorList;
    }

    public virtual void Damage(int damage, Player origin)
    {
        Health -= damage;

        var damageEvent = new AiHealth_SyncEvent(Id.ToString(), Room.Time, Health, damage, 0, 0, origin.CharacterName, false, true);
        Room.SendSyncEvent(damageEvent);

        if (Health <= 0)
        {
            if (EnemyController.OnDeathTargetID != null && Room.Entities.TryGetValue(EnemyController.OnDeathTargetID, out var foundTrigger) && EnemyController.OnDeathTargetID != "0")
            {
                foreach (var component in foundTrigger)
                {
                    if (component is TriggerReceiverComp trigger)
                        trigger.Trigger(true);
                }
            }

            Room.SendSyncEvent(SyncBuilder.AIDie(Entity, string.Empty, 10, true, origin.GameObjectId, false));
            Destroy(Room, Id);
        }
    }

    public virtual bool PlayerInRange(Vector3Model pos) =>
        Position.x - DetectionRange.width / 2 < pos.X && pos.X < Position.x + DetectionRange.width / 2 &&
            Position.y < pos.Y && pos.Y < Position.y + DetectionRange.height && Position.z == pos.Z;

    public virtual void HandlePatrol() { }
    public virtual void HandleAggro() { }
    public virtual void HandleLookAround() { }
    public virtual void HandleComeBack() { }

    //This one is not in the helper because it needs too many arguments and too much reformatted data to quantify being there.
    public AIInit_SyncEvent AIInit(float healthMod, float sclMod, float resMod)
    {
        var aiInit = new AIInit_SyncEvent(Id.ToString(), Room.Time, Position.x, Position.y, Position.z, Position.x, Position.y, Generic.Patrol_InitialProgressRatio,
        Status.MaxHealth, Status.MaxHealth, healthMod, sclMod, resMod, Status.Stars, EnemyController.Level, EnemyGlobalProps.ToString(), WriteBehaviorList());
        aiInit.EventDataList[2] = Position.x;
        aiInit.EventDataList[3] = Position.y;
        aiInit.EventDataList[4] = Position.z;
        return aiInit;
    }

    public void Destroy(Room room, string id)
    {
        room.Entities.Remove(id);
        room.Enemies.Remove(id);
        room.Colliders.Remove(id);
    }
}
