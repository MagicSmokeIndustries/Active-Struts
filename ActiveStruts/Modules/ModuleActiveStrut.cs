/*
Copyright (c) 2014 marce
Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International Public License
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ActiveStruts.Addons;
using ActiveStruts.Util;
using UnityEngine;
using OSD = ActiveStruts.Util.OSD;
using Utilities = ActiveStruts.Util.Utilities;

namespace ActiveStruts.Modules
{
    public class ModuleActiveStrut : PartModule, IDResetable
    {
        private const ControlTypes EDITOR_LOCK_MASK = ControlTypes.EDITOR_PAD_PICK_PLACE | ControlTypes.EDITOR_ICON_PICK;
        private const float NORMAL_ANI_SPEED = 1.5f;
        private const float FAST_ANI_SPEED = 1000f;
        private readonly object freeAttachStrutUpdateLock = new object();
        [KSPField(isPersistant = true)] private bool AniExtended;
        [KSPField(isPersistant = false)] public string AnimationName;
        [KSPField(isPersistant = true)] public uint DockingVesselId;
        [KSPField(isPersistant = true)] public string DockingVesselName;
        [KSPField(isPersistant = true)] public string DockingVesselTypeString;
        [KSPField(isPersistant = false)] public float FlexibleStrutDamper = 0f;
        [KSPField(isPersistant = false)] public float FlexibleStrutOffset = 0f;
        [KSPField(isPersistant = false)] public float FlexibleStrutSlingOffset = 0f;
        [KSPField(isPersistant = false)] public float FlexibleStrutSpring = 0f;
        [KSPField(isPersistant = true)] public string FreeAttachPositionOffsetVector;
        [KSPField(isPersistant = true)] public bool FreeAttachPositionOffsetVectorSetInEditor = false;
        [KSPField(isPersistant = true)] public string FreeAttachTargetId = Guid.Empty.ToString();
        public Transform Grappler;
        [KSPField(isPersistant = false)] public string GrapplerName;
        [KSPField(isPersistant = false)] public float GrapplerOffset;
        [KSPField(isPersistant = false)] public string HeadName;
        public Transform Hooks;
        [KSPField(isPersistant = false)] public string HooksForward = "RIGHT,false";
        [KSPField(isPersistant = false)] public string HooksName;
        [KSPField(isPersistant = false)] public float HooksScaleFactor = 1f;
        [KSPField(isPersistant = true)] public string Id = Guid.Empty.ToString();
        [KSPField(isPersistant = true)] public bool IdResetDone = false;
        [KSPField(isPersistant = false)] public bool InvertRightAxis = true;
        [KSPField(isPersistant = true)] public bool IsConnectionOrigin = false;
        [KSPField(isPersistant = true)] public bool IsDocked;
        [KSPField(guiActive = true, guiName = "Enforced")] public bool IsEnforced = false;
        [KSPField(isPersistant = true)] public bool IsFlexible = false;
        [KSPField(isPersistant = true)] public bool IsFreeAttached = false;
        [KSPField(isPersistant = true)] public bool IsHalfWayExtended = false;
        [KSPField(isPersistant = true)] public bool IsLinked = false;
        [KSPField(isPersistant = true)] public bool IsOwnVesselConnected = false;
        [KSPField(isPersistant = true)] public bool IsTargetOnly = false;
        public Transform LightsBright;
        [KSPField(isPersistant = false)] public string LightsBrightName;
        public Transform LightsDull;
        [KSPField(isPersistant = false)] public string LightsDullName;
        [KSPField(isPersistant = false)] public float LightsOffset;
        internal Dictionary<ModelFeaturesType, bool> ModelFeatures;
        public ModuleActiveStrut OldTargeter;
        public Transform Origin;
        [KSPField(isPersistant = false)] public string SimpleLightsForward = "FORWARD,false";
        [KSPField(isPersistant = false)] public string SimpleLightsName;
        [KSPField(isPersistant = false)] public string SimpleLightsSecondaryName;
        public FXGroup SoundAttach;
        public FXGroup SoundBreak;
        public FXGroup SoundDetach;
        [KSPField(isPersistant = false, guiActive = true)] public string State = "n.a.";
        [KSPField(isPersistant = true)] public bool StraightOutAttachAppliedInEditor = false;
        [KSPField(guiActive = true)] public string Strength = LinkType.None.ToString();
        public Transform Strut;
        [KSPField(isPersistant = false)] public string StrutName;
        internal Transform StrutOrigin;
        [KSPField(isPersistant = false)] public float StrutScaleFactor;
        [KSPField(isPersistant = true)] public string TargetId = Guid.Empty.ToString();
        [KSPField(isPersistant = true)] public string TargeterId = Guid.Empty.ToString();
        private bool brightLightsExtended;
        private bool delayedStartFlag;
        private bool dullLightsExtended = true;
        private Dictionary<ModelFeaturesType, OrientationInfo> featureOrientation;
        private GameObject flexFakeSlingLocal;
        private GameObject flexFakeSlingTarget;
        private SpringJoint flexJoint;
        private GameObject flexStrut;
        private Part freeAttachPart;
        private ModuleActiveStrutFreeAttachTarget freeAttachTarget;
        private Transform headTransform;
        private bool initialized;
        private ConfigurableJoint joint;
        private AttachNode jointAttachNode;
        private bool jointBroken;
        private LinkType linkType;
        private GameObject localFlexAnchor;
        private FixedJoint localFlexAnchorFixedJoint;
        private Mode mode = Mode.Undefined;
        private Vector3 oldTargetPosition = Vector3.zero;
        private PartJoint partJoint;
        private Transform simpleLights;
        private Transform simpleLightsSecondary;
        private GameObject simpleStrut;
        private bool soundFlag;
        private bool straightOutAttached;
        private bool strutFinallyCreated;
        private int strutRealignCounter;
        private bool targetGrapplerVisible;
        private int ticksForDelayedStart;

        public Animation DeployAnimation
        {
            get
            {
                if (string.IsNullOrEmpty(AnimationName))
                {
                    return null;
                }
                return part.FindModelAnimators(AnimationName)[0];
            }
        }

        internal Vector3 FlexOffsetOriginPosition
        {
            get { return Origin.position + (Origin.up*FlexibleStrutOffset); }
        }

        private Part FreeAttachPart
        {
            get
            {
                if (freeAttachPart != null)
                {
                    return freeAttachPart;
                }
                if (FreeAttachTarget != null)
                {
                    freeAttachPart = FreeAttachTarget.part;
                }
                return freeAttachPart;
            }
        }

        public Vector3 FreeAttachPositionOffset
        {
            get
            {
                if (FreeAttachPositionOffsetVector == null)
                {
                    return Vector3.zero;
                }
                var vArr = FreeAttachPositionOffsetVector.Split(' ').Select(Convert.ToSingle).ToArray();
                return new Vector3(vArr[0], vArr[1], vArr[2]);
            }
            set { FreeAttachPositionOffsetVector = String.Format("{0} {1} {2}", value.x, value.y, value.z); }
        }

        public ModuleActiveStrutFreeAttachTarget FreeAttachTarget
        {
            get
            {
                return freeAttachTarget ??
                       (freeAttachTarget = Utilities.FindFreeAttachTarget(new Guid(FreeAttachTargetId)));
            }
            set
            {
                FreeAttachTargetId = value != null ? value.ID.ToString() : Guid.Empty.ToString();
                freeAttachTarget = value;
            }
        }

        public Guid ID
        {
            get
            {
                if (Id == null || new Guid(Id) == Guid.Empty)
                {
                    Id = Guid.NewGuid().ToString();
                }
                return new Guid(Id);
            }
        }

        private bool IsAnimationPlaying
        {
            get
            {
                var ani = DeployAnimation;
                return ani != null && ani.IsPlaying(AnimationName);
            }
        }

        public bool IsConnectionFree
        {
            get { return IsTargetOnly || !IsLinked || (IsLinked && Mode == Mode.Unlinked); }
        }

        public LinkType LinkType
        {
            get { return linkType; }
            set
            {
                linkType = value;
                Strength = value.ToString();
            }
        }

        public Mode Mode
        {
            get { return mode; }
            set
            {
                mode = value;
                State = value.ToString();
            }
        }

        public Vector3 RealModelForward
        {
            get
            {
                if (InvertRightAxis)
                {
                    return Origin.right*-1;
                }
                return Origin.right;
            }
        }

        public ModuleActiveStrut Target
        {
            get { return TargetId == Guid.Empty.ToString() ? null : Utilities.GetStrutById(new Guid(TargetId)); }
            set { TargetId = value != null ? value.ID.ToString() : Guid.Empty.ToString(); }
        }

        public ModuleActiveStrut Targeter
        {
            get { return TargeterId == Guid.Empty.ToString() ? null : Utilities.GetStrutById(new Guid(TargeterId)); }
            set { TargeterId = value != null ? value.ID.ToString() : Guid.Empty.ToString(); }
        }

        public void ResetId()
        {
            var oldId = Id;
            Id = Guid.NewGuid().ToString();
            foreach (var moduleActiveStrut in Utilities.GetAllActiveStruts())
            {
                if (moduleActiveStrut.TargetId != null && moduleActiveStrut.TargetId == oldId)
                {
                    moduleActiveStrut.TargetId = Id;
                }
                if (moduleActiveStrut.TargeterId != null && moduleActiveStrut.TargeterId == oldId)
                {
                    moduleActiveStrut.TargeterId = Id;
                }
            }
            IdResetDone = true;
        }

        [KSPEvent(name = "AbortLink", active = false, guiName = "Abort Link", guiActiveEditor = true,
            guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE)]
        public void AbortLink()
        {
            Mode = Mode.Unlinked;
            Utilities.ResetAllFromTargeting();
            ActiveStrutsAddon.Mode = AddonMode.None;
            ActiveStrutsAddon.FlexibleAttachActive = false;
            if (HighLogic.LoadedSceneIsEditor)
            {
                InputLockManager.RemoveControlLock(Config.Instance.EditorInputLockId);
            }
            OSD.PostMessage("Link aborted.");
            UpdateGui();
            RetractHead(NORMAL_ANI_SPEED);
        }

        public void CreateJoint(Rigidbody originBody, Rigidbody targetBody, LinkType type, Vector3 anchorPosition)
        {
            if (HighLogic.LoadedSceneIsFlight && part != null && part.attachJoint != null &&
                part.attachJoint.Joint != null)
            {
                part.attachJoint.Joint.breakForce = Mathf.Infinity;
                part.attachJoint.Joint.breakTorque = Mathf.Infinity;
                if (!IsFreeAttached && Target != null && Target.part != null && Target.part.attachJoint != null &&
                    Target.part.attachJoint.Joint != null)
                {
                    Target.part.attachJoint.Joint.breakForce = Mathf.Infinity;
                    Target.part.attachJoint.Joint.breakTorque = Mathf.Infinity;
                }
            }
            LinkType = type;
            if (IsFlexible)
            {
                StartCoroutine(WaitAndCreateFlexibleJoint());
            }
            var breakForce = type.GetJointStrength();
            if (!IsFreeAttached)
            {
                var moduleActiveStrut = Target;
                if (moduleActiveStrut != null)
                {
                    moduleActiveStrut.LinkType = type;
                    IsOwnVesselConnected = moduleActiveStrut.vessel == vessel;
                }
            }
            else
            {
                IsOwnVesselConnected = FreeAttachPart.vessel == vessel;
            }
            if (IsFlexible)
            {
                return;
            }
            if (!IsEnforced || Config.Instance.GlobalJointWeakness)
            {
                joint = originBody.gameObject.AddComponent<ConfigurableJoint>();
                joint.connectedBody = targetBody;
                joint.breakForce = joint.breakTorque = Mathf.Infinity;
                joint.xMotion = ConfigurableJointMotion.Locked;
                joint.yMotion = ConfigurableJointMotion.Locked;
                joint.zMotion = ConfigurableJointMotion.Locked;
                joint.angularXMotion = ConfigurableJointMotion.Locked;
                joint.angularYMotion = ConfigurableJointMotion.Locked;
                joint.angularZMotion = ConfigurableJointMotion.Locked;
                joint.projectionAngle = 0f;
                joint.projectionDistance = 0f;
                joint.targetPosition = anchorPosition;
                joint.anchor = anchorPosition;
            }
            else
            {
                ManageAttachNode(breakForce);
            }
            PlayAttachSound();
        }

        public void CreateStrut(Vector3 target, float distancePercent = 1, float strutOffset = 0f)
        {
            if (IsFlexible)
            {
                if (flexStrut != null)
                {
                    flexStrut.SetActive(false);
                    var trans = flexStrut.transform;
                    trans.position = FlexOffsetOriginPosition;
                    trans.LookAt(Target.FlexOffsetOriginPosition);
                    trans.localScale = new Vector3(trans.position.x, trans.position.y, 1);
                    var dist =
                        (Vector3.Distance(Vector3.zero, trans.InverseTransformPoint(Target.FlexOffsetOriginPosition))/
                         2.0f);
                    trans.localScale = new Vector3(0.025f, dist, 0.025f);
                    trans.Rotate(new Vector3(0, 0, 1), 90f);
                    trans.Rotate(new Vector3(1, 0, 0), 90f);
                    trans.Translate(new Vector3(0f, dist, 0f));
                    flexStrut.SetActive(true);
                }
                if (flexFakeSlingLocal != null)
                {
                    MoveFakeRopeSling(true, flexFakeSlingLocal);
                }
                if (flexFakeSlingTarget != null)
                {
                    MoveFakeRopeSling(false, flexFakeSlingTarget);
                }
            }
            else
            {
                if (ModelFeatures[ModelFeaturesType.Animation])
                {
                    if (IsAnimationPlaying)
                    {
                        return;
                    }
                }
                if (Target != null && Target.ModelFeatures[ModelFeaturesType.Animation])
                {
                    if (Target.IsAnimationPlaying)
                    {
                        return;
                    }
                }
                if (Targeter != null && Targeter.ModelFeatures[ModelFeaturesType.Animation])
                {
                    if (Targeter.IsAnimationPlaying)
                    {
                        return;
                    }
                }
                if (ModelFeatures[ModelFeaturesType.Strut])
                {
                    var strut = Strut;
                    strut.LookAt(target);
                    strut.Rotate(new Vector3(0, 1, 0), 90f);
                    strut.localScale = new Vector3(1, 1, 1);
                    var distance = (Vector3.Distance(Vector3.zero, Strut.InverseTransformPoint(target))*distancePercent*
                                    StrutScaleFactor) + strutOffset; //*-1
                    if (IsFreeAttached)
                    {
                        distance += Config.Instance.FreeAttachStrutExtension;
                    }
                    Strut.localScale = new Vector3(distance, 1, 1);
                    TransformLights(true, target, IsDocked);
                }
                else
                {
                    var localStrutOrigin = ModelFeatures[ModelFeaturesType.HeadExtension] ? StrutOrigin : Origin;
                    simpleStrut.SetActive(false);
                    var dist = Vector3.Distance(localStrutOrigin.position, target)*distancePercent/2f;
                    var trans = simpleStrut.transform;
                    trans.position = localStrutOrigin.position;
                    trans.LookAt(target);
                    trans.localScale = new Vector3(trans.position.x, trans.position.y, 1);
                    trans.localScale = new Vector3(0.025f, dist, 0.025f);
                    trans.Rotate(new Vector3(0, 0, 1), 90f);
                    trans.Rotate(new Vector3(1, 0, 0), 90f);
                    trans.Translate(new Vector3(0f, 1f, 0f)*dist);
                    simpleStrut.SetActive(true);
                }
                if (ModelFeatures[ModelFeaturesType.HeadExtension])
                {
                    headTransform.LookAt(target);
                }
                strutFinallyCreated = true;
            }
        }

        private void DeployHead(float speed)
        {
            AniExtended = true;
            PlayDeployAnimation(speed);
        }

        public void DestroyJoint()
        {
            if (flexJoint != null)
            {
                DestroyImmediate(flexJoint);
            }
            if (localFlexAnchorFixedJoint != null)
            {
                DestroyImmediate(localFlexAnchorFixedJoint);
            }
            if (localFlexAnchor != null)
            {
                DestroyImmediate(localFlexAnchor);
            }
            try
            {
                if (partJoint != null)
                {
                    partJoint.DestroyJoint();
                    part.attachNodes.Remove(jointAttachNode);
                    jointAttachNode.owner = null;
                }
                DestroyImmediate(partJoint);
            }
            catch (Exception)
            {
                //ahem...
            }
            DestroyImmediate(joint);
            partJoint = null;
            jointAttachNode = null;
            joint = null;
            LinkType = LinkType.None;
            if (IsDocked)
            {
                ProcessUnDock(true);
            }
            UpdateSimpleLights();
        }

        public void DestroyStrut()
        {
            if (IsFlexible)
            {
                if (flexStrut != null)
                {
                    flexStrut.SetActive(false);
                }
            }
            else
            {
                Strut.localScale = Vector3.zero;
                ShowGrappler(false, Vector3.zero, Vector3.zero, false, Vector3.zero);
                ShowHooks(false, Vector3.zero, Vector3.zero);
                TransformLights(false, Vector3.zero);
            }
            strutFinallyCreated = false;
        }

        [KSPEvent(name = "Dock", active = false, guiName = "Dock with Target", guiActiveEditor = false,
            guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE)]
        public void Dock()
        {
            if (HighLogic.LoadedSceneIsEditor || !IsLinked || !IsConnectionOrigin || IsTargetOnly ||
                IsOwnVesselConnected || (IsFreeAttached ? FreeAttachPart == null : Target == null) || IsDocked)
            {
                OSD.PostMessage("Can't dock.");
                return;
            }
            if (IsFreeAttached
                ? FreeAttachPart != null && FreeAttachPart.vessel == vessel
                : Target != null && Target.part != null && Target.part.vessel == vessel)
            {
                OSD.PostMessage("Already docked");
                return;
            }
            DockingVesselName = vessel.GetName();
            DockingVesselTypeString = vessel.vesselType.ToString();
            DockingVesselId = vessel.rootPart.flightID;
            IsDocked = true;
            if (IsFreeAttached)
            {
                var attachPart = FreeAttachPart;
                if (attachPart != null)
                {
                    attachPart.Couple(part);
                }
            }
            else
            {
                var moduleActiveStrut = Target;
                if (moduleActiveStrut != null)
                {
                    moduleActiveStrut.part.Couple(part);
                }
            }
            UpdateGui();
            foreach (var moduleActiveStrut in Utilities.GetAllActiveStruts())
            {
                moduleActiveStrut.UpdateGui();
            }
            OSD.PostMessage("Docked.");
        }

        [KSPEvent(name = "FreeAttach", active = false, guiActiveEditor = false, guiName = "FreeAttach Link",
            guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE)]
        public void FreeAttach()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                InputLockManager.SetControlLock(EDITOR_LOCK_MASK, Config.Instance.EditorInputLockId);
                var newPart = PartFactory.SpawnPartInEditor("ASTargetCube");
                Debug.Log("[IRAS] spawned part in editor");
                ActiveStrutsAddon.CurrentTargeter = this;
                ActiveStrutsAddon.Mode = AddonMode.FreeAttach;
                ActiveStrutsAddon.NewSpawnedPart = newPart;
            }
            StraightOutAttachAppliedInEditor = false;
            if (Config.Instance.ShowHelpTexts)
            {
                OSD.PostMessage(Config.Instance.FreeAttachHelpText, 5);
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                StartCoroutine(PreparePartForFreeAttach());
            }
        }

        [KSPEvent(name = "FreeAttachStraight", active = false, guiName = "Straight Up FreeAttach",
            guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE)]
        public void FreeAttachStraight()
        {
            var raycast = _performStraightOutRaycast();
            if (raycast.Item1)
            {
                var hittedPart = raycast.Item2.PartFromHit();
                var valid = hittedPart != null;
                if (valid)
                {
                    if (HighLogic.LoadedSceneIsEditor)
                    {
                        StraightOutAttachAppliedInEditor = true;
                        IsLinked = true;
                        IsFreeAttached = true;
                        UpdateGui();
                        straightOutAttached = true;
                        return;
                    }
                    StraightOutAttachAppliedInEditor = false;
                    IsLinked = false;
                    IsFreeAttached = false;
                    straightOutAttached = false;
                    if (HighLogic.LoadedSceneIsFlight)
                    {
                        //StartCoroutine(PreparePartForFreeAttach(true));
                        PlaceFreeAttach(hittedPart, true);
                        straightOutAttached = true;
                    }
                }
            }
            else
            {
                OSD.PostMessage("Nothing has been hit.");
            }
        }

        [KSPAction("FreeAttachStraightAction", KSPActionGroup.None, guiName = "Straight Up FreeAttach")]
        public void FreeAttachStraightAction(KSPActionParam param)
        {
            if (Mode == Mode.Unlinked && !IsTargetOnly)
            {
                FreeAttachStraight();
            }
        }

        [KSPEvent(name = "Link", active = false, guiName = "Link", guiActiveEditor = false, guiActiveUnfocused = true,
            unfocusedRange = Config.UNFOCUSED_RANGE)]
        public void Link()
        {
            StraightOutAttachAppliedInEditor = false;
            if (HighLogic.LoadedSceneIsEditor)
            {
                InputLockManager.SetControlLock(EDITOR_LOCK_MASK, Config.Instance.EditorInputLockId);
            }
            Mode = Mode.Targeting;
            foreach (var possibleTarget in this.GetAllPossibleTargets())
            {
                possibleTarget.SetTargetedBy(this);
                possibleTarget.UpdateGui();
            }
            ActiveStrutsAddon.Mode = AddonMode.Link;
            ActiveStrutsAddon.CurrentTargeter = this;
            if (IsFlexible)
            {
                ActiveStrutsAddon.FlexibleAttachActive = true;
            }
            if (Config.Instance.ShowHelpTexts)
            {
                OSD.PostMessage(Config.Instance.LinkHelpText, 5);
            }
            UpdateGui();
            DeployHead(NORMAL_ANI_SPEED);
        }

        public void OnJointBreak(float breakForce)
        {
            try
            {
                partJoint.DestroyJoint();
                part.attachNodes.Remove(jointAttachNode);
                jointAttachNode.owner = null;
            }
            catch (NullReferenceException)
            {
                //already destroyed
            }
            jointBroken = true;
            PlayBreakSound();
            OSD.PostMessage("Joint broken!");
        }

        public override void OnStart(StartState state)
        {
            _findModelFeatures();
            if (ModelFeatures[ModelFeaturesType.SimpleLights])
            {
                UpdateSimpleLights();
            }
            if (ModelFeatures[ModelFeaturesType.Animation])
            {
                if (AniExtended)
                {
                    DeployHead(FAST_ANI_SPEED);
                }
                else
                {
                    RetractHead(FAST_ANI_SPEED);
                }
            }
            if (!IsFlexible)
            {
                if (!IsTargetOnly)
                {
                    if (ModelFeatures[ModelFeaturesType.LightsBright] || ModelFeatures[ModelFeaturesType.LightsDull])
                    {
                        LightsOffset *= 0.5f;
                    }
                    if (ModelFeatures[ModelFeaturesType.Strut])
                    {
                        DestroyStrut();
                    }
                }
            }
            if (HighLogic.LoadedSceneIsEditor)
            {
                part.OnEditorAttach += ProcessOnPartCopy;
            }
            Origin = part.transform;
            delayedStartFlag = true;
            ticksForDelayedStart = HighLogic.LoadedSceneIsEditor ? 0 : Config.Instance.StartDelay;
            strutRealignCounter = Config.Instance.StrutRealignInterval*(HighLogic.LoadedSceneIsEditor ? 3 : 0);
            if (SoundAttach == null || SoundBreak == null || SoundDetach == null ||
                !GameDatabase.Instance.ExistsAudioClip(Config.Instance.SoundAttachFileUrl) ||
                !GameDatabase.Instance.ExistsAudioClip(Config.Instance.SoundDetachFileUrl) ||
                !GameDatabase.Instance.ExistsAudioClip(Config.Instance.SoundBreakFileUrl))
            {
                Debug.Log("[IRAS] sounds cannot be loaded." +
                          (SoundAttach == null ? "FXGroup not instantiated" : "sound file not found"));
                soundFlag = false;
            }
            else
            {
                SetupFxGroup(SoundAttach, gameObject, Config.Instance.SoundAttachFileUrl);
                SetupFxGroup(SoundDetach, gameObject, Config.Instance.SoundDetachFileUrl);
                SetupFxGroup(SoundBreak, gameObject, Config.Instance.SoundBreakFileUrl);
                soundFlag = true;
            }
            initialized = true;
        }

        public void PlaceFreeAttach(Part targetPart, bool isStraightOut = false)
        {
            lock (freeAttachStrutUpdateLock)
            {
                oldTargetPosition = Vector3.zero;
                ActiveStrutsAddon.Mode = AddonMode.None;

                if (targetPart.Modules.Contains(Config.Instance.ModuleActiveStrutFreeAttachTarget))
                {
                    var target =
                        targetPart.Modules[Config.Instance.ModuleActiveStrutFreeAttachTarget] as
                        ModuleActiveStrutFreeAttachTarget;
                    if (target != null)
                    {
                        FreeAttachTarget = target;
                        target.Targeter = this;
                        Debug.Log("[IRAS] connected to targetpart with ID: " + FreeAttachTarget.ID);
                        if (HighLogic.LoadedSceneIsFlight && target.vessel != null)
                        {
                            IsOwnVesselConnected = target.vessel == vessel;
                        }
                        else if (HighLogic.LoadedSceneIsEditor)
                        {
                            IsOwnVesselConnected = true;
                        }
                    }
                }

                freeAttachPart = targetPart;
                Mode = Mode.Linked;
                IsLinked = true;
                IsFreeAttached = true;
                IsConnectionOrigin = true;
                DestroyJoint();
                DestroyStrut();
                IsEnforced = Config.Instance.GlobalJointEnforcement;
                if (HighLogic.LoadedSceneIsFlight)
                {
                    CreateJoint(part.Rigidbody, (IsFreeAttached && !isStraightOut) ? targetPart.parent.Rigidbody : targetPart.Rigidbody,
                        LinkType.Weak, targetPart.transform.position);
                }
                Target = null;
                Targeter = null;
                DeployHead(NORMAL_ANI_SPEED);
                OSD.PostMessage("FreeAttach Link established!");
            }
            UpdateGui();
        }

        public void PlayAttachSound()
        {
            PlayAudio(SoundAttach);
        }

        private void PlayAudio(FXGroup group)
        {
            if (!soundFlag || group == null || group.audio == null)
            {
                return;
            }
            group.audio.Play();
        }

        public void PlayBreakSound()
        {
            PlayAudio(SoundBreak);
        }

        private void PlayDeployAnimation(float speed)
        {
            if (!ModelFeatures[ModelFeaturesType.Animation])
            {
                return;
            }
            var ani = DeployAnimation;
            if (ani == null)
            {
                Debug.Log("[IRAS] animation is null!");
                return;
            }
            if (IsAnimationPlaying)
            {
                ani.Stop(AnimationName);
            }
            if (!AniExtended)
            {
                speed *= -1;
            }
            if (speed < 0)
            {
                ani[AnimationName].time = ani[AnimationName].length;
            }
            ani[AnimationName].speed = speed;
            ani.Play(AnimationName);
        }

        public void PlayDetachSound()
        {
            PlayAudio(SoundDetach);
        }

        private IEnumerator PreparePartForFreeAttach(bool straightOut = false, int tryCount = 0)
        {
            const int MAX_WAITS = 30;
            const int MAX_TRIES = 5;
            var currWaits = 0;
            var newPart = PartFactory.SpawnPartInFlight("ASTargetCube", part, new Vector3(2, 2, 2),
                part.transform.rotation);
            OSD.PostMessageLowerRightCorner("waiting for Unity to catch up...", 1.5f);
            while (!newPart.GetComponent<Rigidbody>() && currWaits < MAX_WAITS && newPart.vessel != null)
            {
                Debug.Log("[IRAS] rigidbody not ready - waiting");
                currWaits++;
                try
                {
                    DestroyImmediate(newPart.collider);
                }
                catch (Exception)
                {
                    //sanity reason
                }
                try
                {
                    newPart.transform.position = part.transform.position;
                }
                catch (NullReferenceException)
                {
                    //sanity reason
                }
                try
                {
                    newPart.mass = 0.000001f;
                    newPart.maximum_drag = 0f;
                    newPart.minimum_drag = 0f;
                }
                catch (NullReferenceException)
                {
                    //sanity reason
                }
                yield return new WaitForFixedUpdate();
            }
            if (newPart.vessel == null || (MAX_WAITS == currWaits && newPart.GetComponent<Rigidbody>() == null))
            {
                if (tryCount < MAX_TRIES)
                {
                    var nextTryCount = ++tryCount;
                    Debug.Log(
                        string.Format("[IRAS] part spawning failed => retry (vessel is null = {0} | waits = {1}/{2})",
                            (newPart.vessel == null), currWaits, MAX_WAITS));
                    StartCoroutine(PreparePartForFreeAttach(straightOut, nextTryCount));
                }
                else
                {
                    Debug.Log(
                        string.Format(
                            "[IRAS] part spawning failed more than {3} times => aborting FreeAttach (vessel is null = {0} | waits = {1}/{2})",
                            (newPart.vessel == null), currWaits, MAX_WAITS, MAX_TRIES));
                    OSD.PostMessage("FreeAttach failed because target part can not be prepared!");
                    try
                    {
                        AbortLink();
                    }
                    catch (NullReferenceException e)
                    {
                        Debug.Log("[IRAS] tried to abort link because part spawning failed, but abort throw exception: " +
                                  e.Message);
                    }
                }
                try
                {
                    newPart.Die();
                    Destroy(newPart);
                }
                catch (Exception e)
                {
                    Debug.Log(
                        "[IRAS] tried to destroy a part which failed to spawn properly in time, but operation throw exception: " +
                        e.Message);
                }
                yield break;
            }
            newPart.mass = 0.000001f;
            newPart.maximum_drag = 0f;
            newPart.minimum_drag = 0f;
            if (straightOut)
            {
                _continueWithStraightOutAttach(newPart);
            }
            else
            {
                ActiveStrutsAddon.NewSpawnedPart = newPart;
                ActiveStrutsAddon.CurrentTargeter = this;
                ActiveStrutsAddon.Mode = AddonMode.FreeAttach;
            }
        }

        public void ProcessOnPartCopy()
        {
            var allModules = Utilities.GetAllActiveStruts();
            if (allModules != null && allModules.Any(m => m.ID == ID))
            {
                ResetActiveStrutToDefault();
            }
            else
            {
                Unlink();
                Update();
            }
        }

        private void ProcessUnDock(bool undockByUnlink = false)
        {
            if (HighLogic.LoadedSceneIsEditor || (!IsLinked && !undockByUnlink) || !IsConnectionOrigin || IsTargetOnly ||
                (IsOwnVesselConnected && !IsDocked) ||
                (IsFreeAttached ? FreeAttachPart == null : Target == null) ||
                !IsDocked)
            {
                OSD.PostMessage("Can't undock");
                return;
            }
            var vi = new DockedVesselInfo
            {
                name = DockingVesselName,
                rootPartUId = DockingVesselId,
                vesselType = (VesselType) Enum.Parse(typeof (VesselType), DockingVesselTypeString)
            };
            IsDocked = false;
            if (IsFreeAttached)
            {
                FreeAttachPart.Undock(vi);
            }
            else
            {
                Target.part.Undock(vi);
            }
            UpdateGui();
            OSD.PostMessage("Undocked");
        }

        public void ProcessUnlink(bool fromUserAction, bool secondary)
        {
            StraightOutAttachAppliedInEditor = false;
            straightOutAttached = false;
            if (AniExtended)
            {
                RetractHead(NORMAL_ANI_SPEED);
            }
            if (IsFlexible)
            {
                if (flexFakeSlingLocal != null)
                {
                    DestroyImmediate(flexFakeSlingLocal);
                }
                if (flexFakeSlingTarget != null)
                {
                    DestroyImmediate(flexFakeSlingTarget);
                }
                if (flexStrut != null)
                {
                    DestroyImmediate(flexStrut);
                }
            }
            if (!IsTargetOnly && (Target != null || Targeter != null))
            {
                if (!IsConnectionOrigin && !secondary && Targeter != null)
                {
                    try
                    {
                        Targeter.Unlink();
                    }
                    catch (NullReferenceException)
                    {
                        //fail silently
                    }
                    return;
                }
                if (IsFreeAttached)
                {
                    IsFreeAttached = false;
                }
                Mode = Mode.Unlinked;
                IsLinked = false;
                DestroyJoint();
                DestroyStrut();
                oldTargetPosition = Vector3.zero;
                LinkType = LinkType.None;
                if (IsConnectionOrigin)
                {
                    if (Target != null)
                    {
                        try
                        {
                            Target.ProcessUnlink(false, true);
                            if (HighLogic.LoadedSceneIsEditor)
                            {
                                Target.Targeter = null;
                                Target = null;
                            }
                        }
                        catch (NullReferenceException)
                        {
                            //fail silently
                        }
                    }
                    if (!fromUserAction && HighLogic.LoadedSceneIsEditor)
                    {
                        OSD.PostMessage("Unlinked!");
                        PlayDetachSound();
                    }
                }
                IsConnectionOrigin = false;
                UpdateGui();
                return;
            }
            if (IsTargetOnly)
            {
                if (!this.AnyTargetersConnected())
                {
                    Mode = Mode.Unlinked;
                    IsLinked = false;
                }
                UpdateGui();
                return;
            }
            var targetPart = FreeAttachTarget;
            var destroyTarget = false;
            if (IsFreeAttached)
            {
                IsFreeAttached = false;
                destroyTarget = true;
            }
            oldTargetPosition = Vector3.zero;
            FreeAttachTarget = null;
            Mode = Mode.Unlinked;
            IsLinked = false;
            DestroyStrut();
            DestroyJoint();
            if (destroyTarget && targetPart != null)
            {
                targetPart.Die();
            }
            LinkType = LinkType.None;
            UpdateGui();
            if (!fromUserAction && HighLogic.LoadedSceneIsEditor)
            {
                OSD.PostMessage("Unlinked!");
                PlayDetachSound();
            }
        }

        private void Reconnect()
        {
            if (StraightOutAttachAppliedInEditor)
            {
                FreeAttachStraight();
                return;
            }
            if (IsFreeAttached)
            {
                if (FreeAttachTarget != null)
                {
                    PlaceFreeAttach(FreeAttachPart);
                    return;
                }
                IsFreeAttached = false;
                Mode = Mode.Unlinked;
                IsConnectionOrigin = false;
                LinkType = LinkType.None;
                UpdateGui();
                return;
            }
            var unlink = false;
            if (IsConnectionOrigin)
            {
                if (Target != null && this.IsPossibleTarget(Target))
                {
                    if (!Target.IsTargetOnly)
                    {
                        CreateStrut(
                            Target.ModelFeatures[ModelFeaturesType.HeadExtension]
                                ? Target.StrutOrigin.position
                                : Target.Origin.position, 0.5f);
                    }
                    else
                    {
                        CreateStrut(Target.ModelFeatures[ModelFeaturesType.HeadExtension]
                            ? Target.StrutOrigin.position
                            : Target.Origin.position);
                    }
                    var type = IsFlexible ? LinkType.Flexible : Target.IsTargetOnly ? LinkType.Normal : LinkType.Maximum;
                    IsEnforced = Config.Instance.GlobalJointEnforcement || type == LinkType.Maximum;
                    CreateJoint(part.GetComponent<Rigidbody>(), Target.part.parent.GetComponent<Rigidbody>(), type, Target.transform.position);
                    Mode = Mode.Linked;
                    Target.Mode = Mode.Linked;
                    IsLinked = true;
                }
                else
                {
                    unlink = true;
                }
            }
            else
            {
                if (IsTargetOnly)
                {
                    Mode = Mode.Linked;
                    IsLinked = true;
                }
                else if (Targeter != null && this.IsPossibleTarget(Targeter))
                {
                    if (!IsFlexible)
                    {
                        CreateStrut(
                            Targeter.ModelFeatures[ModelFeaturesType.HeadExtension]
                                ? Targeter.StrutOrigin.position
                                : Targeter.Origin.position, 0.5f);
                        LinkType = LinkType.Maximum;
                    }
                    else
                    {
                        LinkType = LinkType.Flexible;
                    }
                    Mode = Mode.Linked;
                    IsLinked = true;
                }
                else
                {
                    unlink = true;
                }
            }
            if (unlink)
            {
                Unlink();
            }
            UpdateGui();
        }

        private void ResetActiveStrutToDefault()
        {
            Target = null;
            Targeter = null;
            IsConnectionOrigin = false;
            IsFreeAttached = false;
            Mode = Mode.Unlinked;
            IsHalfWayExtended = false;
            Id = Guid.NewGuid().ToString();
            LinkType = LinkType.None;
            OldTargeter = null;
            FreeAttachTarget = null;
            IsFreeAttached = false;
            IsLinked = false;
            if (!IsTargetOnly)
            {
                DestroyJoint();
                DestroyStrut();
            }
        }

        private void RetractHead(float speed)
        {
            AniExtended = false;
            PlayDeployAnimation(speed);
        }

        [KSPEvent(name = "SetAsTarget", active = false, guiName = "Set as Target", guiActiveEditor = false,
            guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE)]
        public void SetAsTarget()
        {
            IsLinked = true;
            part.SetHighlightDefault();
            Mode = Mode.Linked;
            IsConnectionOrigin = false;
            IsFreeAttached = false;
            if (!IsTargetOnly && !IsFlexible)
            {
                if (ModelFeatures[ModelFeaturesType.Animation])
                {
                    DeployHead(NORMAL_ANI_SPEED);
                }
                CreateStrut(
                    Targeter.ModelFeatures[ModelFeaturesType.HeadExtension]
                        ? Targeter.StrutOrigin.position
                        : Targeter.Origin.position, 0.5f);
            }
            Targeter.SetTarget(this);
            UpdateGui();
        }

        public void SetTarget(ModuleActiveStrut target)
        {
            if (ModelFeatures[ModelFeaturesType.Animation] && !AniExtended)
            {
                DeployHead(NORMAL_ANI_SPEED);
            }
            Target = target;
            Mode = Mode.Linked;
            IsLinked = true;
            IsConnectionOrigin = true;
            var type = IsFlexible ? LinkType.Flexible : target.IsTargetOnly ? LinkType.Normal : LinkType.Maximum;
            IsEnforced = !IsFlexible && (Config.Instance.GlobalJointEnforcement || type == LinkType.Maximum);
            CreateJoint(part.GetComponent<Rigidbody>(), target.part.parent.GetComponent<Rigidbody>(), type, Target.transform.position);
            CreateStrut(
                target.ModelFeatures[ModelFeaturesType.HeadExtension]
                    ? target.StrutOrigin.position
                    : target.Origin.position, target.IsTargetOnly ? 1 : 0.5f);
            Utilities.ResetAllFromTargeting();
            OSD.PostMessage("Link established!");
            ActiveStrutsAddon.Mode = AddonMode.None;
            UpdateGui();
        }

        public void SetTargetedBy(ModuleActiveStrut targeter)
        {
            OldTargeter = Targeter;
            Targeter = targeter;
            Mode = Mode.Target;
        }

        private static void SetupFxGroup(FXGroup group, GameObject gameObject, string audioFileUrl)
        {
            group.audio = gameObject.AddComponent<AudioSource>();
            group.audio.clip = GameDatabase.Instance.GetAudioClip(audioFileUrl);
            group.audio.dopplerLevel = 0f;
            group.audio.rolloffMode = AudioRolloffMode.Linear;
            group.audio.maxDistance = 30f;
            group.audio.loop = false;
            group.audio.playOnAwake = false;
            group.audio.volume = GameSettings.SHIP_VOLUME;
        }

        public void ShowGrappler(bool show, Vector3 targetPos, Vector3 lookAtPoint, bool applyOffset,
            Vector3 targetNormalVector, bool useNormalVector = false, bool inverseOffset = false)
        {
            if (Grappler == null || ModelFeatures == null)
            {
                return;
            }

            if (!show)
            {
                Grappler.localScale = Vector3.zero;
                return;
            }

            if (!ModelFeatures[ModelFeaturesType.Grappler] )
            {
                return;
            }

            if (show && !IsTargetOnly)
            {
                Grappler.localScale = new Vector3(1, 1, 1);
                Grappler.position = Origin.position;
                Grappler.LookAt(lookAtPoint);
                Grappler.position = targetPos;
                Grappler.Rotate(new Vector3(0, 1, 0), 90f);
                if (useNormalVector)
                {
                    Grappler.rotation = Quaternion.FromToRotation(Grappler.right, targetNormalVector)*Grappler.rotation;
                }
                if (applyOffset)
                {
                    var offset = inverseOffset ? -1*GrapplerOffset : GrapplerOffset;
                    Grappler.Translate(new Vector3(offset, 0, 0));
                }
            }

        }

        public void ShowHooks(bool show, Vector3 targetPos, Vector3 targetNormalVector, bool useNormalVector = false)
        {
            if (Hooks == null || ModelFeatures == null)
            {
                return;
            }

            if (!show)
            {
                Hooks.localScale = Vector3.zero;
                return;
            }

            if (!ModelFeatures[ModelFeaturesType.Hooks])
            {
                return;
            }

            if (show && !IsTargetOnly)
            {
                Hooks.localScale = new Vector3(1, 1, 1)*HooksScaleFactor;
                Hooks.LookAt(targetPos);
                Hooks.position = targetPos;
                if (useNormalVector)
                {
                    Hooks.rotation =
                        Quaternion.FromToRotation(featureOrientation[ModelFeaturesType.Hooks].GetAxis(Hooks),
                            targetNormalVector)*Hooks.rotation;
                }
            }

        }

        [KSPEvent(name = "ToggleEnforcement", active = false, guiName = "Toggle Enforcement", guiActiveEditor = false)]
        public void ToggleEnforcement()
        {
            if (!IsLinked || !IsConnectionOrigin)
            {
                return;
            }
            IsEnforced = !IsEnforced;
            DestroyJoint();
            if (!IsFreeAttached)
            {
                CreateJoint(part.GetComponent<Rigidbody>(), Target.part.parent.GetComponent<Rigidbody>(), LinkType, Target.transform.position);
            }
            else
            {
                var rayRes = Utilities.PerformRaycast(Origin.position, FreeAttachTarget.PartOrigin.position,
                    RealModelForward);
                if (rayRes.HittedPart != null && rayRes.DistanceFromOrigin <= Config.Instance.MaxDistance)
                {
                    var moduleActiveStrutFreeAttachTarget =
                        rayRes.HittedPart.Modules[Config.Instance.ModuleActiveStrutFreeAttachTarget] as
                            ModuleActiveStrutFreeAttachTarget;
                    if (moduleActiveStrutFreeAttachTarget != null)
                    {
                        CreateJoint(part.GetComponent<Rigidbody>(), moduleActiveStrutFreeAttachTarget.PartRigidbody, LinkType.Weak,
                            (rayRes.Hit.point + Origin.position)/2);
                    }
                }
            }
            OSD.PostMessage("Joint enforcement temporarily changed.");
            UpdateGui();
        }

        [KSPEvent(name = "ToggleLink", active = false, guiName = "Toggle Link", guiActiveUnfocused = true,
            unfocusedRange = Config.UNFOCUSED_RANGE)]
        public void ToggleLink()
        {
            if (Mode == Mode.Linked)
            {
                if (IsConnectionOrigin)
                {
                    Unlink();
                }
                else
                {
                    if (Targeter != null)
                    {
                        Targeter.Unlink();
                    }
                }
            }
            else if (Mode == Mode.Unlinked &&
                     ((Target != null && Target.IsConnectionFree) || (Targeter != null && Targeter.IsConnectionFree)))
            {
                if (Target != null)
                {
                    if (this.IsPossibleTarget(Target))
                    {
                        Target.Targeter = this;
                        Target.SetAsTarget();
                    }
                    else
                    {
                        OSD.PostMessage("Can't relink at the moment, target may be obstructed.");
                    }
                }
                else if (Targeter != null)
                {
                    if (Targeter.IsPossibleTarget(this))
                    {
                        SetAsTarget();
                    }
                    else
                    {
                        OSD.PostMessage("Can't relink at the moment, targeter may be obstructed.");
                    }
                }
            }
            UpdateGui();
        }

        [KSPAction("ToggleLinkAction", KSPActionGroup.None, guiName = "Toggle Link")]
        public void ToggleLinkAction(KSPActionParam param)
        {
            if (Mode == Mode.Linked ||
                (Mode == Mode.Unlinked &&
                 ((Target != null && Target.IsConnectionFree) || (Targeter != null && Targeter.IsConnectionFree))))
            {
                ToggleLink();
            }
        }

        [KSPEvent(name = "UnDock", active = false, guiName = "Undock from Target", guiActiveEditor = false,
            guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE)]
        public void UnDock()
        {
            ProcessUnDock();
        }

        [KSPEvent(name = "Unlink", active = false, guiName = "Unlink", guiActiveEditor = false,
            guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE)]
        public void Unlink()
        {
            ProcessUnlink(true, false);
        }

        public void Update()
        {
            if (!initialized)
            {
                return;
            }
            if (delayedStartFlag)
            {
                _delayedStart();
                return;
            }
            if (jointBroken)
            {
                jointBroken = false;
                Unlink();
                return;
            }
            if (IsLinked)
            {
                if (strutRealignCounter > 0 && !IsFlexible && strutFinallyCreated &&
                    ModelFeatures[ModelFeaturesType.Strut])
                {
                    strutRealignCounter--;
                }
                else
                {
                    strutRealignCounter = Config.Instance.StrutRealignInterval;
                    UpdateSimpleLights();
                    _realignStrut();
                    if (IsFreeAttached)
                    {
                        LinkType = LinkType.Weak;
                    }
                    else if (IsConnectionOrigin)
                    {
                        if (Target != null)
                        {
                            LinkType = IsFlexible
                                ? LinkType.Flexible
                                : Target.IsTargetOnly ? LinkType.Normal : LinkType.Maximum;
                        }
                    }
                    else
                    {
                        if (Targeter != null)
                        {
                            LinkType = IsFlexible
                                ? LinkType.Flexible
                                : IsTargetOnly ? LinkType.Normal : LinkType.Maximum;
                        }
                    }
                }
            }
            else
            {
                LinkType = LinkType.None;
            }
            if (Mode == Mode.Unlinked || Mode == Mode.Target || Mode == Mode.Targeting)
            {
                if (IsTargetOnly)
                {
                    _showTargetGrappler(false);
                }
                return;
            }
            if (IsTargetOnly)
            {
                if (!this.AnyTargetersConnected())
                {
                    _showTargetGrappler(false);
                    Mode = Mode.Unlinked;
                    UpdateGui();
                    return;
                }
                _showTargetGrappler(true);
            }
            if (Mode == Mode.Linked)
            {
                if (HighLogic.LoadedSceneIsEditor)
                {
                    return;
                }
                if (IsOwnVesselConnected)
                {
                    if (IsFreeAttached)
                    {
                        if (FreeAttachPart != null)
                        {
                            if (FreeAttachPart.vessel != vessel)
                            {
                                IsOwnVesselConnected = false;
                            }
                        }
                    }
                    else if (IsTargetOnly)
                    {
                        foreach (
                            var connectedTargeter in
                                this.GetAllConnectedTargeters()
                                    .Where(
                                        connectedTargeter =>
                                            connectedTargeter.vessel != null && connectedTargeter.vessel != vessel))
                        {
                            connectedTargeter.Unlink();
                        }
                    }
                    else if (Target != null)
                    {
                        if (Target.vessel != vessel)
                        {
                            IsOwnVesselConnected = false;
                        }
                    }
                    if (!IsOwnVesselConnected)
                    {
                        Unlink();
                    }
                    UpdateGui();
                }
            }
        }

        public void UpdateGui()
        {
            Events["ToggleEnforcement"].active = Events["ToggleEnforcement"].guiActive = false;
            if (HighLogic.LoadedSceneIsEditor || IsFlexible || IsTargetOnly || !IsConnectionOrigin || !IsLinked ||
                Config.Instance.GlobalJointWeakness)
            {
                Fields["IsEnforced"].guiActive = false;
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (vessel != null && vessel.isEVA)
                {
                    Events["UnDock"].active = Events["UnDock"].guiActive = false;
                    Events["Dock"].active = Events["UnDock"].guiActive = false;
                    Events["Link"].active = Events["Link"].guiActive = false;
                    Events["AbortLink"].active = Events["AbortLink"].guiActive = false;
                    Events["ToggleLink"].active = Events["ToggleLink"].guiActive = false;
                    Events["FreeAttach"].active = Events["FreeAttach"].guiActive = false;
                    Events["SetAsTarget"].active = Events["SetAsTarget"].guiActive = false;
                    Events["FreeAttachStraight"].active = Events["FreeAttachStraight"].guiActive = false;
                    return;
                }
                switch (Mode)
                {
                    case Mode.Linked:
                    {
                        Events["Link"].active = Events["Link"].guiActive = false;
                        Events["SetAsTarget"].active = Events["SetAsTarget"].guiActive = false;
                        Events["FreeAttach"].active = Events["FreeAttach"].guiActive = false;
                        if (!IsTargetOnly)
                        {
                            Events["AbortLink"].active = Events["AbortLink"].guiActive = false;
                            if (!IsFlexible && IsConnectionOrigin && !Config.Instance.GlobalJointWeakness)
                            {
                                Events["ToggleEnforcement"].active = Events["ToggleEnforcement"].guiActive = true;
                            }
                            if (!IsFlexible)
                            {
                                Fields["IsEnforced"].guiActive = true;
                            }
                            if (IsFreeAttached)
                            {
                                Events["ToggleLink"].active = Events["ToggleLink"].guiActive = false;
                                Events["Unlink"].active = Events["Unlink"].guiActive = true;
                            }
                            else
                            {
                                Events["ToggleLink"].active = Events["ToggleLink"].guiActive = true;
                                Events["Unlink"].active = Events["Unlink"].guiActive = false;
                            }
                            if (!IsOwnVesselConnected && !IsDocked)
                            {
                                if (Config.Instance.DockingEnabled &&
                                    !(IsFreeAttached
                                        ? FreeAttachPart != null && FreeAttachPart.vessel == vessel
                                        : Target != null && Target.part != null && Target.part.vessel == vessel))
                                {
                                    Events["Dock"].active = Events["Dock"].guiActive = true;
                                }
                                Events["UnDock"].active = Events["UnDock"].guiActive = false;
                            }
                            if (!IsOwnVesselConnected && IsDocked)
                            {
                                Events["Dock"].active = Events["Dock"].guiActive = false;
                                Events["UnDock"].active = Events["UnDock"].guiActive = true;
                            }
                        }
                        else
                        {
                            Events["Unlink"].active = Events["Unlink"].guiActive = false;
                            Events["ToggleLink"].active = Events["ToggleLink"].guiActive = false;
                        }
                    }
                        break;
                    case Mode.Unlinked:
                    {
                        Events["SetAsTarget"].active = Events["SetAsTarget"].guiActive = false;
                        Events["Unlink"].active = Events["Unlink"].guiActive = false;
                        Events["UnDock"].active = Events["UnDock"].guiActive = false;
                        Events["Dock"].active = Events["Dock"].guiActive = false;
                        if (IsTargetOnly)
                        {
                            Events["Link"].active = Events["Link"].guiActive = false;
                        }
                        else
                        {
                            Events["Link"].active = Events["Link"].guiActive = true;
                            if (!IsFlexible)
                            {
                                Events["FreeAttach"].active = Events["FreeAttach"].guiActive = true;
                            }
                            else
                            {
                                Events["FreeAttach"].active = Events["FreeAttach"].guiActive = false;
                            }
                            Events["AbortLink"].active = Events["AbortLink"].guiActive = false;
                            if ((Target != null && Target.IsConnectionFree) ||
                                (Targeter != null && Targeter.IsConnectionFree))
                            {
                                Events["ToggleLink"].active = Events["ToggleLink"].guiActive = true;
                            }
                            else
                            {
                                Events["ToggleLink"].active = Events["ToggleLink"].guiActive = false;
                            }
                        }
                    }
                        break;
                    case Mode.Target:
                    {
                        Events["UnDock"].active = Events["UnDock"].guiActive = false;
                        Events["Dock"].active = Events["Dock"].guiActive = false;
                        Events["SetAsTarget"].active = Events["SetAsTarget"].guiActive = true;
                        if (!IsTargetOnly)
                        {
                            Events["Link"].active = Events["Link"].guiActive = false;
                        }
                        Events["ToggleLink"].active = Events["ToggleLink"].guiActive = false;
                        Events["FreeAttach"].active = Events["FreeAttach"].guiActive = false;
                    }
                        break;
                    case Mode.Targeting:
                    {
                        Events["UnDock"].active = Events["UnDock"].guiActive = false;
                        Events["Dock"].active = Events["Dock"].guiActive = false;
                        Events["Link"].active = Events["Link"].guiActive = false;
                        Events["AbortLink"].active = Events["AbortLink"].guiActive = true;
                        Events["ToggleLink"].active = Events["ToggleLink"].guiActive = false;
                        Events["FreeAttach"].active = Events["FreeAttach"].guiActive = false;
                    }
                        break;
                }
                Events["FreeAttachStraight"].active =
                    Events["FreeAttachStraight"].guiActive = Events["FreeAttach"].active;
            }
            else if (HighLogic.LoadedSceneIsEditor)
            {
                Events["ToggleLink"].active =
                    Events["ToggleLink"].guiActive = Events["ToggleLink"].guiActiveEditor = false;
                Events["UnDock"].active = Events["UnDock"].guiActive = Events["UnDock"].guiActiveEditor = false;
                Events["Dock"].active = Events["Dock"].guiActive = Events["Dock"].guiActiveEditor = false;
                switch (Mode)
                {
                    case Mode.Linked:
                    {
                        if (!IsTargetOnly)
                        {
                            Events["Unlink"].active =
                                Events["Unlink"].guiActive = Events["Unlink"].guiActiveEditor = true;
                        }
                        Events["Link"].active = Events["Link"].guiActive = Events["Link"].guiActiveEditor = false;
                        Events["SetAsTarget"].active =
                            Events["SetAsTarget"].guiActive = Events["SetAsTarget"].guiActiveEditor = false;
                        Events["AbortLink"].active =
                            Events["AbortLink"].guiActive = Events["AbortLink"].guiActiveEditor = false;
                        Events["FreeAttach"].active =
                            Events["FreeAttach"].guiActive = Events["FreeAttach"].guiActiveEditor = false;
                    }
                        break;
                    case Mode.Unlinked:
                    {
                        Events["Unlink"].active = Events["Unlink"].guiActive = Events["Unlink"].guiActiveEditor = false;
                        Events["SetAsTarget"].active =
                            Events["SetAsTarget"].guiActive = Events["SetAsTarget"].guiActiveEditor = false;
                        Events["AbortLink"].active =
                            Events["AbortLink"].guiActive = Events["AbortLink"].guiActiveEditor = false;
                        if (!IsTargetOnly)
                        {
                            Events["Link"].active = Events["Link"].guiActive = Events["Link"].guiActiveEditor = true;
                            if (!IsFlexible)
                            {
                                Events["FreeAttach"].active =
                                    Events["FreeAttach"].guiActive = Events["FreeAttach"].guiActiveEditor = true;
                            }
                            else
                            {
                                Events["FreeAttach"].active =
                                    Events["FreeAttach"].guiActive = Events["FreeAttach"].guiActiveEditor = false;
                            }
                        }
                    }
                        break;
                    case Mode.Target:
                    {
                        Events["Unlink"].active = Events["Unlink"].guiActive = Events["Unlink"].guiActiveEditor = false;
                        Events["Link"].active = Events["Link"].guiActive = Events["Link"].guiActiveEditor = false;
                        Events["SetAsTarget"].active =
                            Events["SetAsTarget"].guiActive = Events["SetAsTarget"].guiActiveEditor = true;
                        Events["AbortLink"].active =
                            Events["AbortLink"].guiActive = Events["AbortLink"].guiActiveEditor = false;
                        Events["FreeAttach"].active =
                            Events["FreeAttach"].guiActive = Events["FreeAttach"].guiActiveEditor = false;
                    }
                        break;
                    case Mode.Targeting:
                    {
                        Events["Unlink"].active = Events["Unlink"].guiActive = Events["Unlink"].guiActiveEditor = false;
                        Events["Link"].active = Events["Link"].guiActive = Events["Link"].guiActiveEditor = false;
                        Events["SetAsTarget"].active =
                            Events["SetAsTarget"].guiActive = Events["SetAsTarget"].guiActiveEditor = false;
                        Events["AbortLink"].active =
                            Events["AbortLink"].guiActive = Events["AbortLink"].guiActiveEditor = true;
                        Events["FreeAttach"].active =
                            Events["FreeAttach"].guiActive = Events["FreeAttach"].guiActiveEditor = false;
                    }
                        break;
                }
                Events["FreeAttachStraight"].active =
                    Events["FreeAttachStraight"].guiActive =
                        Events["FreeAttachStraight"].guiActiveEditor = Events["FreeAttach"].active;
                if (!Config.Instance.AllowFreeAttachInEditor)
                {
                    Events["FreeAttach"].guiActiveEditor = false;
                }
            }
        }

        private IEnumerator WaitAndCreateFlexibleJoint()
        {
            localFlexAnchor = Utilities.CreateLocalAnchor("ActiveFlexJoint", true);
            localFlexAnchor.transform.position = Target.FlexOffsetOriginPosition;
            flexStrut = Utilities.CreateFlexStrut("ActiveFlexJointStrut", false, Color.black);
            flexFakeSlingLocal = Utilities.CreateFakeRopeSling("ActiveFlexJointStrutSling", false, Color.black);
            flexFakeSlingTarget = Utilities.CreateFakeRopeSling("ActiveFlexJointStrutSling", false, Color.black);
            yield return new WaitForSeconds(0.1f);
            yield return new WaitForFixedUpdate();
            localFlexAnchor.transform.position = Target.FlexOffsetOriginPosition;
            var distance = Vector3.Distance(FlexOffsetOriginPosition, Target.FlexOffsetOriginPosition);
            flexJoint = localFlexAnchor.AddComponent<SpringJoint>();
            flexJoint.spring = FlexibleStrutSpring;
            flexJoint.damper = FlexibleStrutDamper;
            flexJoint.anchor = localFlexAnchor.transform.position;
            flexJoint.connectedBody = Target.part.parent.GetComponent<Rigidbody>();
            flexJoint.maxDistance = distance + 0.25f;
            flexJoint.breakForce = Mathf.Infinity;
            flexJoint.breakTorque = Mathf.Infinity;
            yield return new WaitForSeconds(0.1f);
            yield return new WaitForFixedUpdate();
            localFlexAnchor.transform.position = FlexOffsetOriginPosition;
            yield return new WaitForFixedUpdate();
            localFlexAnchorFixedJoint = localFlexAnchor.AddComponent<FixedJoint>();
            localFlexAnchorFixedJoint.connectedBody = part.GetComponent<Rigidbody>();
            localFlexAnchorFixedJoint.breakForce = localFlexAnchorFixedJoint.breakTorque = Mathf.Infinity;
        }

        private void _continueWithStraightOutAttach(Part newPart)
        {
            var rayres = _performStraightOutRaycast();
            if (rayres.Item1)
            {
                ActiveStrutsAddon.NewSpawnedPart = newPart;
                ActiveStrutsAddon.CurrentTargeter = this;
                StartCoroutine(ActiveStrutsAddon.PlaceNewPart(rayres.Item2.PartFromHit(), rayres.Item2));
                return;
            }
            OSD.PostMessage("Straight Out Attach failed!");
            Debug.Log("[IRAS] straight out raycast didn't hit anything after part creation");
            DestroyImmediate(newPart);
        }

        private void _delayedStart()
        {
            if (ticksForDelayedStart > 0)
            {
                ticksForDelayedStart--;
                return;
            }
            delayedStartFlag = false;
            if (Id == Guid.Empty.ToString())
            {
                Id = Guid.NewGuid().ToString();
            }
            if (HighLogic.LoadedSceneIsFlight && !IdResetDone)
            {
                ActiveStrutsAddon.Enqueue(this);
            }
            if (IsLinked)
            {
                if (IsTargetOnly)
                {
                    Mode = Mode.Linked;
                }
                else
                {
                    Reconnect();
                }
            }
            else
            {
                Mode = Mode.Unlinked;
            }
            Events.Sort((l, r) =>
            {
                if (l.name == "Link" && r.name == "FreeAttach")
                {
                    return -1;
                }
                if (r.name == "Link" && l.name == "FreeAttach")
                {
                    return 1;
                }
                if (l.name == "FreeAttach" && r.name == "FreeAttachStraight")
                {
                    return -1;
                }
                if (r.name == "FreeAttach" && l.name == "FreeAttachStraight")
                {
                    return 1;
                }
                if (l.name == "Link" && r.name == "FreeAttachStraight")
                {
                    return -1;
                }
                if (r.name == "Link" && l.name == "FreeAttachStraight")
                {
                    return 1;
                }
                if (r.name == "ToggleEnforcement")
                {
                    return 1;
                }
                if (l.name == "ToggleEnforcement")
                {
                    return -1;
                }
                return string.Compare(l.name, r.name, StringComparison.Ordinal);
            }
                );
            UpdateGui();
        }

        private void _findModelFeatures()
        {
            ModelFeatures = new Dictionary<ModelFeaturesType, bool>();
            featureOrientation = new Dictionary<ModelFeaturesType, OrientationInfo>();
            if (!string.IsNullOrEmpty(GrapplerName))
            {
                Grappler = part.FindModelTransform(GrapplerName);
                ModelFeatures.Add(ModelFeaturesType.Grappler, true);
            }
            else
            {
                ModelFeatures.Add(ModelFeaturesType.Grappler, false);
            }
            if (!string.IsNullOrEmpty(StrutName))
            {
                Strut = part.FindModelTransform(StrutName);
                ModelFeatures.Add(ModelFeaturesType.Strut, true);
                DestroyImmediate(Strut.GetComponent<Collider>());
            }
            else
            {
                if (!IsTargetOnly)
                {
                    simpleStrut = Utilities.CreateSimpleStrut("Targeterstrut");
                    simpleStrut.SetActive(true);
                    simpleStrut.transform.localScale = Vector3.zero;
                    Strut = simpleStrut.transform;
                }
                ModelFeatures.Add(ModelFeaturesType.Strut, false);
            }
            if (!string.IsNullOrEmpty(HooksName))
            {
                Hooks = part.FindModelTransform(HooksName);
                ModelFeatures.Add(ModelFeaturesType.Hooks, true);
                featureOrientation.Add(ModelFeaturesType.Hooks, new OrientationInfo(HooksForward));
                DestroyImmediate(Hooks.GetComponent<Collider>());
            }
            else
            {
                ModelFeatures.Add(ModelFeaturesType.Hooks, false);
            }
            if (!string.IsNullOrEmpty(LightsBrightName))
            {
                LightsBright = part.FindModelTransform(LightsBrightName);
                ModelFeatures.Add(ModelFeaturesType.LightsBright, true);
                DestroyImmediate(LightsBright.GetComponent<Collider>());
            }
            else
            {
                ModelFeatures.Add(ModelFeaturesType.LightsBright, false);
            }
            if (!string.IsNullOrEmpty(LightsDullName))
            {
                LightsDull = part.FindModelTransform(LightsDullName);
                ModelFeatures.Add(ModelFeaturesType.LightsDull, true);
                DestroyImmediate(LightsDull.GetComponent<Collider>());
            }
            else
            {
                ModelFeatures.Add(ModelFeaturesType.LightsDull, false);
            }
            if (!string.IsNullOrEmpty(HeadName))
            {
                var head = part.FindModelTransform(HeadName);
                StrutOrigin = head.transform;
                headTransform = head;
                ModelFeatures.Add(ModelFeaturesType.HeadExtension, true);
            }
            else
            {
                ModelFeatures.Add(ModelFeaturesType.HeadExtension, false);
            }
            if (!string.IsNullOrEmpty(SimpleLightsName))
            {
                simpleLights = part.FindModelTransform(SimpleLightsName);
                simpleLightsSecondary = part.FindModelTransform(SimpleLightsSecondaryName);
                featureOrientation.Add(ModelFeaturesType.SimpleLights, new OrientationInfo(SimpleLightsForward));
                ModelFeatures.Add(ModelFeaturesType.SimpleLights, true);
            }
            else
            {
                ModelFeatures.Add(ModelFeaturesType.SimpleLights, false);
            }
            ModelFeatures.Add(ModelFeaturesType.Animation, !string.IsNullOrEmpty(AnimationName));
        }

        private void ManageAttachNode(float breakForce)
        {
            if (!IsConnectionOrigin || IsTargetOnly || jointAttachNode != null || !HighLogic.LoadedSceneIsFlight)
            {
                return;
            }
            try
            {
                var targetPart = IsFreeAttached ? FreeAttachPart : Target.part;
                if (targetPart == null)
                {
                    return;
                }
                var freeAttachedHitPoint = IsFreeAttached ? FreeAttachPart.transform.position : Vector3.zero;
                var normDir =
                    (Origin.position - (IsFreeAttached ? FreeAttachPart.transform.position : Target.Origin.position))
                        .normalized;
                jointAttachNode = new AttachNode {id = Guid.NewGuid().ToString(), attachedPart = targetPart};
                jointAttachNode.breakingForce = jointAttachNode.breakingTorque = breakForce;
                jointAttachNode.position =
                    targetPart.partTransform.InverseTransformPoint(IsFreeAttached
                        ? freeAttachedHitPoint
                        : targetPart.partTransform.position);
                jointAttachNode.orientation = targetPart.partTransform.InverseTransformDirection(normDir);
                jointAttachNode.size = 1;
                jointAttachNode.ResourceXFeed = false;
                jointAttachNode.attachMethod = AttachNodeMethod.FIXED_JOINT;
                part.attachNodes.Add(jointAttachNode);
                jointAttachNode.owner = part;
                partJoint = PartJoint.Create(part, IsFreeAttached ? (targetPart.parent ?? targetPart) : targetPart,
                    jointAttachNode, null, AttachModes.SRF_ATTACH);
            }
            catch (Exception e)
            {
                jointAttachNode = null;
                Debug.Log("[IRAS] failed to create attachjoint: " + e.Message + " " + e.StackTrace);
            }
        }

        private void MoveFakeRopeSling(bool local, GameObject sling)
        {
            sling.SetActive(false);
            var trans = sling.transform;
            trans.rotation = local ? Origin.rotation : Target.Origin.rotation;
            trans.Rotate(new Vector3(0, 0, 1), 90f);
            trans.Rotate(new Vector3(1, 0, 0), 90f);
            trans.LookAt(local ? Target.FlexOffsetOriginPosition : FlexOffsetOriginPosition);
            var dir = Target.FlexOffsetOriginPosition - FlexOffsetOriginPosition;
            if (!local)
            {
                dir *= -1;
            }
            trans.position = (local ? FlexOffsetOriginPosition : Target.FlexOffsetOriginPosition) +
                             (dir.normalized*FlexibleStrutSlingOffset);
            sling.SetActive(true);
        }

        private Tuple<bool, RaycastHit> _performStraightOutRaycast()
        {
            var rayRes = Utilities.PerformRaycastIntoDir(Origin.position, RealModelForward, RealModelForward, part);
            return new Tuple<bool, RaycastHit>(rayRes.HitResult, rayRes.Hit);
        }

        private void _realignStrut()
        {
            if (IsFreeAttached)
            {
                lock (freeAttachStrutUpdateLock)
                {
                    Vector3[] targetPos;
                    if (StraightOutAttachAppliedInEditor || straightOutAttached)
                    {
                        var raycast = _performStraightOutRaycast();
                        if (!raycast.Item1)
                        {
                            DestroyStrut();
                            IsLinked = false;
                            IsFreeAttached = false;
                            return;
                        }
                        targetPos = new[] {raycast.Item2.point, raycast.Item2.normal};
                    }
                    else
                    {
                        var raycast = Utilities.PerformRaycast(Origin.position, FreeAttachPart.transform.position,
                            Origin.up, new[] {FreeAttachPart, part});
                        targetPos = new[] {FreeAttachPart.transform.position, raycast.Hit.normal};
                    }
                    if (strutFinallyCreated && ModelFeatures[ModelFeaturesType.Strut] && !IsFlexible &&
                        (Vector3.Distance(targetPos[0], oldTargetPosition) <=
                         Config.Instance.StrutRealignDistanceTolerance))
                    {
                        return;
                    }
                    oldTargetPosition = targetPos[0];
                    DestroyStrut();
                    CreateStrut(targetPos[0]);
                    ShowGrappler(true, targetPos[0], targetPos[0], false, targetPos[1], true);
                    ShowHooks(true, targetPos[0], targetPos[1], true);
                }
            }
            else if (!IsTargetOnly)
            {
                if (Target == null || !IsConnectionOrigin)
                {
                    return;
                }
                var refPos = IsFlexible
                    ? Target.FlexOffsetOriginPosition
                    : Target.ModelFeatures[ModelFeaturesType.HeadExtension]
                        ? Target.StrutOrigin.position
                        : Target.Origin.position;
                if ((strutFinallyCreated && ModelFeatures[ModelFeaturesType.Strut] && !IsFlexible &&
                     Vector3.Distance(refPos, oldTargetPosition) <= Config.Instance.StrutRealignDistanceTolerance))
                {
                    return;
                }
                oldTargetPosition = refPos;
                DestroyStrut();
                if (IsFlexible)
                {
                    CreateStrut(refPos);
                }
                else if (Target.IsTargetOnly)
                {
                    CreateStrut(Target.ModelFeatures[ModelFeaturesType.HeadExtension]
                        ? Target.StrutOrigin.position
                        : Target.Origin.position);
                    ShowGrappler(false, Vector3.zero, Vector3.zero, false, Vector3.zero);
                }
                else
                {
                    var targetStrutPos = Target.ModelFeatures[ModelFeaturesType.HeadExtension]
                        ? Target.StrutOrigin.position
                        : Target.Origin.position;
                    var localStrutPos = ModelFeatures[ModelFeaturesType.HeadExtension]
                        ? StrutOrigin.position
                        : Origin.position;
                    Target.DestroyStrut();
                    CreateStrut(targetStrutPos, 0.5f, -1*GrapplerOffset);
                    Target.CreateStrut(localStrutPos, 0.5f, -1*GrapplerOffset);
                    ShowHooks(false, Vector3.zero, Vector3.zero);
                    Target.ShowHooks(false, Vector3.zero, Vector3.zero);
                    var grapplerTargetPos = ((targetStrutPos - localStrutPos)*0.5f) + localStrutPos;
                    ShowGrappler(true, grapplerTargetPos, targetStrutPos, true, Vector3.zero);
                    Target.ShowGrappler(true, grapplerTargetPos, localStrutPos, true, Vector3.zero);
                }
            }
        }

        private void _showTargetGrappler(bool show)
        {
            if (!IsTargetOnly || !ModelFeatures[ModelFeaturesType.Grappler])
            {
                return;
            }
            if (show && !targetGrapplerVisible)
            {
                Grappler.Translate(new Vector3(-GrapplerOffset, 0, 0));
                targetGrapplerVisible = true;
            }
            else if (!show && targetGrapplerVisible)
            {
                Grappler.Translate(new Vector3(GrapplerOffset, 0, 0));
                targetGrapplerVisible = false;
            }
        }

        private void TransformLights(bool show, Vector3 lookAtTarget, bool bright = false)
        {
            if (LightsDull == null || LightsBright == null || ModelFeatures == null)
                return;
            
            if (!(ModelFeatures[ModelFeaturesType.LightsBright] && ModelFeatures[ModelFeaturesType.LightsDull]))
            {
                return;
            }
            if (!show)
            {
                LightsBright.localScale = Vector3.zero;
                LightsDull.localScale = Vector3.zero;
                if (dullLightsExtended)
                {
                    LightsDull.Translate(new Vector3(LightsOffset, 0, 0));
                    dullLightsExtended = false;
                }
                if (brightLightsExtended)
                {
                    LightsBright.Translate(new Vector3(LightsOffset, 0, 0));
                    brightLightsExtended = false;
                }
                return;
            }
            if (bright)
            {
                LightsDull.localScale = Vector3.zero;
                LightsBright.LookAt(lookAtTarget);
                LightsBright.Rotate(new Vector3(0, 1, 0), 90f);
                LightsBright.localScale = new Vector3(1, 1, 1);
                if (!brightLightsExtended)
                {
                    LightsBright.Translate(new Vector3(-LightsOffset, 0, 0));
                }
                if (dullLightsExtended)
                {
                    LightsDull.Translate(new Vector3(LightsOffset, 0, 0));
                }
                dullLightsExtended = false;
                brightLightsExtended = true;
                return;
            }
            LightsBright.localScale = Vector3.zero;
            LightsDull.LookAt(lookAtTarget);
            LightsDull.Rotate(new Vector3(0, 1, 0), 90f);
            LightsDull.position = Origin.position;
            LightsDull.localScale = new Vector3(1, 1, 1);
            if (!dullLightsExtended)
            {
                LightsDull.Translate(new Vector3(-LightsOffset, 0, 0));
            }
            if (brightLightsExtended)
            {
                LightsBright.Translate(new Vector3(LightsOffset, 0, 0));
            }
            dullLightsExtended = true;
            brightLightsExtended = false;
        }

        private void UpdateSimpleLights()
        {
            try
            {
                if (!ModelFeatures[ModelFeaturesType.SimpleLights])
                {
                    return;
                }
            }
            catch (KeyNotFoundException)
            {
                return;
            }
            catch (NullReferenceException)
            {
                return;
            }
            Color col;
            if (IsLinked)
            {
                col = Utilities.SetColorForEmissive(IsDocked ? Color.blue : Color.green);
            }
            else
            {
                col = Utilities.SetColorForEmissive(Color.yellow);
            }
            foreach (
                var m in
                    new[] {simpleLights, simpleLightsSecondary}.Select(
                    lightTransform => lightTransform.GetComponent<Renderer>().material))
            {
                m.SetColor("_Emissive", col);
                m.SetColor("_MainTex", col);
                m.SetColor("_EmissiveColor", col);
            }
        }

        internal enum ModelFeaturesType
        {
            Strut,
            Grappler,
            Hooks,
            LightsBright,
            LightsDull,
            SimpleLights,
            HeadExtension,
            Animation
        }

        private class OrientationInfo
        {
            internal OrientationInfo(string stringToParse)
            {
                if (string.IsNullOrEmpty(stringToParse))
                {
                    Orientation = Orientations.Up;
                    Invert = false;
                    return;
                }
                var substrings = stringToParse.Split(',').Select(s => s.Trim().ToUpperInvariant()).ToList();
                if (substrings.Count == 2)
                {
                    var oS = substrings[0];
                    if (oS == "RIGHT")
                    {
                        Orientation = Orientations.Right;
                    }
                    else if (oS == "FORWARD")
                    {
                        Orientation = Orientations.Forward;
                    }
                    else
                    {
                        Orientation = Orientations.Up;
                    }
                    bool outBool;
                    bool.TryParse(substrings[1], out outBool);
                    Invert = outBool;
                }
            }

            internal OrientationInfo(Orientations orientation, bool invert)
            {
                Orientation = orientation;
                Invert = invert;
            }

            private bool Invert { get; set; }
            private Orientations Orientation { get; set; }

            internal Vector3 GetAxis(Transform transform)
            {
                var axis = Vector3.zero;
                if (transform == null)
                {
                    return axis;
                }
                switch (Orientation)
                {
                    case Orientations.Forward:
                    {
                        axis = transform.forward;
                    }
                        break;
                    case Orientations.Right:
                    {
                        axis = transform.right;
                    }
                        break;
                    case Orientations.Up:
                    {
                        axis = transform.up;
                    }
                        break;
                }
                axis = Invert ? axis*-1f : axis;
                return axis;
            }
        }

        private enum Orientations
        {
            Up,
            Forward,
            Right
        }
    }
}