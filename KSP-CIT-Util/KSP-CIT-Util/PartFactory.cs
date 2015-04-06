using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CIT_Util
{
    public static class PartFactory
    {
        private const string LogPrefix = "[CIT][PartFactory]";

        public static Part SpawnPartInEditor(string partName)
        {
            if (!HighLogic.LoadedSceneIsEditor)
            {
                Debug.Log(LogPrefix + "[ERR] can only spawn in editor");
                return null;
            }
            var editor = EditorLogic.fetch;
            editor.SpawnPart(PartLoader.getPartInfoByName(partName));
            return null;
        }

        /// <remarks>
        ///     This code is based on KAS by KospY and the following license applies:
        ///     http://kerbal.curseforge.com/ksp-mods/223900-kerbal-attachment-system-kas/license (link valid 03.09.2014)
        ///     Usage of this code by me (marce) has been generously granted by KospY on 02.09.2014 per PM.
        /// </remarks>
        public static Part SpawnPartInFlight(string partName, Part referencePart, Vector3 referencePartOriginSpawnOffset, Quaternion spawnRotation, bool spawnLanded = true)
        {
            if (!HighLogic.LoadedSceneIsFlight)
            {
                Debug.Log(LogPrefix + " can only spawn in flight");
                return null;
            }
            var currentVessel = FlightGlobals.ActiveVessel;
            var avPart = PartLoader.getPartInfoByName(partName);
            var obj = Object.Instantiate(avPart.partPrefab);
            if (obj == null)
            {
                Debug.Log(LogPrefix + " failed to instantiate part " + partName);
                return null;
            }
            try
            {
                var newPart = (Part) obj;
                newPart.gameObject.SetActive(true);
                newPart.gameObject.name = avPart.name;
                newPart.partInfo = avPart;
                //newPart.highlightRecurse = true;
                newPart.SetMirror(Vector3.one);
                var newShip = new ShipConstruct {newPart};
                newShip.SaveShip();
                newShip.shipName = avPart.title;
                var type = Convert.ChangeType(VesselType.Debris, VesselType.Debris.GetTypeCode());
                //if (type != null)
                //{
                //    newShip.shipType = (int) type;
                //}
                //else
                //{
                //    newShip.shipType = 1;
                //}
                var v = newShip.parts[0].localRoot.gameObject.AddComponent<Vessel>();
                v.id = Guid.NewGuid();
                v.vesselName = newShip.shipName;
                v.Initialize();
                v.Landed = spawnLanded;
                v.rootPart.flightID = ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState);
                v.rootPart.missionID = referencePart.missionID;
                v.rootPart.flagURL = referencePart.flagURL;
                FlightGlobals.SetActiveVessel(currentVessel);
                v.SetPosition(referencePart.transform.position + referencePartOriginSpawnOffset);
                v.SetRotation(spawnRotation);
                for (var i = 0; i < newPart.Modules.Count; i++)
                {
                    var node = new ConfigNode();
                    node.AddValue("name", newPart.Modules[i].moduleName);
                    var j = i;
                    newPart.LoadModule(node, ref j);
                }
                return newPart;
            }
            catch (NullReferenceException)
            {
                Debug.Log(LogPrefix + " part unknown");
                return null;
            }
        }
    }
}