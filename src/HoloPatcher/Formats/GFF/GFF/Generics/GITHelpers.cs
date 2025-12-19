using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Logger;
using KOTORModSync.Resource;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py
    // Original: construct_git and dismantle_git functions
    public static class GITHelpers
    {
        // Helper method to create a default triangle geometry at a position
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:1258-1259, 1333-1334
        // Original: encounter.geometry.create_triangle(origin=encounter.position)
        private static void CreateDefaultTriangle(List<Vector3> geometry, Vector3 origin)
        {
            // Create a simple triangle: origin, origin + (3, 0, 0), origin + (3, 3, 0)
            geometry.Add(new Vector3(origin.X, origin.Y, origin.Z));
            geometry.Add(new Vector3(origin.X + 3.0f, origin.Y, origin.Z));
            geometry.Add(new Vector3(origin.X + 3.0f, origin.Y + 3.0f, origin.Z));
        }
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:1184-1365
        // Original: def construct_git(gff: GFF) -> GIT:
        public static GIT ConstructGit(GFF gff)
        {
            var git = new GIT();

            var root = gff.Root;
            var propertiesStruct = root.Acquire<GFFStruct>("AreaProperties", new GFFStruct());
            git.AmbientVolume = propertiesStruct.Acquire<int>("AmbientSndDayVol", 0);
            git.AmbientSoundId = propertiesStruct.Acquire<int>("AmbientSndDay", 0);
            git.EnvAudio = propertiesStruct.Acquire<int>("EnvAudio", 0);
            git.MusicStandardId = propertiesStruct.Acquire<int>("MusicDay", 0);
            git.MusicBattleId = propertiesStruct.Acquire<int>("MusicBattle", 0);
            git.MusicDelay = propertiesStruct.Acquire<int>("MusicDelay", 0);

            // Extract camera list
            var cameraList = root.Acquire<GFFList>("CameraList", new GFFList());
            foreach (var cameraStruct in cameraList)
            {
                var camera = new GITCamera();
                camera.CameraId = cameraStruct.Acquire<int>("CameraID", 0);
                camera.Fov = cameraStruct.Acquire<float>("FieldOfView", 0.0f);
                camera.Height = cameraStruct.Acquire<float>("Height", 0.0f);
                camera.MicRange = cameraStruct.Acquire<float>("MicRange", 0.0f);
                camera.Orientation = cameraStruct.Acquire<Vector4>("Orientation", new Vector4());
                camera.Position = cameraStruct.Acquire<Vector3>("Position", new Vector3());
                camera.Pitch = cameraStruct.Acquire<float>("Pitch", 0.0f);
                git.Cameras.Add(camera);
            }

            // Extract creature list
            var creatureList = root.Acquire<GFFList>("Creature List", new GFFList());
            foreach (var creatureStruct in creatureList)
            {
                var creature = new GITCreature();
                creature.ResRef = creatureStruct.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
                float x = creatureStruct.Acquire<float>("XPosition", 0.0f);
                float y = creatureStruct.Acquire<float>("YPosition", 0.0f);
                float z = creatureStruct.Acquire<float>("ZPosition", 0.0f);
                creature.Position = new Vector3(x, y, z);
                float rotX = creatureStruct.Acquire<float>("XOrientation", 0.0f);
                float rotY = creatureStruct.Acquire<float>("YOrientation", 0.0f);
                // Calculate bearing from orientation
                var vec2 = new Vector2(rotX, rotY);
                creature.Bearing = (float)Math.Atan2(vec2.Y, vec2.X) - (float)(Math.PI / 2);
                git.Creatures.Add(creature);
            }

            // Extract door list
            var doorList = root.Acquire<GFFList>("Door List", new GFFList());
            foreach (var doorStruct in doorList)
            {
                var door = new GITDoor();
                door.Bearing = doorStruct.Acquire<float>("Bearing", 0.0f);
                door.Tag = doorStruct.Acquire<string>("Tag", "");
                door.ResRef = doorStruct.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
                door.LinkedTo = doorStruct.Acquire<string>("LinkedTo", "");
                door.LinkedToFlags = (GITModuleLink)doorStruct.Acquire<int>("LinkedToFlags", 0);
                door.LinkedToModule = doorStruct.Acquire<ResRef>("LinkedToModule", ResRef.FromBlank());
                door.TransitionDestination = doorStruct.Acquire<LocalizedString>("TransitionDestin", LocalizedString.FromInvalid());
                float x = doorStruct.Acquire<float>("X", 0.0f);
                float y = doorStruct.Acquire<float>("Y", 0.0f);
                float z = doorStruct.Acquire<float>("Z", 0.0f);
                door.Position = new Vector3(x, y, z);
                int tweakEnabled = doorStruct.Acquire<int>("UseTweakColor", 0);
                if (tweakEnabled != 0)
                {
                    int tweakColorInt = doorStruct.Acquire<int>("TweakColor", 0);
                    door.TweakColor = Color.FromBgrInteger(tweakColorInt);
                }
                git.Doors.Add(door);
            }

            // Extract encounter list
            var encounterList = root.Acquire<GFFList>("Encounter List", new GFFList());
            foreach (var encounterStruct in encounterList)
            {
                float x = encounterStruct.Acquire<float>("XPosition", 0.0f);
                float y = encounterStruct.Acquire<float>("YPosition", 0.0f);
                float z = encounterStruct.Acquire<float>("ZPosition", 0.0f);
                var encounter = new GITEncounter();
                encounter.Position = new Vector3(x, y, z);
                encounter.ResRef = encounterStruct.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
                
                if (encounterStruct.Exists("Geometry"))
                {
                    var geometryList = encounterStruct.Acquire<GFFList>("Geometry", new GFFList());
                    if (geometryList != null && geometryList.Count > 0)
                    {
                        foreach (var geometryStruct in geometryList)
                        {
                            float gx = geometryStruct.Acquire<float>("X", 0.0f);
                            float gy = geometryStruct.Acquire<float>("Y", 0.0f);
                            float gz = geometryStruct.Acquire<float>("Z", 0.0f);
                            encounter.Geometry.Add(new Vector3(gx, gy, gz));
                        }
                    }
                    else
                    {
                        new Logger.RobustLogger().Warning("Encounter geometry list is empty! Creating a default triangle at its position.");
                        CreateDefaultTriangle(encounter.Geometry, encounter.Position);
                    }
                }
                else
                {
                    new Logger.RobustLogger().Warning("Encounter geometry list missing! Creating a default triangle at its position.");
                    CreateDefaultTriangle(encounter.Geometry, encounter.Position);
                }

                // Extract spawn points
                var spawnList = encounterStruct.Acquire<GFFList>("SpawnPointList", new GFFList());
                foreach (var spawnStruct in spawnList)
                {
                    var spawn = new GITEncounterSpawnPoint();
                    spawn.X = spawnStruct.Acquire<float>("X", 0.0f);
                    spawn.Y = spawnStruct.Acquire<float>("Y", 0.0f);
                    spawn.Z = spawnStruct.Acquire<float>("Z", 0.0f);
                    spawn.Orientation = spawnStruct.Acquire<float>("Orientation", 0.0f);
                    encounter.SpawnPoints.Add(spawn);
                }
                
                git.Encounters.Add(encounter);
            }

            // Extract placeable list
            var placeableList = root.Acquire<GFFList>("Placeable List", new GFFList());
            foreach (var placeableStruct in placeableList)
            {
                var placeable = new GITPlaceable();
                placeable.ResRef = placeableStruct.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
                float x = placeableStruct.Acquire<float>("X", 0.0f);
                float y = placeableStruct.Acquire<float>("Y", 0.0f);
                float z = placeableStruct.Acquire<float>("Z", 0.0f);
                placeable.Position = new Vector3(x, y, z);
                placeable.Bearing = placeableStruct.Acquire<float>("Bearing", 0.0f);
                int tweakEnabled = placeableStruct.Acquire<int>("UseTweakColor", 0);
                if (tweakEnabled != 0)
                {
                    int tweakColorInt = placeableStruct.Acquire<int>("TweakColor", 0);
                    placeable.TweakColor = Color.FromBgrInteger(tweakColorInt);
                }
                git.Placeables.Add(placeable);
            }

            // Extract sound list
            var soundList = root.Acquire<GFFList>("SoundList", new GFFList());
            foreach (var soundStruct in soundList)
            {
                var sound = new GITSound();
                sound.ResRef = soundStruct.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
                float x = soundStruct.Acquire<float>("XPosition", 0.0f);
                float y = soundStruct.Acquire<float>("YPosition", 0.0f);
                float z = soundStruct.Acquire<float>("ZPosition", 0.0f);
                sound.Position = new Vector3(x, y, z);
                git.Sounds.Add(sound);
            }

            // Extract store list
            var storeList = root.Acquire<GFFList>("StoreList", new GFFList());
            foreach (var storeStruct in storeList)
            {
                var store = new GITStore();
                store.ResRef = storeStruct.Acquire<ResRef>("ResRef", ResRef.FromBlank());
                float x = storeStruct.Acquire<float>("XPosition", 0.0f);
                float y = storeStruct.Acquire<float>("YPosition", 0.0f);
                float z = storeStruct.Acquire<float>("ZPosition", 0.0f);
                store.Position = new Vector3(x, y, z);
                float rotX = storeStruct.Acquire<float>("XOrientation", 0.0f);
                float rotY = storeStruct.Acquire<float>("YOrientation", 0.0f);
                // Bearing is calculated as arctangent of YOrientation over XOrientation, minus 90 degrees
                store.Bearing = (float)Math.Atan2(rotY, rotX) - (float)(Math.PI / 2);
                git.Stores.Add(store);
            }

            // Extract trigger list
            var triggerList = root.Acquire<GFFList>("TriggerList", new GFFList());
            foreach (var triggerStruct in triggerList)
            {
                var trigger = new GITTrigger();
                trigger.ResRef = triggerStruct.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
                float x = triggerStruct.Acquire<float>("XPosition", 0.0f);
                float y = triggerStruct.Acquire<float>("YPosition", 0.0f);
                float z = triggerStruct.Acquire<float>("ZPosition", 0.0f);
                trigger.Position = new Vector3(x, y, z);
                trigger.Tag = triggerStruct.Acquire<string>("Tag", "");
                trigger.LinkedTo = triggerStruct.Acquire<string>("LinkedTo", "");
                trigger.LinkedToFlags = (GITModuleLink)triggerStruct.Acquire<int>("LinkedToFlags", 0);
                trigger.LinkedToModule = triggerStruct.Acquire<ResRef>("LinkedToModule", ResRef.FromBlank());
                trigger.TransitionDestination = triggerStruct.Acquire<LocalizedString>("TransitionDestin", LocalizedString.FromInvalid());
                // Extract geometry if present
                if (triggerStruct.Exists("Geometry"))
                {
                    var geometryList = triggerStruct.Acquire<GFFList>("Geometry", new GFFList());
                    if (geometryList != null && geometryList.Count > 0)
                    {
                        foreach (var geometryStruct in geometryList)
                        {
                            float px = geometryStruct.Acquire<float>("PointX", 0.0f);
                            float py = geometryStruct.Acquire<float>("PointY", 0.0f);
                            float pz = geometryStruct.Acquire<float>("PointZ", 0.0f);
                            trigger.Geometry.Add(new Vector3(px, py, pz));
                        }
                    }
                    else
                    {
                        new Logger.RobustLogger().Warning("Trigger geometry list is empty! Creating a default triangle at its position.");
                        CreateDefaultTriangle(trigger.Geometry, trigger.Position);
                    }
                }
                else
                {
                    new Logger.RobustLogger().Warning("Trigger geometry list missing! Creating a default triangle at its position.");
                    CreateDefaultTriangle(trigger.Geometry, trigger.Position);
                }
                git.Triggers.Add(trigger);
            }

            // Extract waypoint list
            var waypointList = root.Acquire<GFFList>("WaypointList", new GFFList());
            foreach (var waypointStruct in waypointList)
            {
                var waypoint = new GITWaypoint();
                waypoint.Name = waypointStruct.Acquire<LocalizedString>("LocalizedName", LocalizedString.FromInvalid());
                waypoint.Tag = waypointStruct.Acquire<string>("Tag", "");
                waypoint.ResRef = waypointStruct.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
                float x = waypointStruct.Acquire<float>("XPosition", 0.0f);
                float y = waypointStruct.Acquire<float>("YPosition", 0.0f);
                float z = waypointStruct.Acquire<float>("ZPosition", 0.0f);
                waypoint.Position = new Vector3(x, y, z);
                waypoint.HasMapNote = waypointStruct.Acquire<int>("HasMapNote", 0) != 0;
                if (waypoint.HasMapNote)
                {
                    waypoint.MapNote = waypointStruct.Acquire<LocalizedString>("MapNote", LocalizedString.FromInvalid());
                    waypoint.MapNoteEnabled = waypointStruct.Acquire<int>("MapNoteEnabled", 0) != 0;
                }
                else
                {
                    waypoint.MapNote = null; // Explicitly set to null when HasMapNote is false, matching Python behavior
                }
                float rotX = waypointStruct.Acquire<float>("XOrientation", 0.0f);
                float rotY = waypointStruct.Acquire<float>("YOrientation", 0.0f);
                if (Math.Abs(rotX) < 1e-6f && Math.Abs(rotY) < 1e-6f)
                {
                    waypoint.Bearing = 0.0f;
                }
                else
                {
                    // Math.Atan2 calculates the angle in radians between the X axis and the point (rotX, rotY)
                    waypoint.Bearing = (float)Math.Atan2(rotY, rotX) - (float)(Math.PI / 2);
                }
                git.Waypoints.Add(waypoint);
            }

            return git;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:1368-1594
        // Original: def dismantle_git(git: GIT, game: Game = Game.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleGit(GIT git, Game game = Game.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.GIT);
            var root = gff.Root;

            root.SetUInt8("UseTemplates", 1);

            var propertiesStruct = new GFFStruct(100);
            root.SetStruct("AreaProperties", propertiesStruct);
            propertiesStruct.SetInt32("AmbientSndDayVol", git.AmbientVolume);
            propertiesStruct.SetInt32("AmbientSndDay", git.AmbientSoundId);
            propertiesStruct.SetInt32("AmbientSndNitVol", git.AmbientVolume);
            propertiesStruct.SetInt32("AmbientSndNight", git.AmbientSoundId);
            propertiesStruct.SetInt32("EnvAudio", git.EnvAudio);
            propertiesStruct.SetInt32("MusicDay", git.MusicStandardId);
            propertiesStruct.SetInt32("MusicNight", git.MusicStandardId);
            propertiesStruct.SetInt32("MusicBattle", git.MusicBattleId);
            propertiesStruct.SetInt32("MusicDelay", git.MusicDelay);

            // Write camera list
            var cameraList = new GFFList();
            root.SetList("CameraList", cameraList);
            foreach (var camera in git.Cameras)
            {
                var cameraStruct = cameraList.Add(GITCamera.GffStructId);
                cameraStruct.SetInt32("CameraID", camera.CameraId);
                cameraStruct.SetSingle("FieldOfView", camera.Fov);
                cameraStruct.SetSingle("Height", camera.Height);
                cameraStruct.SetSingle("MicRange", camera.MicRange);
                cameraStruct.SetVector4("Orientation", camera.Orientation);
                cameraStruct.SetVector3("Position", camera.Position);
                cameraStruct.SetSingle("Pitch", camera.Pitch);
            }

            // Write creature list
            var creatureList = new GFFList();
            root.SetList("Creature List", creatureList);
            foreach (var creature in git.Creatures)
            {
                float angle = creature.Bearing + (float)(Math.PI / 2);
                var bearing = System.Numerics.Vector2.Normalize(new System.Numerics.Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)));
                var creatureStruct = creatureList.Add(GITCreature.GffStructId);
                if (creature.ResRef != null && !string.IsNullOrEmpty(creature.ResRef.ToString()))
                {
                    creatureStruct.SetResRef("TemplateResRef", creature.ResRef);
                }
                creatureStruct.SetSingle("XOrientation", bearing.X);
                creatureStruct.SetSingle("YOrientation", bearing.Y);
                creatureStruct.SetSingle("XPosition", creature.Position.X);
                creatureStruct.SetSingle("YPosition", creature.Position.Y);
                creatureStruct.SetSingle("ZPosition", creature.Position.Z);
            }

            // Write door list
            var doorList = new GFFList();
            root.SetList("Door List", doorList);
            foreach (var door in git.Doors)
            {
                var doorStruct = doorList.Add(GITDoor.GffStructId);
                doorStruct.SetSingle("Bearing", door.Bearing);
                doorStruct.SetString("Tag", door.Tag);
                if (door.ResRef != null && !string.IsNullOrEmpty(door.ResRef.ToString()))
                {
                    doorStruct.SetResRef("TemplateResRef", door.ResRef);
                }
                doorStruct.SetString("LinkedTo", door.LinkedTo);
                doorStruct.SetUInt8("LinkedToFlags", (byte)door.LinkedToFlags);
                doorStruct.SetResRef("LinkedToModule", door.LinkedToModule);
                doorStruct.SetLocString("TransitionDestin", door.TransitionDestination);
                doorStruct.SetSingle("X", door.Position.X);
                doorStruct.SetSingle("Y", door.Position.Y);
                doorStruct.SetSingle("Z", door.Position.Z);
                if (game.IsK2())
                {
                    int tweakColor = door.TweakColor != null ? door.TweakColor.ToBgrInteger() : 0;
                    doorStruct.SetUInt32("TweakColor", (uint)tweakColor);
                    doorStruct.SetUInt8("UseTweakColor", door.TweakColor != null ? (byte)1 : (byte)0);
                }
            }

            // Write encounter list
            var encounterList = new GFFList();
            root.SetList("Encounter List", encounterList);
            foreach (var encounter in git.Encounters)
            {
                var encounterStruct = encounterList.Add(GITEncounter.GffStructId);
                if (encounter.ResRef != null && !string.IsNullOrEmpty(encounter.ResRef.ToString()))
                {
                    encounterStruct.SetResRef("TemplateResRef", encounter.ResRef);
                }
                encounterStruct.SetSingle("XPosition", encounter.Position.X);
                encounterStruct.SetSingle("YPosition", encounter.Position.Y);
                encounterStruct.SetSingle("ZPosition", encounter.Position.Z);

                if (encounter.Geometry == null || encounter.Geometry.Count == 0)
                {
                    new Logger.RobustLogger().Warning($"Missing encounter geometry for '{encounter.ResRef}', creating a default triangle at its position...");
                    var tempGeometry = new List<Vector3>();
                    CreateDefaultTriangle(tempGeometry, encounter.Position);
                    encounter.Geometry = tempGeometry;
                }

                var geometryList = new GFFList();
                encounterStruct.SetList("Geometry", geometryList);
                foreach (var point in encounter.Geometry)
                {
                    var geometryStruct = geometryList.Add(GITEncounter.GffGeometryStructId);
                    geometryStruct.SetSingle("X", point.X);
                    geometryStruct.SetSingle("Y", point.Y);
                    geometryStruct.SetSingle("Z", point.Z);
                }

                var spawnList = new GFFList();
                encounterStruct.SetList("SpawnPointList", spawnList);
                foreach (var spawn in encounter.SpawnPoints)
                {
                    var spawnStruct = spawnList.Add(GITEncounter.GffSpawnStructId);
                    spawnStruct.SetSingle("Orientation", spawn.Orientation);
                    spawnStruct.SetSingle("X", spawn.X);
                    spawnStruct.SetSingle("Y", spawn.Y);
                    spawnStruct.SetSingle("Z", spawn.Z);
                }
            }

            // Write placeable list
            var placeableList = new GFFList();
            root.SetList("Placeable List", placeableList);
            foreach (var placeable in git.Placeables)
            {
                var placeableStruct = placeableList.Add(GITPlaceable.GffStructId);
                placeableStruct.SetSingle("Bearing", placeable.Bearing);
                if (placeable.ResRef != null && !string.IsNullOrEmpty(placeable.ResRef.ToString()))
                {
                    placeableStruct.SetResRef("TemplateResRef", placeable.ResRef);
                }
                placeableStruct.SetSingle("X", placeable.Position.X);
                placeableStruct.SetSingle("Y", placeable.Position.Y);
                placeableStruct.SetSingle("Z", placeable.Position.Z);
                if (game.IsK2())
                {
                    int tweakColor = placeable.TweakColor != null ? placeable.TweakColor.ToBgrInteger() : 0;
                    placeableStruct.SetUInt32("TweakColor", (uint)tweakColor);
                    placeableStruct.SetUInt8("UseTweakColor", placeable.TweakColor != null ? (byte)1 : (byte)0);
                }
            }

            // Write sound list
            var soundList = new GFFList();
            root.SetList("SoundList", soundList);
            foreach (var sound in git.Sounds)
            {
                var soundStruct = soundList.Add(GITSound.GffStructId);
                soundStruct.SetUInt32("GeneratedType", 0);
                if (sound.ResRef != null && !string.IsNullOrEmpty(sound.ResRef.ToString()))
                {
                    soundStruct.SetResRef("TemplateResRef", sound.ResRef);
                }
                soundStruct.SetSingle("XPosition", sound.Position.X);
                soundStruct.SetSingle("YPosition", sound.Position.Y);
                soundStruct.SetSingle("ZPosition", sound.Position.Z);
            }

            // Write store list
            var storeList = new GFFList();
            root.SetList("StoreList", storeList);
            foreach (var store in git.Stores)
            {
                float angle = store.Bearing + (float)(Math.PI / 2);
                var bearing = System.Numerics.Vector2.Normalize(new System.Numerics.Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)));
                var storeStruct = storeList.Add(GITStore.GffStructId);
                if (store.ResRef != null && !string.IsNullOrEmpty(store.ResRef.ToString()))
                {
                    storeStruct.SetResRef("ResRef", store.ResRef);
                }
                storeStruct.SetSingle("XOrientation", bearing.X);
                storeStruct.SetSingle("YOrientation", bearing.Y);
                storeStruct.SetSingle("XPosition", store.Position.X);
                storeStruct.SetSingle("YPosition", store.Position.Y);
                storeStruct.SetSingle("ZPosition", store.Position.Z);
            }

            // Write trigger list
            var triggerList = new GFFList();
            root.SetList("TriggerList", triggerList);
            foreach (var trigger in git.Triggers)
            {
                var triggerStruct = triggerList.Add(GITTrigger.GffStructId);
                if (trigger.ResRef != null && !string.IsNullOrEmpty(trigger.ResRef.ToString()))
                {
                    triggerStruct.SetResRef("TemplateResRef", trigger.ResRef);
                }
                triggerStruct.SetSingle("XPosition", trigger.Position.X);
                triggerStruct.SetSingle("YPosition", trigger.Position.Y);
                triggerStruct.SetSingle("ZPosition", trigger.Position.Z);
                triggerStruct.SetSingle("XOrientation", 0.0f);
                triggerStruct.SetSingle("YOrientation", 0.0f);
                triggerStruct.SetSingle("ZOrientation", 0.0f);
                triggerStruct.SetString("Tag", trigger.Tag);
                triggerStruct.SetString("LinkedTo", trigger.LinkedTo);
                triggerStruct.SetUInt8("LinkedToFlags", (byte)trigger.LinkedToFlags);
                triggerStruct.SetResRef("LinkedToModule", trigger.LinkedToModule);
                triggerStruct.SetLocString("TransitionDestin", trigger.TransitionDestination);

                if (trigger.Geometry == null || trigger.Geometry.Count == 0)
                {
                    new Logger.RobustLogger().Warning($"Missing trigger geometry for '{trigger.ResRef}', creating a default triangle at its position...");
                    var tempGeometry = new List<Vector3>();
                    CreateDefaultTriangle(tempGeometry, trigger.Position);
                    trigger.Geometry = tempGeometry;
                }

                var geometryList = new GFFList();
                triggerStruct.SetList("Geometry", geometryList);
                foreach (var point in trigger.Geometry)
                {
                    var geometryStruct = geometryList.Add(GITTrigger.GffGeometryStructId);
                    geometryStruct.SetSingle("PointX", point.X);
                    geometryStruct.SetSingle("PointY", point.Y);
                    geometryStruct.SetSingle("PointZ", point.Z);
                }
            }

            // Write waypoint list
            var waypointList = new GFFList();
            root.SetList("WaypointList", waypointList);
            foreach (var waypoint in git.Waypoints)
            {
                float angle = waypoint.Bearing + (float)(Math.PI / 2);
                var bearing = System.Numerics.Vector2.Normalize(new System.Numerics.Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)));
                var waypointStruct = waypointList.Add(GITWaypoint.GffStructId);
                waypointStruct.SetLocString("LocalizedName", waypoint.Name);
                waypointStruct.SetString("Tag", waypoint.Tag);
                waypointStruct.SetResRef("TemplateResRef", waypoint.ResRef);
                waypointStruct.SetSingle("XPosition", waypoint.Position.X);
                waypointStruct.SetSingle("YPosition", waypoint.Position.Y);
                waypointStruct.SetSingle("ZPosition", waypoint.Position.Z);
                waypointStruct.SetSingle("XOrientation", bearing.X);
                waypointStruct.SetSingle("YOrientation", bearing.Y);
                waypointStruct.SetUInt8("MapNoteEnabled", waypoint.MapNoteEnabled ? (byte)1 : (byte)0);
                waypointStruct.SetUInt8("HasMapNote", waypoint.HasMapNote ? (byte)1 : (byte)0);
                // Matching PyKotor: LocalizedString.from_invalid() if waypoint.map_note is None else waypoint.map_note
                waypointStruct.SetLocString("MapNote", waypoint.MapNote == null ? LocalizedString.FromInvalid() : waypoint.MapNote);

                if (useDeprecated)
                {
                    waypointStruct.SetUInt8("Appearance", 1);
                    waypointStruct.SetLocString("Description", LocalizedString.FromInvalid());
                    waypointStruct.SetString("LinkedTo", "");
                }
            }

            if (useDeprecated)
            {
                root.SetList("List", new GFFList());
            }

            return gff;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:1585-1594
        // Original: def bytes_git(git: GIT, game: Game = Game.K2, file_format: ResourceType = ResourceType.GFF) -> bytes:
        public static byte[] BytesGit(GIT git, Game game = Game.K2, ResourceType fileFormat = null)
        {
            if (fileFormat == null)
            {
                fileFormat = ResourceType.GIT;
            }
            GFF gff = DismantleGit(git, game);
            return GFFAuto.BytesGff(gff, fileFormat);
        }
    }
}