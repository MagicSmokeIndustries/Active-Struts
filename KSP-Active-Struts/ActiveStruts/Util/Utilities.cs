using System;
using System.Collections.Generic;
using System.Linq;
using ActiveStruts.Modules;
using CIT_Util.Types;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ActiveStruts.Util
{
    public class FreeAttachTargetCheck
    {
        public bool HitResult { get; set; }
        public Part TargetPart { get; set; }
    }

    public static class Utilities
    {
        public static bool AnyTargetersConnected(this ModuleActiveStrut target)
        {
            return GetAllActiveStruts().Any(m => !m.IsTargetOnly && m.Mode == Mode.Linked && m.Target != null && m.Target == target);
        }

        public static List<PartStageBackup> BackupVesselStaging(this Vessel vessel)
        {
            var bkupList = new List<PartStageBackup>(vessel.Parts.Count);
            bkupList.AddRange(vessel.Parts.Select(part => new PartStageBackup(part, part.inStageIndex)));
            return bkupList;
        }

        public static FreeAttachTargetCheck CheckFreeAttachPoint(this ModuleActiveStrut origin)
        {
            var raycast = PerformRaycast(origin.Origin.position, origin.FreeAttachTarget.PartOrigin.position, origin.RealModelForward);
            if (raycast.HitResult)
            {
                var distOk = raycast.DistanceFromOrigin <= Config.Instance.MaxDistance;
                return new FreeAttachTargetCheck
                       {
                           TargetPart = raycast.HittedPart,
                           HitResult = distOk
                       };
            }
            return new FreeAttachTargetCheck
                   {
                       TargetPart = null,
                       HitResult = false
                   };
        }

        internal static GameObject CreateFakeRopeSling(string name, bool active, Color color)
        {
            var sling = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sling.name = name;
            Object.DestroyImmediate(sling.collider);
            const float height = 0.0125f;
            const float diameter = height*6.5f;
            sling.transform.localScale = new Vector3(diameter, diameter, diameter);
            var mr = sling.GetComponent<MeshRenderer>();
            mr.name = name;
            mr.material = new Material(Shader.Find("Diffuse")) {color = color};
            sling.SetActive(active);
            return sling;
        }

        internal static GameObject CreateFlexStrut(string name, bool active, Color color)
        {
            var strut = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            strut.name = name;
            Object.DestroyImmediate(strut.collider);
            var connDim = Config.Instance.ConnectorDimension*0.5f;
            strut.transform.localScale = new Vector3(connDim, connDim, connDim);
            var mr = strut.GetComponent<MeshRenderer>();
            mr.name = name;
            mr.material = new Material(Shader.Find("Diffuse")) {color = color};
            strut.SetActive(active);
            return strut;
        }

        internal static GameObject CreateLocalAnchor(string name, bool active)
        {
            var localAnchor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (localAnchor.rigidbody == null)
            {
                localAnchor.AddComponent<Rigidbody>();
            }
            localAnchor.name = name;
            Object.DestroyImmediate(localAnchor.collider);
            const float localAnchorDim = 0.000001f;
            localAnchor.transform.localScale = new Vector3(localAnchorDim, localAnchorDim, localAnchorDim);
            var mr = localAnchor.GetComponent<MeshRenderer>();
            mr.name = name;
            mr.material = new Material(Shader.Find("Diffuse")) {color = Color.magenta};
            localAnchor.rigidbody.mass = 0.00001f;
            localAnchor.SetActive(active);
            return localAnchor;
        }

        internal static GameObject CreateSimpleStrut(string name)
        {
            var strut = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            strut.name = name;
            Object.DestroyImmediate(strut.collider);
            const float connDim = 1f;
            strut.transform.localScale = new Vector3(connDim, connDim, connDim);
            var mr = strut.GetComponent<MeshRenderer>();
            mr.name = name;
            mr.material = new Material(Shader.Find("Diffuse")) {color = new Color(0.15686f, 0.16078f, 0.2f, 1f)};
            strut.SetActive(false);
            return strut;
        }

        public static bool DistanceInToleranceRange(float savedDistance, float currentDistance)
        {
            return currentDistance >= savedDistance - Config.Instance.FreeAttachDistanceTolerance && currentDistance <= savedDistance + Config.Instance.FreeAttachDistanceTolerance &&
                   currentDistance <= Config.Instance.MaxDistance;
        }

        public static ModuleActiveStrutFreeAttachTarget FindFreeAttachTarget(Guid guid)
        {
            return GetAllFreeAttachTargets().Find(m => m.ID == guid);
        }

        public static List<ModuleActiveStrut> GetAllActiveStruts()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                var allParts = FlightGlobals.Vessels.SelectMany(v => v.parts).ToList();
                return allParts.Where(p => p.Modules.Contains(Config.Instance.ModuleName)).Select(p => p.Modules[Config.Instance.ModuleName] as ModuleActiveStrut).ToList();
            }
            if (!HighLogic.LoadedSceneIsEditor)
            {
                return new List<ModuleActiveStrut>();
            }
            var partList = CIT_Util.Utilities.ListEditorParts(true);
            return partList.Where(p => p.Modules.Contains(Config.Instance.ModuleName)).Select(p => p.Modules[Config.Instance.ModuleName] as ModuleActiveStrut).ToList();
        }

        internal static List<ModuleActiveStrut> GetAllActiveStrutsInLoadRange()
        {
            return CIT_Util.Utilities.GetAllModulesInLoadRange(Config.Instance.ModuleName, p => p as ModuleActiveStrut);
        }

        public static List<ModuleActiveStrut> GetAllConnectedTargeters(this ModuleActiveStrut target)
        {
            return GetAllActiveStruts().Where(m => !m.IsTargetOnly && m.Mode == Mode.Linked && m.Target != null && m.Target == target).ToList();
        }

        public static List<ModuleActiveStrutFreeAttachTarget> GetAllFreeAttachTargets()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                var allParts = FlightGlobals.Vessels.SelectMany(v => v.parts).ToList();
                return
                    allParts.Where(p => p.Modules.Contains(Config.Instance.ModuleActiveStrutFreeAttachTarget))
                            .Select(p => p.Modules[Config.Instance.ModuleActiveStrutFreeAttachTarget] as ModuleActiveStrutFreeAttachTarget)
                            .ToList();
            }
            if (!HighLogic.LoadedSceneIsEditor)
            {
                return new List<ModuleActiveStrutFreeAttachTarget>();
            }
            var partList = CIT_Util.Utilities.ListEditorParts(true);
            return partList.Where(p => p.Modules.Contains(Config.Instance.ModuleActiveStrutFreeAttachTarget)).Select(p => p.Modules[Config.Instance.ModuleActiveStrutFreeAttachTarget] as ModuleActiveStrutFreeAttachTarget).ToList();
        }

        internal static List<ModuleActiveStrutFreeAttachTarget> GetAllFreeAttachTargetsInLoadRange()
        {
            return CIT_Util.Utilities.GetAllModulesInLoadRange(Config.Instance.ModuleActiveStrutFreeAttachTarget, p => p as ModuleActiveStrutFreeAttachTarget);
        }

        internal static List<ModuleKerbalHook> GetAllKerbalHookModulesInLoadRange()
        {
            return CIT_Util.Utilities.GetAllModulesInLoadRange(Config.Instance.ModuleKerbalHook, p => p as ModuleKerbalHook);
        }

        public static List<ModuleActiveStrut> GetAllPossibleTargets(this ModuleActiveStrut origin)
        {
            Debug.Log("[AS] there are " + GetAllActiveStruts().Count + " active struts");
            foreach (var moduleActiveStrut in GetAllActiveStruts())
            {
                Debug.Log("[AS] module with ID " + moduleActiveStrut.ID + " is a possible target: " + origin.IsPossibleTarget(moduleActiveStrut));
            }
            return GetAllActiveStruts().Where(m => m.ID != origin.ID && origin.IsPossibleTarget(m)).Select(m => m).ToList();
        }

        internal static List<ModuleActiveStrutFreeAttachTarget> GetAllUntargetedFreeAttachTargetsInLoadRange()
        {
            var allTargets = GetAllFreeAttachTargetsInLoadRange();
            if (!Config.Instance.EnableFreeAttachKerbalTether)
            {
                allTargets = (from t in allTargets
                              let am = t.part.FindModuleImplementing<ModuleKerbalHookAnchor>()
                              where am == null
                              select t).ToList();
            }
            var allKerbalHooks = GetAllKerbalHookModulesInLoadRange();
            var allActiveStruts = GetAllActiveStrutsInLoadRange().Where(aS => aS.IsFreeAttached).ToList();
            var delList = new List<ModuleActiveStrutFreeAttachTarget>(allTargets.Count);
            delList.AddRange(allTargets);
            foreach (var moduleActiveStrutFreeAttachTarget in allTargets)
            {
                if (allKerbalHooks.Any(kh => kh.Target != null && kh.Target.ID == moduleActiveStrutFreeAttachTarget.ID))
                {
                    delList.Remove(moduleActiveStrutFreeAttachTarget);
                }
                if (allActiveStruts.Any(aSt => aSt.FreeAttachTarget != null && aSt.FreeAttachTarget.ID == moduleActiveStrutFreeAttachTarget.ID))
                {
                    delList.Remove(moduleActiveStrutFreeAttachTarget);
                }
            }
            return delList;
        }

        public static float GetJointStrength(this LinkType type)
        {
            switch (type)
            {
                case LinkType.None:
                {
                    return 0;
                }
                case LinkType.Normal:
                {
                    return Config.Instance.NormalJointStrength;
                }
                case LinkType.Maximum:
                {
                    return Config.Instance.MaximalJointStrength;
                }
                case LinkType.Weak:
                {
                    return Config.Instance.WeakJointStrength;
                }
            }
            return 0;
        }

        public static Tuple<Vector3, RaycastHit?> GetMouseWorldPosition()
        {
            return CIT_Util.Utilities.GetMouseWorldPosition(Config.Instance.MaxDistance);
        }

        public static Vector3 GetNewWorldPosForFreeAttachTarget(Part freeAttachPart, Vector3 freeAttachTargetLocalVector)
        {
            if (freeAttachPart == null)
            {
                return Vector3.zero;
            }
            var newPoint = freeAttachPart.transform.position + freeAttachTargetLocalVector;
            return newPoint;
        }

        public static ModuleActiveStrut GetStrutById(Guid id)
        {
            return GetAllActiveStruts().Find(m => m.ID == id);
        }

        public static bool IsPossibleFreeAttachTarget(this ModuleActiveStrut origin, Vector3 mousePosition)
        {
            var raycast = PerformRaycast(origin.Origin.position, mousePosition, origin.RealModelForward);
            return raycast.HitResult && raycast.DistanceFromOrigin <= Config.Instance.MaxDistance && raycast.RayAngle <= Config.Instance.MaxAngle;
        }

        public static bool IsPossibleTarget(this ModuleActiveStrut origin, ModuleActiveStrut possibleTarget)
        {
            if (possibleTarget.IsConnectionFree || (possibleTarget.Targeter != null && possibleTarget.Targeter.ID == origin.ID) || (possibleTarget.Target != null && possibleTarget.Target.ID == origin.ID))
            {
                var raycast = PerformRaycast(origin.Origin.position, possibleTarget.Origin.position, origin.IsFlexible ? origin.Origin.up : origin.RealModelForward, origin.part);
                return raycast.HitResult && raycast.HittedPart == possibleTarget.part && raycast.DistanceFromOrigin <= Config.Instance.MaxDistance && raycast.RayAngle <= Config.Instance.MaxAngle;
            }
            return false;
        }

        public static RaycastResult PerformRaycast(Vector3 origin, Vector3 target, Vector3 originUp, Part partToIgnore = null)
        {
            var arr = partToIgnore == null ? new Part[0] : new[] {partToIgnore};
            return PerformRaycast(origin, target, originUp, arr);
        }

        public static RaycastResult PerformRaycast(Vector3 origin, Vector3 target, Vector3 originUp, ICollection<Part> partsToIgnore)
        {
            return CIT_Util.Utilities.PerformRaycast(origin, target, originUp, Config.Instance.MaxDistance + 1f, partsToIgnore);
        }

        public static RaycastResult PerformRaycastFromKerbal(Vector3 origin, Vector3 target, Vector3 originUp, Vessel vesselToIgnore)
        {
            return PerformRaycast(origin, target, originUp, vesselToIgnore.Parts);
        }

        public static RaycastResult PerformRaycastIntoDir(Vector3 origin, Vector3 direction, Vector3 originUp, Part partToIgnore)
        {
            return CIT_Util.Utilities.PerformRaycastIntoDirection(origin, direction, originUp, Config.Instance.MaxDistance + 1f, new[] {partToIgnore});
        }

        internal static void RemoveAllUntargetedFreeAttachTargetsInLoadRange()
        {
            var list = GetAllUntargetedFreeAttachTargetsInLoadRange();
            Debug.Log("[AS] removing " + list.Count + " unused target parts");
            foreach (var moduleActiveStrutFreeAttachTarget in list)
            {
                moduleActiveStrutFreeAttachTarget.Die();
            }
        }

        public static void ResetAllFromTargeting()
        {
            foreach (var moduleActiveStrut in GetAllActiveStruts().Where(m => m.Mode == Mode.Target))
            {
                moduleActiveStrut.Mode = Mode.Unlinked;
                moduleActiveStrut.part.SetHighlightDefault();
                moduleActiveStrut.UpdateGui();
                moduleActiveStrut.Targeter = moduleActiveStrut.OldTargeter;
            }
        }

        public static void RestoreVesselStaging(this Vessel vessel, List<PartStageBackup> partStageBackups, bool addExtraStage = false)
        {
            var currVessel = FlightGlobals.ActiveVessel;
            FlightGlobals.SetActiveVessel(vessel);
            foreach (var partStageBackup in partStageBackups)
            {
                partStageBackup.Part.inStageIndex = partStageBackup.Stage;
            }
            if (addExtraStage)
            {
                var currMax = Staging.lastStage;
                Staging.AddStageAt(++currMax);
            }
            vessel.currentStage = Staging.lastStage;
            FlightGlobals.SetActiveVessel(currVessel);
        }

        public static void UnlinkAllConnectedTargeters(this ModuleActiveStrut target)
        {
            var allTargeters = target.GetAllConnectedTargeters();
            foreach (var moduleActiveStrut in allTargeters)
            {
                moduleActiveStrut.Unlink();
            }
        }

        internal static Color _setColorForEmissive(Color color)
        {
            return new Color(color.r, color.g, color.b, 1f);
        }
    }

    public struct PartStageBackup
    {
        public Part Part { get; private set; }
        public int Stage { get; private set; }

        public PartStageBackup(Part part, int currentStage) : this()
        {
            this.Part = part;
            this.Stage = currentStage;
        }
    }
}