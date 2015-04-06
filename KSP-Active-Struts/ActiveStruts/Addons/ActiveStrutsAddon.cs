using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ActiveStruts.Modules;
using ActiveStruts.Util;
using CIT_Util;
using CIT_Util.Types;
using UnityEngine;
using Utilities = ActiveStruts.Util.Utilities;

namespace ActiveStruts.Addons
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class ActiveStrutsAddon : MonoBehaviour
    {
        private const float MpToRayHitDistanceTolerance = 0.02f;
        private const int UnusedTargetPartRemovalCounterInterval = 18000;
        private static GameObject _connector;
        private static object _idResetQueueLock;
        private static object _targetDeleteListLock;
        private static int _idResetCounter;
        private static int _unusedTargetPartRemovalCounter;
        private static bool _idResetTrimFlag;
        private static bool _noInputAxesReset;
        private static bool _partPlacementInProgress;
        private HashSet<MouseOverHighlightData> _mouseOverPartsData;
        private object _mouseOverSetLock;
        private bool _resetAllHighlighting;
        private List<StraightOutHintActivePart> _straightOutHintActiveParts;
        private List<HighlightedPart> _targetHighlightedParts;
        public static ModuleKerbalHook CurrentKerbalTargeter { get; set; }
        public static ModuleActiveStrut CurrentTargeter { get; set; }
        public static bool FlexibleAttachActive { get; set; }
        public static AddonMode Mode { get; set; }
        public static Part NewSpawnedPart { get; set; }
        public static Vector3 Origin { get; set; }
        private static Queue<IDResetable> _idResetQueue { get; set; }

        //must not be static
        private void ActionMenuClosed(Part data)
        {
            if (!_checkForModule(data))
            {
                return;
            }
            var module = data.Modules[Config.Instance.ModuleName] as ModuleActiveStrut;
            if (module == null)
            {
                return;
            }
            if (module.IsConnectionOrigin && module.Target != null)
            {
                module.Target.part.SetHighlightDefault();
                var part = this._targetHighlightedParts.Where(p => p.ModuleID == module.ID).Select(p => p).FirstOrDefault();
                if (part != null)
                {
                    try
                    {
                        this._targetHighlightedParts.Remove(part);
                    }
                    catch (NullReferenceException)
                    {
                        //multithreading issue orccured here, not sure if fixed
                    }
                }
            }
            else if (module.Target != null && (!module.IsConnectionOrigin && module.Targeter != null))
            {
                module.Targeter.part.SetHighlightDefault();
            }
            if (!Config.Instance.ShowStraightOutHint || module.IsTargetOnly || module.IsLinked)
            {
                return;
            }
            var hintObj = this._straightOutHintActiveParts.Where(sohap => sohap.ModuleID == module.ID).Select(sohap => sohap).FirstOrDefault();
            if (hintObj == null)
            {
                return;
            }
            this._straightOutHintActiveParts.Remove(hintObj);
            Destroy(hintObj.HintObject);
        }

        //must not be static
        private void ActionMenuCreated(Part data)
        {
            if (!_checkForModule(data))
            {
                return;
            }
            var module = data.Modules[Config.Instance.ModuleName] as ModuleActiveStrut;
            if (module == null)
            {
                return;
            }
            if (module.IsConnectionOrigin && module.Target != null)
            {
                module.Target.part.SetHighlightColor(Color.cyan);
                module.Target.part.SetHighlight(true, false);
                this._targetHighlightedParts.Add(new HighlightedPart(module.Target.part, module.ID));
            }
            else if (module.Targeter != null && !module.IsConnectionOrigin)
            {
                module.Targeter.part.SetHighlightColor(Color.cyan);
                module.Targeter.part.SetHighlight(true, false);
                this._targetHighlightedParts.Add(new HighlightedPart(module.Targeter.part, module.ID));
            }
            if (Config.Instance.ShowStraightOutHint && !module.IsFlexible && !module.IsTargetOnly)
            {
                this._straightOutHintActiveParts.Add(new StraightOutHintActivePart(data, module.ID, CreateStraightOutHintForPart(module), module));
            }
        }

        private IEnumerator AddModuleToEva(GameEvents.FromToAction<Part, Part> data)
        {
            while (!FlightGlobals.ActiveVessel.isEVA)
            {
                yield return new WaitForFixedUpdate();
            }
            if (FlightGlobals.ActiveVessel.isEVA)
            {
                var rP = FlightGlobals.ActiveVessel.rootPart;
                if (rP != null)
                {
                    try
                    {
                        rP.AddModule("ModuleKerbalHook");
                        this.StartCoroutine(this.CatchModuleAddedToEVA(rP));
                    }
                    catch (NullReferenceException)
                    {
                        Debug.Log("[AS] exception thrown while adding module to EVA");
                    }
                }
            }
        }

        public void Awake()
        {
            if (!(HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
            {
                return;
            }
            _unusedTargetPartRemovalCounter = HighLogic.LoadedSceneIsEditor ? 30 : 180;
            FlexibleAttachActive = false;
            this._targetHighlightedParts = new List<HighlightedPart>();
            this._straightOutHintActiveParts = new List<StraightOutHintActivePart>();
            this._mouseOverSetLock = new object();
            lock (this._mouseOverSetLock)
            {
                this._mouseOverPartsData = new HashSet<MouseOverHighlightData>();
            }
            _connector = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _connector.name = "ASConn";
            DestroyImmediate(_connector.collider);
            var connDim = Config.Instance.ConnectorDimension;
            _connector.transform.localScale = new Vector3(connDim, connDim, connDim);
            var mr = _connector.GetComponent<MeshRenderer>();
            mr.name = "ASConn";
            mr.material = new Material(Shader.Find("Transparent/Diffuse")) {color = Color.green.MakeColorTransparent(Config.Instance.ColorTransparency)};
            _connector.SetActive(false);
            GameEvents.onPartActionUICreate.Add(this.ActionMenuCreated);
            GameEvents.onPartActionUIDismiss.Add(this.ActionMenuClosed);
            GameEvents.onCrewBoardVessel.Add(this.HandleEvaEnd);
            Mode = AddonMode.None;
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onPartRemove.Add(this.HandleEditorPartDetach);
                GameEvents.onPartAttach.Add(this.HandleEditorPartAttach);
                _targetDeleteListLock = new object();
            }
            else if (HighLogic.LoadedSceneIsFlight)
            {
                GameEvents.onPartAttach.Add(this.HandleFlightPartAttach);
                GameEvents.onPartRemove.Add(this.HandleFlightPartAttach);
                _idResetQueueLock = new object();
                _idResetQueue = new Queue<IDResetable>(10);
                _idResetCounter = Config.IdResetCheckInterval;
                _idResetTrimFlag = false;
            }
        }

        private IEnumerator CatchModuleAddedToEVA(Part rp)
        {
            var run = true;
            while (run)
            {
                ModuleKerbalHook module = null;
                if (rp.TryGetModule(out module))
                {
                    Debug.Log("[AS] module found in part!");
                    run = false;
                }
                else
                {
                    Debug.Log("[AS] module not found - waiting");
                    yield return new WaitForFixedUpdate();
                }
            }
        }

        private static GameObject CreateStraightOutHintForPart(ModuleActiveStrut module)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.SetActive(false);
            go.name = Guid.NewGuid().ToString();
            DestroyImmediate(go.collider);
            var connDim = Config.Instance.ConnectorDimension;
            go.transform.localScale = new Vector3(connDim, connDim, connDim);
            var mr = go.GetComponent<MeshRenderer>();
            mr.name = go.name;
            mr.material = new Material(Shader.Find("Transparent/Diffuse")) {color = Color.blue.MakeColorTransparent(Config.Instance.ColorTransparency)};
            UpdateStraightOutHint(module, go);
            return go;
        }

        public static IDResetable Dequeue()
        {
            lock (_idResetQueueLock)
            {
                return _idResetQueue.Dequeue();
            }
        }

        public static void Enqueue(IDResetable module)
        {
            lock (_idResetQueueLock)
            {
                _idResetQueue.Enqueue(module);
            }
        }

        //public void FixedUpdate()
        //{
        //    if (!(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor))
        //    {
        //        return;
        //    }

        //}

        public void Update()
        {
            this._processUpdate();
            //if run in FixedUpdate transforms are offset in orbit around bodies without atmosphere
            this._processFixedUpdate();
        }

        private void HandleEditorPartAttach(GameEvents.HostTargetAction<Part, Part> data)
        {
            var partList = new List<Part> {data.host};
            foreach (var child in data.host.children)
            {
                child.RecursePartList(partList);
            }
            if (!data.host.name.Contains("ASTargetCube") || Mode != AddonMode.FreeAttach)
            {
                return;
            }
            CurrentTargeter.PlaceFreeAttach(data.host);
            NewSpawnedPart = null;
        }

        private void HandleEditorPartDetach(GameEvents.HostTargetAction<Part, Part> hostTargetAction)
        {
            var partList = new List<Part> {hostTargetAction.target};
            foreach (var child in hostTargetAction.target.children)
            {
                child.RecursePartList(partList);
            }
            var movedModules = (from p in partList
                                where p.Modules.Contains(Config.Instance.ModuleName)
                                select p.Modules[Config.Instance.ModuleName] as ModuleActiveStrut).ToList();
            var movedTargets = (from p in partList
                                where p.Modules.Contains(Config.Instance.ModuleActiveStrutFreeAttachTarget)
                                select p.Modules[Config.Instance.ModuleActiveStrutFreeAttachTarget] as ModuleActiveStrutFreeAttachTarget).ToList();
            var vesselModules = (from p in CIT_Util.Utilities.ListEditorParts(false)
                                 where p.Modules.Contains(Config.Instance.ModuleName)
                                 select p.Modules[Config.Instance.ModuleName] as ModuleActiveStrut).ToList();
            foreach (var module in movedModules)
            {
                module.Unlink();
            }
            foreach (var module in vesselModules.Where(module =>
                                                       (module.Target != null && movedModules.Any(m => m.ID == module.Target.ID) ||
                                                        (module.Targeter != null && movedModules.Any(m => m.ID == module.Targeter.ID))) ||
                                                       (module.IsFreeAttached && !module.StraightOutAttachAppliedInEditor && movedTargets.Any(t => t.ID == module.FreeAttachTarget.ID))))
            {
                module.Unlink();
            }
        }

        private void HandleEvaEnd(GameEvents.FromToAction<Part, Part> data)
        {
            var module = (new[] {data.from, data.to})
                .Where(p => p.Modules.Contains(Config.Instance.ModuleKerbalHook))
                .Select(p => p.Modules[Config.Instance.ModuleKerbalHook] as ModuleKerbalHook)
                .FirstOrDefault();
            //Debug.Log("[AS] on eva end module has been found!");
            if (module != null)
            {
                var modulePart = module.part;
                module.Die();
                if (modulePart != null)
                {
                    modulePart.RemoveModule(module);
                }
            }
        }

        //private void HandleEvaStart(GameEvents.FromToAction<Part, Part> data)
        //{
        //    this.StartCoroutine(this.AddModuleToEva(data));
        //}

        public void HandleFlightPartAttach(GameEvents.HostTargetAction<Part, Part> hostTargetAction)
        {
            try
            {
                if (!FlightGlobals.ActiveVessel.isEVA)
                {
                    return;
                }
                foreach (var module in hostTargetAction.target.GetComponentsInChildren<ModuleActiveStrut>())
                {
                    if (module.IsTargetOnly)
                    {
                        module.UnlinkAllConnectedTargeters();
                    }
                    else
                    {
                        module.Unlink();
                    }
                }
            }
            catch (NullReferenceException)
            {
                //thrown on launch, don't know why
            }
        }

        public void HandleFlightPartUndock(Part data)
        {
            Debug.Log("[AS] part undocked");
        }

        private IEnumerator HighlightMouseOverPart(Part mouseOverPart)
        {
            var lPart = this._getMohdForPart(mouseOverPart);
            while (mouseOverPart != null && lPart != null && !lPart.Reset)
            {
                lPart.Part.SetHighlightColor(Color.blue);
                lPart.Part.SetHighlight(true, false);
                lPart.Reset = true;
                yield return new WaitForEndOfFrame();
                //yield return new WaitForSeconds(0.1f);
                lPart = this._getMohdForPart(mouseOverPart);
            }
            this._removeMohdFromList(lPart);
            if (mouseOverPart != null)
            {
                mouseOverPart.SetHighlightDefault();
            }
        }

        private static bool IsQueueEmpty()
        {
            lock (_idResetQueueLock)
            {
                return _idResetQueue.Count == 0;
            }
        }

        private static bool IsValidPosition(RaycastResult raycast, Vector3 mp)
        {
            var valid = raycast.HitResult
                        && raycast.HittedPart != null
                        && (raycast.HittedPart.vessel != null || HighLogic.LoadedSceneIsEditor)
                        && raycast.DistanceFromOrigin <= Config.Instance.MaxDistance
                        && (Mode == AddonMode.AttachKerbalHook || raycast.RayAngle <= Config.Instance.MaxAngle);
            switch (Mode)
            {
                case AddonMode.Link:
                {
                    if (raycast.HittedPart != null && raycast.HittedPart.Modules.Contains(Config.Instance.ModuleName))
                    {
                        var moduleActiveStrut = raycast.HittedPart.Modules[Config.Instance.ModuleName] as ModuleActiveStrut;
                        if (moduleActiveStrut != null)
                        {
                            valid &= raycast.HittedPart != null && raycast.HittedPart.Modules.Contains(Config.Instance.ModuleName) && moduleActiveStrut.IsConnectionFree;
                            if (FlexibleAttachActive)
                            {
                                valid &= moduleActiveStrut.IsFlexible;
                            }
                        }
                    }
                }
                    break;
                case AddonMode.FreeAttach:
                {
                    var tPos = CurrentTargeter.ModelFeatures[ModuleActiveStrut.ModelFeaturesType.HeadExtension] ? CurrentTargeter.StrutOrigin.position : CurrentTargeter.Origin.position;
                    var mPosDist = Vector3.Distance(tPos, mp);
                    valid &= Mathf.Abs(mPosDist - raycast.DistanceFromOrigin) < MpToRayHitDistanceTolerance;
                }
                    break;
                case AddonMode.AttachKerbalHook:
                {
                    valid &= raycast.HittedPart != null
                             && raycast.HittedPart.vessel != null
                             && raycast.HittedPart.vessel != FlightGlobals.ActiveVessel
                             && Mathf.Abs(Vector3.Distance(CurrentKerbalTargeter.part.transform.position, mp) - raycast.DistanceFromOrigin) < MpToRayHitDistanceTolerance;
                }
                    break;
            }
            return valid;
        }

        public void OnDestroy()
        {
            GameEvents.onPartActionUICreate.Remove(this.ActionMenuCreated);
            GameEvents.onPartActionUIDismiss.Remove(this.ActionMenuClosed);
            GameEvents.onPartRemove.Remove(this.HandleEditorPartDetach);
            GameEvents.onPartUndock.Remove(this.HandleFlightPartUndock);
            GameEvents.onPartAttach.Remove(this.HandleFlightPartAttach);
            GameEvents.onPartAttach.Remove(this.HandleEditorPartAttach);
            GameEvents.onCrewBoardVessel.Remove(this.HandleEvaEnd);
        }

        public static IEnumerator PlaceNewPart(Part hittedPart, RaycastHit hit)
        {
            var rayres = new RaycastResult {HittedPart = hittedPart, Hit = hit};
            return PlaceNewPart(rayres);
        }

        private static IEnumerator PlaceNewPart(RaycastResult raycast)
        {
            var activeVessel = FlightGlobals.ActiveVessel;
            NewSpawnedPart.transform.position = raycast.Hit.point;
            NewSpawnedPart.rigidbody.velocity = raycast.HittedPart.rigidbody.velocity;
            NewSpawnedPart.rigidbody.angularVelocity = raycast.HittedPart.rigidbody.angularVelocity;
            yield return new WaitForSeconds(0.1f);
            NewSpawnedPart.transform.rotation = raycast.HittedPart.transform.rotation;
            NewSpawnedPart.transform.position = raycast.Hit.point;

            NewSpawnedPart.transform.LookAt(Mode == AddonMode.AttachKerbalHook ? CurrentKerbalTargeter.transform.position : CurrentTargeter.transform.position);
            NewSpawnedPart.transform.rotation = Quaternion.FromToRotation(NewSpawnedPart.transform.up, raycast.Hit.normal)*NewSpawnedPart.transform.rotation;

            yield return new WaitForFixedUpdate();
            var targetModuleName = Config.Instance.ModuleActiveStrutFreeAttachTarget;
            if (!NewSpawnedPart.Modules.Contains(targetModuleName))
            {
                Debug.Log("[AS][ERR] spawned part contains no target module. Panic!!");
                NewSpawnedPart.decouple();
                Destroy(NewSpawnedPart);
            }
            var module = NewSpawnedPart.Modules[Config.Instance.ModuleActiveStrutFreeAttachTarget];
            if (raycast.HittedPart.vessel != activeVessel)
            {
                if (module != null)
                {
                    var targetModule = module as ModuleActiveStrutFreeAttachTarget;
                    if (targetModule != null)
                    {
                        var dockingVesselName = activeVessel.GetName();
                        var dockingVesselType = activeVessel.vesselType;
                        var dockingVesselId = activeVessel.rootPart.flightID;
                        var vesselInfo = new DockedVesselInfo {name = dockingVesselName, vesselType = dockingVesselType, rootPartUId = dockingVesselId};
                        targetModule.part.Couple(activeVessel.rootPart);
                        targetModule.part.Undock(vesselInfo);
                    }
                }
            }
            NewSpawnedPart.transform.position = raycast.Hit.point;
            if (module != null)
            {
                var targetModule = module as ModuleActiveStrutFreeAttachTarget;
                if (targetModule != null)
                {
                    targetModule.CreateJointToParent(raycast.HittedPart);
                }
            }
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.1f);
            yield return new WaitForFixedUpdate();
            if (Mode == AddonMode.AttachKerbalHook)
            {
                CurrentKerbalTargeter.PlaceHook(NewSpawnedPart);
            }
            else
            {
                CurrentTargeter.PlaceFreeAttach(NewSpawnedPart);
            }
            _partPlacementInProgress = false;
            NewSpawnedPart = null;
        }

        private static void TrimQueue()
        {
            lock (_idResetQueueLock)
            {
                _idResetQueue.TrimExcess();
            }
        }

        private static void UpdateStraightOutHint(ModuleActiveStrut module, GameObject hint)
        {
            hint.SetActive(false);
            var rayres = Utilities.PerformRaycastIntoDir(module.Origin.position, module.RealModelForward, module.RealModelForward, module.part);
            var trans = hint.transform;
            trans.position = module.Origin.position;
            var dist = rayres.HitResult ? rayres.DistanceFromOrigin/2f : Config.Instance.MaxDistance;
            if (rayres.HitResult)
            {
                trans.LookAt(rayres.Hit.point);
            }
            else
            {
                trans.LookAt(module.Origin.transform.position + (module.IsFlexible ? module.Origin.up : module.RealModelForward));
            }
            trans.Rotate(new Vector3(0, 1, 0), 90f);
            trans.Rotate(new Vector3(0, 0, 1), 90f);
            trans.localScale = new Vector3(0.05f, dist, 0.05f);
            trans.Translate(new Vector3(0f, dist, 0f));
            hint.SetActive(true);
        }

        private void _checkForInEditorAbort()
        {
            if (!HighLogic.LoadedSceneIsEditor)
            {
                return;
            }
            if (EditorLogic.SelectedPart != null && EditorLogic.SelectedPart.Modules.Contains(Config.Instance.ModuleActiveStrutFreeAttachTarget))
            {
                var module = EditorLogic.SelectedPart.Modules[Config.Instance.ModuleActiveStrutFreeAttachTarget] as ModuleActiveStrutFreeAttachTarget;
                if (module != null)
                {
                    module.Die();
                }
            }
        }

        private static bool _checkForModule(Part part)
        {
            return part.Modules.Contains(Config.Instance.ModuleName);
        }

        private static bool _determineColor(Vector3 mp, RaycastResult raycast)
        {
            var validPosition = IsValidPosition(raycast, mp);
            var mr = _connector.GetComponent<MeshRenderer>();
            mr.material.color =
                (validPosition
                     ? (Mode == AddonMode.Link && !raycast.HittedPart.Modules.Contains(Config.Instance.ModuleName)) ||
                       ((Mode == AddonMode.FreeAttach || Mode == AddonMode.AttachKerbalHook) &&
                        (raycast.HittedPart.Modules.Contains(Config.Instance.ModuleName)))
                           ? Color.yellow
                           : Color.green
                     : Color.red).MakeColorTransparent(Config.Instance.ColorTransparency);
            if (Mode == AddonMode.FreeAttach || Mode == AddonMode.AttachKerbalHook)
            {
                validPosition = validPosition && !raycast.HittedPart.Modules.Contains(Config.Instance.ModuleName);
            }
            return validPosition;
        }

        private MouseOverHighlightData _getMohdForPart(Part mopart)
        {
            if (mopart == null)
            {
                return null;
            }
            lock (this._mouseOverSetLock)
            {
                return this._mouseOverPartsData.FirstOrDefault(mohd => mohd.Part == mopart);
            }
        }

        private static void _highlightCurrentTargets()
        {
            var targets = Utilities.GetAllActiveStruts().Where(m => m.Mode == Util.Mode.Target).Select(m => m.part).ToList();
            foreach (var part in targets)
            {
                part.SetHighlightColor(Color.green);
                part.SetHighlight(true, false);
            }
        }

        private void _pointToMousePosition(Vector3 mp, RaycastResult rayRes)
        {
            var startPos = Mode == AddonMode.AttachKerbalHook
                               ? CurrentKerbalTargeter.transform.position
                               : (CurrentTargeter.ModelFeatures[ModuleActiveStrut.ModelFeaturesType.HeadExtension]
                                      ? CurrentTargeter.StrutOrigin.position
                                      : CurrentTargeter.Origin.position);
            _connector.SetActive(true);
            var trans = _connector.transform;
            trans.position = startPos;
            trans.LookAt(mp);
            trans.localScale = new Vector3(trans.position.x, trans.position.y, 1);
            var dist = Vector3.Distance(Vector3.zero, trans.InverseTransformPoint(mp))/2.0f;
            trans.localScale = new Vector3(0.05f, dist, 0.05f);
            trans.Rotate(new Vector3(0, 0, 1), 90f);
            trans.Rotate(new Vector3(1, 0, 0), 90f);
            trans.Translate(new Vector3(0f, dist, 0f));
            //if (rayRes.HitResult)
            //{
            //    var mouseOverPart = rayRes.HittedPart;
            //    if (mouseOverPart != null)
            //    {
            //        if (!this._setMouseOverPart(mouseOverPart))
            //        {
            //            this.StartCoroutine(this.HighlightMouseOverPart(mouseOverPart));
            //        }
            //    }
            //}
        }

        private void _processFixedUpdate()
        {
            if (_unusedTargetPartRemovalCounter > 0)
            {
                _unusedTargetPartRemovalCounter--;
            }
            else
            {
                _unusedTargetPartRemovalCounter = UnusedTargetPartRemovalCounterInterval;
                try
                {
                    Utilities.RemoveAllUntargetedFreeAttachTargetsInLoadRange();
                }
                catch (Exception e)
                {
                    Debug.Log("[AS] unused target part cleanup threw exception: " + e.Message);
                }
            }
            if (!HighLogic.LoadedSceneIsFlight)
            {
                return;
            }
            var activeVessel = FlightGlobals.ActiveVessel;
            if (activeVessel == null || !activeVessel.isEVA)
            {
                foreach (var module in FlightGlobals.Vessels
                                                    .Where(v => v.loaded && v.isEVA)
                                                    .Select(v => v.rootPart.FindModuleImplementing<ModuleKerbalHook>())
                                                    .Where(module => module != null))
                {
                    module.OnFixedUpdate();
                }
                return;
            }
            var rP = FlightGlobals.ActiveVessel.rootPart;
            ModuleKerbalHook rPMod = null;
            if (rP != null && !rP.TryGetModule(out rPMod))
            {
                Debug.Log("[AS] trying to add module to EVA root part");
                try
                {
                    rP.AddModule("ModuleKerbalHook");
                    Debug.Log("[AS] module added to EVA root part");
                    this.StartCoroutine(this.CatchModuleAddedToEVA(rP));
                }
                catch (NullReferenceException e)
                {
                    Debug.Log("[AS] exception thrown: " + e.Message);
                }
            }
            else if (rP != null)
            {
                if (rPMod != null)
                {
                    rPMod.OnFixedUpdate();
                }
            }
        }

        private void _processFreeAttachPlacement(RaycastResult raycast)
        {
            if (NewSpawnedPart == null)
            {
                Mode = AddonMode.None;
                if (Mode == AddonMode.FreeAttach)
                {
                    CurrentTargeter.AbortLink();
                }
                else if (Mode == AddonMode.AttachKerbalHook)
                {
                    CurrentKerbalTargeter.Abort();
                }
                Debug.Log("[AS][ERR] no target part ready - aborting FreeAttach");
                return;
            }
            if (_partPlacementInProgress)
            {
                return;
            }
            _partPlacementInProgress = true;
            this.StartCoroutine(PlaceNewPart(raycast));
        }

        private static void _processIdResets()
        {
            if (_idResetCounter > 0)
            {
                _idResetCounter--;
                return;
            }
            _idResetCounter = Config.IdResetCheckInterval;
            var updateFlag = false;
            while (!IsQueueEmpty())
            {
                var module = Dequeue();
                if (module != null)
                {
                    module.ResetId();
                }
                updateFlag = true;
            }
            if (updateFlag)
            {
                Debug.Log("[AS] IDs have been updated.");
            }
            if (_idResetTrimFlag)
            {
                TrimQueue();
            }
            else
            {
                _idResetTrimFlag = true;
            }
        }

        private void _processUpdate()
        {
            try
            {
                if (HighLogic.LoadedSceneIsFlight)
                {
                    if (NewSpawnedPart != null)
                    {
                        var justDestroyPart = false;
                        if (CurrentTargeter != null || CurrentKerbalTargeter != null)
                        {
                            // ReSharper disable once PossibleNullReferenceException - is checked
                            NewSpawnedPart.transform.position = CurrentKerbalTargeter != null ? CurrentKerbalTargeter.transform.position : CurrentTargeter.transform.position;
                        }
                        else
                        {
                            var module = NewSpawnedPart.Modules[Config.Instance.ModuleActiveStrutFreeAttachTarget];
                            if (module != null)
                            {
                                var target = module as ModuleActiveStrutFreeAttachTarget;
                                if (target != null)
                                {
                                    target.Die();
                                }
                                else
                                {
                                    justDestroyPart = true;
                                }
                            }
                            else
                            {
                                justDestroyPart = true;
                            }
                        }
                        if (justDestroyPart)
                        {
                            Destroy(NewSpawnedPart);
                            NewSpawnedPart = null;
                        }
                    }
                    _processIdResets();
                }
                if (Config.Instance.ShowStraightOutHint && this._straightOutHintActiveParts != null)
                {
                    var remList = new List<StraightOutHintActivePart>();
                    var renewList = new List<StraightOutHintActivePart>();
                    foreach (var straightOutHintActivePart in this._straightOutHintActiveParts)
                    {
                        if (straightOutHintActivePart.HasToBeRemoved)
                        {
                            remList.Add(straightOutHintActivePart);
                        }
                        else
                        {
                            renewList.Add(straightOutHintActivePart);
                        }
                    }
                    foreach (var straightOutHintActivePart in remList)
                    {
                        this._straightOutHintActiveParts.Remove(straightOutHintActivePart);
                        Destroy(straightOutHintActivePart.HintObject);
                    }
                    foreach (var straightOutHintActivePart in renewList)
                    {
                        UpdateStraightOutHint(straightOutHintActivePart.Module, straightOutHintActivePart.HintObject);
                    }
                }
                var resetList = new List<HighlightedPart>();
                if (this._targetHighlightedParts != null)
                {
                    resetList = this._targetHighlightedParts.Where(targetHighlightedPart => targetHighlightedPart != null && targetHighlightedPart.HasToBeRemoved).ToList();
                }
                foreach (var targetHighlightedPart in resetList)
                {
                    targetHighlightedPart.Part.SetHighlightDefault();
                    try
                    {
                        if (this._targetHighlightedParts != null)
                        {
                            this._targetHighlightedParts.Remove(targetHighlightedPart);
                        }
                    }
                    catch (NullReferenceException)
                    {
                        //multithreading issue occured here, don't know if fixed
                    }
                }
                if (this._targetHighlightedParts != null)
                {
                    foreach (var targetHighlightedPart in this._targetHighlightedParts)
                    {
                        var part = targetHighlightedPart.Part;
                        part.SetHighlightColor(Color.cyan);
                        part.SetHighlight(true, false);
                    }
                }
                if (Mode == AddonMode.None || (CurrentTargeter == null && CurrentKerbalTargeter == null))
                {
                    if (this._resetAllHighlighting)
                    {
                        this._resetAllHighlighting = false;
                        var asList = Utilities.GetAllActiveStruts();
                        foreach (var moduleActiveStrut in asList)
                        {
                            moduleActiveStrut.part.SetHighlightDefault();
                        }
                    }
                    if (_connector != null)
                    {
                        _connector.SetActive(false);
                    }
                    return;
                }
                this._resetAllHighlighting = true;
                if (Mode == AddonMode.Link)
                {
                    _highlightCurrentTargets();
                }
                var mp = Utilities.GetMouseWorldPosition();
                var transformForRaycast = Mode == AddonMode.AttachKerbalHook
                                              ? CurrentKerbalTargeter.transform
                                              : CurrentTargeter.ModelFeatures[ModuleActiveStrut.ModelFeaturesType.HeadExtension]
                                                    ? CurrentTargeter.StrutOrigin
                                                    : CurrentTargeter.Origin;
                var rightDir = Mode == AddonMode.AttachKerbalHook ? Vector3.zero : CurrentTargeter.RealModelForward;
                var raycast = Mode == AddonMode.AttachKerbalHook
                                  ? Utilities.PerformRaycastFromKerbal(transformForRaycast.position, mp.Item1, transformForRaycast.up, CurrentKerbalTargeter.part.vessel)
                                  : Utilities.PerformRaycast(transformForRaycast.position, mp.Item1, CurrentTargeter.IsFlexible ? transformForRaycast.up : rightDir, CurrentTargeter.IsFlexible ? CurrentTargeter.part : null);
                this._pointToMousePosition(mp.Item1, raycast);
                if (!raycast.HitResult)
                {
                    var handled = false;
                    if (Mode == AddonMode.Link && Input.GetKeyDown(KeyCode.Mouse0))
                    {
                        CurrentTargeter.AbortLink();
                        CurrentTargeter.UpdateGui();
                        handled = true;
                    }
                    if ((Mode == AddonMode.FreeAttach || Mode == AddonMode.AttachKerbalHook) && (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape)))
                    {
                        if (HighLogic.LoadedSceneIsFlight && Input.GetKeyDown(KeyCode.Escape))
                        {
                            Input.ResetInputAxes();
                        }
                        Mode = AddonMode.None;
                        if (Mode == AddonMode.AttachKerbalHook)
                        {
                            CurrentKerbalTargeter.Abort();
                        }
                        else
                        {
                            CurrentTargeter.UpdateGui();
                            CurrentTargeter.AbortLink();
                            CurrentTargeter = null;
                        }
                        this._checkForInEditorAbort();
                        handled = true;
                    }
                    _connector.SetActive(false);
                    if (HighLogic.LoadedSceneIsEditor && handled)
                    {
                        Input.ResetInputAxes();
                        InputLockManager.RemoveControlLock(Config.Instance.EditorInputLockId);
                    }
                    return;
                }
                var validPos = _determineColor(mp.Item1, raycast);
                if (validPos && Mode == AddonMode.FreeAttach && HighLogic.LoadedSceneIsEditor)
                {
                    InputLockManager.RemoveControlLock(Config.Instance.EditorInputLockId);
                    _noInputAxesReset = true;
                }
                else if (HighLogic.LoadedSceneIsEditor)
                {
                    InputLockManager.SetControlLock(Config.Instance.EditorInputLockId);
                    _noInputAxesReset = false;
                }
                this._processUserInput(mp.Item1, raycast, validPos);
            }
            catch (NullReferenceException e)
            {
                Debug.Log("[AS] addon update exception catched: " + e);
                /*
                 * For no apparent reason an exception is thrown on first load.
                 * I found no way to circumvent this.
                 * Since the exception has to be handled only once we are 
                 * "just" entering the try block constantly which I consider 
                 * still to be preferred over an unhandled exception.
                 */
            }
        }

        private void _processUserInput(Vector3 mp, RaycastResult raycast, bool validPos)
        {
            var handled = false;
            switch (Mode)
            {
                case AddonMode.Link:
                {
                    if (Input.GetKeyDown(KeyCode.Mouse0))
                    {
                        if (validPos && raycast.HittedPart.Modules.Contains(Config.Instance.ModuleName))
                        {
                            var moduleActiveStrut = raycast.HittedPart.Modules[Config.Instance.ModuleName] as ModuleActiveStrut;
                            if (moduleActiveStrut != null)
                            {
                                moduleActiveStrut.SetAsTarget();
                                handled = true;
                            }
                        }
                    }
                    else if ((Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape)))
                    {
                        if (HighLogic.LoadedSceneIsFlight && Input.GetKeyDown(KeyCode.Escape))
                        {
                            Input.ResetInputAxes();
                        }
                        CurrentTargeter.AbortLink();
                        handled = true;
                    }
                }
                    break;
                case AddonMode.FreeAttach:
                case AddonMode.AttachKerbalHook:
                {
                    if (Input.GetKeyDown(KeyCode.Mouse0))
                    {
                        if (validPos)
                        {
                            if (HighLogic.LoadedSceneIsFlight)
                            {
                                this._processFreeAttachPlacement(raycast);
                            }
                            handled = true;
                        }
                    }
                    else if ((Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape)))
                    {
                        if (HighLogic.LoadedSceneIsFlight && Input.GetKeyDown(KeyCode.Escape))
                        {
                            Input.ResetInputAxes();
                        }
                        if (Mode == AddonMode.AttachKerbalHook)
                        {
                            CurrentKerbalTargeter = null;
                        }
                        else
                        {
                            CurrentTargeter.AbortLink();
                            CurrentTargeter = null;
                        }
                        this._checkForInEditorAbort();
                        Mode = AddonMode.None;
                        handled = true;
                    }
                }
                    break;
            }
            if (HighLogic.LoadedSceneIsEditor && handled)
            {
                if (!_noInputAxesReset)
                {
                    Input.ResetInputAxes();
                }
                InputLockManager.RemoveControlLock(Config.Instance.EditorInputLockId);
            }
        }

        private void _removeMohdFromList(MouseOverHighlightData mohd)
        {
            if (mohd == null)
            {
                return;
            }
            lock (this._mouseOverSetLock)
            {
                this._mouseOverPartsData.Remove(mohd);
            }
        }

        private bool _setMouseOverPart(Part mopart)
        {
            lock (this._mouseOverSetLock)
            {
                var lp = this._mouseOverPartsData.FirstOrDefault(mohd => mohd.Part == mopart);
                if (lp != null)
                {
                    lp.Reset = false;
                    return true;
                }
                this._mouseOverPartsData.Add(new MouseOverHighlightData(mopart));
                return false;
            }
        }

        private class MouseOverHighlightData
        {
            internal Part Part { get; private set; }
            internal bool Reset { get; set; }

            internal MouseOverHighlightData(Part part)
            {
                this.Part = part;
                this.Reset = false;
            }
        }
    }

    public class HighlightedPart
    {
        public int Duration { get; set; }

        public bool HasToBeRemoved
        {
            get
            {
                var now = DateTime.Now;
                var dur = (now - this.HighlightStartTime).TotalSeconds;
                return dur >= this.Duration;
            }
        }

        public DateTime HighlightStartTime { get; set; }
        public Guid ModuleID { get; set; }
        public Part Part { get; set; }

        public HighlightedPart(Part part, Guid moduleId)
        {
            this.Part = part;
            this.HighlightStartTime = DateTime.Now;
            this.ModuleID = moduleId;
            this.Duration = Config.Instance.TargetHighlightDuration;
        }
    }

    public class StraightOutHintActivePart : HighlightedPart
    {
        public GameObject HintObject { get; set; }
        public ModuleActiveStrut Module { get; set; }

        public StraightOutHintActivePart(Part part, Guid moduleId, GameObject hintObject, ModuleActiveStrut module) : base(part, moduleId)
        {
            this.HintObject = hintObject;
            this.Module = module;
            this.Duration = Config.Instance.StraightOutHintDuration;
        }
    }

    public enum AddonMode
    {
        FreeAttach,
        Link,
        None,
        AttachKerbalHook
    }

    internal struct LayerBackup
    {
        internal int Layer { get; private set; }
        internal Part Part { get; private set; }

        internal LayerBackup(int layer, Part part) : this()
        {
            this.Layer = layer;
            this.Part = part;
        }
    }
}