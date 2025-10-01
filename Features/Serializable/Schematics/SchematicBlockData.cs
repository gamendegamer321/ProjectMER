using AdminToys;
using Interactables.Interobjects;
using Interactables.Interobjects.DoorUtils;
using InventorySystem.Items.Firearms.Attachments;
using LabApi.Features.Wrappers;
using Mirror;
using ProjectMER.Events.Handlers.Internal;
using ProjectMER.Features.Enums;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable.Lockers;
using UnityEngine;
using Utf8Json;
using BreakableDoor = Interactables.Interobjects.BreakableDoor;
using CameraType = ProjectMER.Features.Enums.CameraType;
using CapybaraToy = LabApi.Features.Wrappers.CapybaraToy;
using ElevatorDoor = Interactables.Interobjects.ElevatorDoor;
using LightSourceToy = AdminToys.LightSourceToy;
using Locker = MapGeneration.Distributors.Locker;
using Object = UnityEngine.Object;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;
using Random = System.Random;
using TextToy = AdminToys.TextToy;
using WaypointToy = AdminToys.WaypointToy;

namespace ProjectMER.Features.Serializable.Schematics;

public class SchematicBlockData
{
    public virtual string Name { get; set; }

    public virtual int ObjectId { get; set; }

    public virtual int ParentId { get; set; }

    public virtual string AnimatorName { get; set; }

    public virtual Vector3 Position { get; set; }

    public virtual Vector3 Rotation { get; set; }

    public virtual Vector3 Scale { get; set; }

    public virtual BlockType BlockType { get; set; }

    public virtual Dictionary<string, object> Properties { get; set; }

    public static readonly BlockType[] NoParentTypes =
    [
        BlockType.Door, BlockType.Prefab, BlockType.Sinkhole
    ];

    private static readonly Random Random = new();

    public GameObject Create(SchematicObject schematicObject, Transform parentTransform)
    {
        GameObject gameObject = BlockType switch
        {
            BlockType.Empty => CreateEmpty(),
            BlockType.Primitive => CreatePrimitive(),
            BlockType.Light => CreateLight(),
            BlockType.Pickup => CreatePickup(schematicObject),
            BlockType.Workstation => CreateWorkstation(),
            BlockType.Text => CreateText(),
            BlockType.Interactable => CreateInteractable(),
            BlockType.Waypoint => CreateWaypoint(),
            BlockType.Capybara => CreateCapybara(),
            BlockType.Door => CreateDoor(),
            BlockType.Elevator => CreateElevator(),
            BlockType.Prefab => CreatePrefab(),
            BlockType.Sinkhole => CreateSinkhole(),
            BlockType.Camera => CreateCamera(),
            BlockType.Generator => CreateGenerator(),
            BlockType.Locker => CreateLocker(),
            _ => CreateEmpty(true)
        };

        gameObject.name = Name;

        Transform transform = gameObject.transform;
        transform.SetParent(parentTransform);
        transform.SetLocalPositionAndRotation(Position, Quaternion.Euler(Rotation));

        transform.localScale = BlockType switch
        {
            BlockType.Empty when Scale == Vector3.zero => Vector3.one,
            BlockType.Waypoint => Scale * SerializableWaypoint.ScaleMultiplier,
            _ => Scale,
        };

        if (gameObject.TryGetComponent(out AdminToyBase adminToyBase))
        {
            if (Properties != null && Properties.TryGetValue("Static", out object isStatic) &&
                Convert.ToBoolean(isStatic))
            {
                adminToyBase.NetworkIsStatic = true;
            }
            else
            {
                adminToyBase.NetworkMovementSmoothing = 60;
            }
        }

        if (NoParentTypes.Contains(BlockType)) // Temporarily remove the parent again
        {
            transform.SetParent(null);
        }

        return gameObject;
    }

    private GameObject CreateEmpty(bool fallback = false)
    {
        if (fallback)
            Logger.Warn($"{BlockType} is not yet implemented. Object will be an empty GameObject instead.");

        PrimitiveObjectToy primitive = GameObject.Instantiate(PrefabManager.PrimitiveObject);
        primitive.NetworkPrimitiveFlags = PrimitiveFlags.None;

        return primitive.gameObject;
    }

    private GameObject CreatePrimitive()
    {
        PrimitiveObjectToy primitive = GameObject.Instantiate(PrefabManager.PrimitiveObject);

        primitive.NetworkPrimitiveType = (PrimitiveType)Convert.ToInt32(Properties["PrimitiveType"]);
        primitive.NetworkMaterialColor = Properties["Color"].ToString().GetColorFromString();

        PrimitiveFlags primitiveFlags;
        if (Properties.TryGetValue("PrimitiveFlags", out object flags))
        {
            primitiveFlags = (PrimitiveFlags)Convert.ToByte(flags);
        }
        else
        {
            // Backward compatibility
            primitiveFlags = PrimitiveFlags.Visible;
            if (Scale.x >= 0f)
                primitiveFlags |= PrimitiveFlags.Collidable;
        }

        primitive.NetworkPrimitiveFlags = primitiveFlags;

        return primitive.gameObject;
    }

    private GameObject CreateLight()
    {
        LightSourceToy light = GameObject.Instantiate(PrefabManager.LightSource);

        light.NetworkLightType = Properties.TryGetValue("LightType", out object lightType)
            ? (LightType)Convert.ToInt32(lightType)
            : LightType.Point;
        light.NetworkLightColor = Properties["Color"].ToString().GetColorFromString();
        light.NetworkLightIntensity = Convert.ToSingle(Properties["Intensity"]);
        light.NetworkLightRange = Convert.ToSingle(Properties["Range"]);

        if (Properties.TryGetValue("Shadows", out object shadows))
        {
            // Backward compatibility
            light.NetworkShadowType = Convert.ToBoolean(shadows) ? LightShadows.Soft : LightShadows.None;
        }
        else
        {
            light.NetworkShadowType = (LightShadows)Convert.ToInt32(Properties["ShadowType"]);
            light.NetworkLightShape = (LightShape)Convert.ToInt32(Properties["Shape"]);
            light.NetworkSpotAngle = Convert.ToSingle(Properties["SpotAngle"]);
            light.NetworkInnerSpotAngle = Convert.ToSingle(Properties["InnerSpotAngle"]);
            light.NetworkShadowStrength = Convert.ToSingle(Properties["ShadowStrength"]);
        }

        return light.gameObject;
    }

    private GameObject CreatePickup(SchematicObject schematicObject)
    {
        if (Properties.TryGetValue("Chance", out object property) &&
            UnityEngine.Random.Range(0, 101) > Convert.ToSingle(property))
            return new("Empty Pickup");

        Pickup pickup = Pickup.Create((ItemType)Convert.ToInt32(Properties["ItemType"]), Vector3.zero)!;
        if (Properties.ContainsKey("Locked"))
            PickupEventsHandler.ButtonPickups.Add(pickup.Serial, schematicObject);

        return pickup.GameObject;
    }

    private GameObject CreateWorkstation()
    {
        WorkstationController workstation = GameObject.Instantiate(PrefabManager.Workstation);
        workstation.NetworkStatus = (byte)(Properties.TryGetValue("IsInteractable", out object isInteractable) &&
                                           Convert.ToBoolean(isInteractable)
            ? 0
            : 4);

        return workstation.gameObject;
    }

    private GameObject CreateText()
    {
        TextToy text = GameObject.Instantiate(PrefabManager.Text);

        text.TextFormat = Convert.ToString(Properties["Text"]);
        text.DisplaySize = Properties["DisplaySize"].ToVector2() * 20f;

        return text.gameObject;
    }

    private GameObject CreateInteractable()
    {
        InvisibleInteractableToy interactable = GameObject.Instantiate(PrefabManager.Interactable);
        interactable.NetworkShape = (InvisibleInteractableToy.ColliderShape)Convert.ToInt32(Properties["Shape"]);
        interactable.NetworkInteractionDuration = Convert.ToSingle(Properties["InteractionDuration"]);
        interactable.NetworkIsLocked =
            Properties.TryGetValue("IsLocked", out object isLocked) && Convert.ToBoolean(isLocked);

        return interactable.gameObject;
    }

    private GameObject CreateWaypoint()
    {
        WaypointToy waypoint = GameObject.Instantiate(PrefabManager.Waypoint);
        waypoint.NetworkPriority = byte.MaxValue;

        return waypoint.gameObject;
    }

    private GameObject CreateCapybara()
    {
        var capybara = CapybaraToy.Create();
        capybara.CollidersEnabled = Convert.ToBoolean(Properties["CollidersEnabled"]);

        return capybara.GameObject;
    }

    private GameObject CreateDoor()
    {
        var type = (DoorType)Convert.ToInt32(Properties["DoorType"]);

        DoorVariant door;
        switch (type)
        {
            case DoorType.Lcz:
                door = Object.Instantiate(PrefabManager.DoorLcz);
                break;
            case DoorType.Hcz:
                door = Object.Instantiate(PrefabManager.DoorHcz);
                break;
            case DoorType.Ez:
                door = Object.Instantiate(PrefabManager.DoorEz);
                break;
            case DoorType.Bulkdoor:
                door = Object.Instantiate(PrefabManager.DoorHeavyBulk);
                break;
            case DoorType.Gate:
                door = Object.Instantiate(PrefabManager.DoorGate);
                break;
            default:
                return CreateEmpty(true);
        }

        if (type is DoorType.Lcz or DoorType.Hcz or DoorType.Ez)
        {
            if (door is BreakableDoor breakableDoor)
            {
                breakableDoor.RemainingHealth = Convert.ToSingle(Properties["Health"]);
                breakableDoor.IgnoredDamageSources =
                    (DoorDamageType)Convert.ToInt32(Properties["IgnoredDamageSources"]);
                breakableDoor.IsDestroyed = Convert.ToBoolean(Properties["IsDestroyed"]);
                breakableDoor._nonInteractable = !Convert.ToBoolean(Properties["Interactable"]);
                breakableDoor.IsScp106Passable = Convert.ToBoolean(Properties["Scp106Passable"]);
            }
        }

        var wrappedDoor = Door.Get(door);
        wrappedDoor.IsOpened = Convert.ToBoolean(Properties["SpawnOpened"]);
        wrappedDoor.Permissions = (DoorPermissionFlags)Convert.ToInt32(Properties["Permissions"]);
        wrappedDoor.IsLocked = Convert.ToBoolean(Properties["Locked"]);

        return door.gameObject;
    }

    private GameObject CreateElevator()
    {
        var type = (ElevatorType)Convert.ToInt32(Properties["ElevatorType"]);

        ElevatorChamber elevator;
        switch (type)
        {
            case ElevatorType.Default:
                elevator = Object.Instantiate(PrefabManager.ElevatorChamber);
                break;
            case ElevatorType.Gates:
                elevator = Object.Instantiate(PrefabManager.ElevatorChamberGates);
                break;
            case ElevatorType.Nuke:
                elevator = Object.Instantiate(PrefabManager.ElevatorChamberNuke);
                break;
            case ElevatorType.Cargo:
                elevator = Object.Instantiate(PrefabManager.ElevatorChamberCargo);
                break;
            default:
                return CreateEmpty(true);
        }

        var count = Convert.ToInt32(Properties["DoorCount"]);
        var doors = new List<ElevatorDoor>();
        for (var i = 0; i < count; i++)
        {
            var obj = new GameObject("Spawned Elevator Door")
            {
                transform =
                {
                    position = Convert.ToString(Properties[$"Door-{i}-doorPosition"]).ToVector3()
                }
            };

            var door = obj.AddComponent<ElevatorDoor>();

            door._targetPosition = Convert.ToString(Properties[$"Door-{i}-targetPosition"]).ToVector3();
            door._topPosition = Convert.ToString(Properties[$"Door-{i}-topPosition"]).ToVector3();
            door._bottomPosition = Convert.ToString(Properties[$"Door-{i}-bottomPosition"]).ToVector3();
            door.Chamber = elevator;

            doors.Add(door);
        }

        elevator._floorDoors = doors;
        elevator._lastArrivedDestination = doors[Convert.ToInt32(Properties["InitialDoor"])];

        return elevator.gameObject;
    }

    private GameObject CreatePrefab()
    {
        var prefab = Convert.ToString(Properties["Prefab"]);
        var found = uint.TryParse(prefab, out var id)
            ? NetworkClient.prefabs[id]
            : NetworkClient.prefabs.Values
                .FirstOrDefault(x => x.name.Equals(prefab, StringComparison.CurrentCultureIgnoreCase));

        if (found != null) return Object.Instantiate(found);

        Logger.Warn($"Could not find the prefab: {prefab}");
        return CreateEmpty();
    }

    private GameObject CreateSinkhole()
    {
        var sinkhole = SinkholeHazard.Spawn(Position, Quaternion.Euler(Rotation), Scale);
        return sinkhole.Base.gameObject;
    }

    private GameObject CreateCamera()
    {
        var type = (CameraType)Convert.ToInt32(Properties["CameraType"]);

        Scp079CameraToy camera;
        switch (type)
        {
            case CameraType.Lcz:
                camera = Object.Instantiate(PrefabManager.CameraLcz);
                break;
            case CameraType.Hcz:
                camera = Object.Instantiate(PrefabManager.CameraHcz);
                break;
            case CameraType.Ez:
                camera = Object.Instantiate(PrefabManager.CameraEz);
                break;
            case CameraType.EzArm:
                camera = Object.Instantiate(PrefabManager.CameraEzArm);
                break;
            case CameraType.Sz:
                camera = Object.Instantiate(PrefabManager.CameraSz);
                break;
            default:
                return CreateEmpty(true);
        }

        camera.VerticalConstraint = Convert.ToString(Properties["VerticalConstraint"]).ToVector2();
        camera.HorizontalConstraint = Convert.ToString(Properties["HorizontalConstraint"]).ToVector2();
        camera.ZoomConstraint = Convert.ToString(Properties["ZoomConstraint"]).ToVector2();

        camera.Label = Convert.ToString(Properties["Label"]);

        return camera.gameObject;
    }

    private GameObject CreateGenerator()
    {
        var generator = Object.Instantiate(PrefabManager.Generator);
        generator._requiredPermission = (DoorPermissionFlags)Convert.ToInt32(Properties["RequiredPermissions"]);
        generator._totalActivationTime = Convert.ToSingle(Properties["ActivationTime"]);
        generator._totalDeactivationTime = Convert.ToSingle(Properties["DeactivationTime"]);
        generator.IsOpen = Convert.ToBoolean(Properties["IsOpen"]);
        generator.IsUnlocked = Convert.ToBoolean(Properties["IsUnlocked"]);
        generator.Engaged = Convert.ToBoolean(Properties["Engaged"]);

        return generator.gameObject;
    }

    private GameObject CreateLocker()
    {
        if (Random.Next(100) >= Convert.ToInt32(Properties["Chance"]))
        {
            return CreateEmpty();
        }

        var type = (LockerType)Convert.ToInt32(Properties["LockerType"]);

        Locker locker;
        switch (type)
        {
            case LockerType.PedestalScp500:
                locker = Object.Instantiate(PrefabManager.PedestalScp500);
                break;
            case LockerType.LargeGun:
                locker = Object.Instantiate(PrefabManager.LockerLargeGun);
                break;
            case LockerType.RifleRack:
                locker = Object.Instantiate(PrefabManager.LockerRifleRack);
                break;
            case LockerType.Misc:
                locker = Object.Instantiate(PrefabManager.LockerMisc);
                break;
            case LockerType.Medkit:
                locker = Object.Instantiate(PrefabManager.LockerRegularMedkit);
                break;
            case LockerType.Adrenaline:
                locker = Object.Instantiate(PrefabManager.LockerAdrenalineMedkit);
                break;
            case LockerType.PedestalScp018:
                locker = Object.Instantiate(PrefabManager.PedestalScp018);
                break;
            case LockerType.PedestalScp207:
                locker = Object.Instantiate(PrefabManager.PedestalScp207);
                break;
            case LockerType.PedestalScp244:
                locker = Object.Instantiate(PrefabManager.PedestalScp244);
                break;
            case LockerType.PedestalScp268:
                locker = Object.Instantiate(PrefabManager.PedestalScp268);
                break;
            case LockerType.PedestalScp1853:
                locker = Object.Instantiate(PrefabManager.PedestalScp1853);
                break;
            case LockerType.PedestalScp2176:
                locker = Object.Instantiate(PrefabManager.PedestalScp2176);
                break;
            case LockerType.PedestalScpScp1576:
                locker = Object.Instantiate(PrefabManager.PedestalScp1576);
                break;
            case LockerType.PedestalAntiScp207:
                locker = Object.Instantiate(PrefabManager.PedestalAntiScp207);
                break;
            case LockerType.PedestalScp1344:
                locker = Object.Instantiate(PrefabManager.PedestalScp1344);
                break;
            case LockerType.ExperimentalWeapon:
                locker = Object.Instantiate(PrefabManager.LockerExperimentalWeapon);
                break;
            case LockerType.None:
            default:
                return CreateEmpty(true);
        }

        var keycardPermissions = (DoorPermissionFlags)Convert.ToInt32(Properties["KeycardPermissions"]);
        var chambers = JsonSerializer.Deserialize<Dictionary<int, List<SerializableLockerItem>>>(
            JsonSerializer.Serialize(Properties["Chambers"]));

        var options = new Queue<int>();
        if (Convert.ToBoolean(Properties["ShuffleChambers"]))
        {
            var clone = chambers.Keys.ToList();
            while (clone.Count > 0)
            {
                options.Enqueue(clone.PullRandomItem());
            }
        }
        else
        {
            var all = chambers.Keys.ToList();
            all.Sort();
            foreach (var number in all)
            {
                options.Enqueue(number);
            }
        }

        locker._serverChambersFilled = true;

        foreach (var chamber in locker.Chambers)
        {
            chamber.RequiredPermissions = keycardPermissions;

            if (options.Count == 0)
            {
                locker.FillChamber(chamber);
                continue;
            }

            if (!chambers.TryGetValue(options.Dequeue(), out var chamberLoot))
            {
                locker.FillChamber(chamber);
                continue;
            }
            
            var total = chamberLoot.Select(x => x.Chance).Sum();
            var item = Random.NextDouble() * total;

            double count = 0;
            foreach (var loot in chamberLoot)
            {
                count += loot.Chance;

                if (count > item)
                {
                    chamber.SpawnItem((ItemType)Enum.Parse(typeof(ItemType), loot.Item), (int)loot.Count);
                    break;
                }
            }
        }

        return locker.gameObject;
    }
}