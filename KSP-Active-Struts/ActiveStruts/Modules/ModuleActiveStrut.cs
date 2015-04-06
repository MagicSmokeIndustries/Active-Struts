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
using CIT_Util;
using CIT_Util.Types;
using UnityEngine;
using OSD = ActiveStruts.Util.OSD;
using Utilities = ActiveStruts.Util.Utilities;

namespace ActiveStruts.Modules
{
    public class ModuleActiveStrut : PartModule, IDResetable
    {
        private const ControlTypes EditorLockMask = ControlTypes.EDITOR_PAD_PICK_PLACE | ControlTypes.EDITOR_ICON_PICK;
        private const float NormalAniSpeed = 1.5f;
        private const float FastAniSpeed = 1000f;
        private readonly object _freeAttachStrutUpdateLock = new object();
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
        private bool _brightLightsExtended;
        private bool _delayedStartFlag;
        private bool _dullLightsExtended = true;
        private Dictionary<ModelFeaturesType, OrientationInfo> _featureOrientation;
        private GameObject _flexFakeSlingLocal;
        private GameObject _flexFakeSlingTarget;
        private SpringJoint _flexJoint;
        private GameObject _flexStrut;
        private Part _freeAttachPart;
        private ModuleActiveStrutFreeAttachTarget _freeAttachTarget;
        private Transform _headTransform;
        private bool _initialized;
        private ConfigurableJoint _joint;
        private AttachNode _jointAttachNode;
        private bool _jointBroken;
        private LinkType _linkType;
        private GameObject _localFlexAnchor;
        private FixedJoint _localFlexAnchorFixedJoint;
        private Mode _mode = Mode.Undefined;
        private Vector3 _oldTargetPosition = Vector3.zero;
        private PartJoint _partJoint;
        private Transform _simpleLights;
        private Transform _simpleLightsSecondary;
        private GameObject _simpleStrut;
        private bool _soundFlag;
        private bool _strutFinallyCreated;
        private int _strutRealignCounter;
        private bool _targetGrapplerVisible;
        private int _ticksForDelayedStart;
        private bool _straightOutAttached;

        public Animation DeployAnimation
        {
            get
            {
                if (string.IsNullOrEmpty(this.AnimationName))
                {
                    return null;
                }
                return this.part.FindModelAnimators(this.AnimationName)[0];
            }
        }

        internal Vector3 FlexOffsetOriginPosition
        {
            get { return this.Origin.position + (this.Origin.up*this.FlexibleStrutOffset); }
        }

        private Part FreeAttachPart
        {
            get
            {
                if (this._freeAttachPart != null)
                {
                    return this._freeAttachPart;
                }
                if (this.FreeAttachTarget != null)
                {
                    this._freeAttachPart = this.FreeAttachTarget.part;
                }
                return this._freeAttachPart;
            }
        }

        public Vector3 FreeAttachPositionOffset
        {
            get
            {
                if (this.FreeAttachPositionOffsetVector == null)
                {
                    return Vector3.zero;
                }
                var vArr = this.FreeAttachPositionOffsetVector.Split(' ').Select(Convert.ToSingle).ToArray();
                return new Vector3(vArr[0], vArr[1], vArr[2]);
            }
            set { this.FreeAttachPositionOffsetVector = String.Format("{0} {1} {2}", value.x, value.y, value.z); }
        }

        public ModuleActiveStrutFreeAttachTarget FreeAttachTarget
        {
            get { return this._freeAttachTarget ?? (this._freeAttachTarget = Utilities.FindFreeAttachTarget(new Guid(this.FreeAttachTargetId))); }
            set
            {
                this.FreeAttachTargetId = value != null ? value.ID.ToString() : Guid.Empty.ToString();
                this._freeAttachTarget = value;
            }
        }

        public Guid ID
        {
            get
            {
                if (this.Id == null || new Guid(this.Id) == Guid.Empty)
                {
                    this.Id = Guid.NewGuid().ToString();
                }
                return new Guid(this.Id);
            }
        }

        private bool IsAnimationPlaying
        {
            get
            {
                var ani = this.DeployAnimation;
                return ani != null && ani.IsPlaying(this.AnimationName);
            }
        }

        public bool IsConnectionFree
        {
            get { return this.IsTargetOnly || !this.IsLinked || (this.IsLinked && this.Mode == Mode.Unlinked); }
        }

        public LinkType LinkType
        {
            get { return this._linkType; }
            set
            {
                this._linkType = value;
                this.Strength = value.ToString();
            }
        }

        public Mode Mode
        {
            get { return this._mode; }
            set
            {
                this._mode = value;
                this.State = value.ToString();
            }
        }

        public Vector3 RealModelForward
        {
            get
            {
                if (this.InvertRightAxis)
                {
                    return this.Origin.right*-1;
                }
                return this.Origin.right;
            }
        }

        public ModuleActiveStrut Target
        {
            get { return this.TargetId == Guid.Empty.ToString() ? null : Utilities.GetStrutById(new Guid(this.TargetId)); }
            set { this.TargetId = value != null ? value.ID.ToString() : Guid.Empty.ToString(); }
        }

        public ModuleActiveStrut Targeter
        {
            get { return this.TargeterId == Guid.Empty.ToString() ? null : Utilities.GetStrutById(new Guid(this.TargeterId)); }
            set { this.TargeterId = value != null ? value.ID.ToString() : Guid.Empty.ToString(); }
        }

        public void ResetId()
        {
            var oldId = this.Id;
            this.Id = Guid.NewGuid().ToString();
            foreach (var moduleActiveStrut in Utilities.GetAllActiveStruts())
            {
                if (moduleActiveStrut.TargetId != null && moduleActiveStrut.TargetId == oldId)
                {
                    moduleActiveStrut.TargetId = this.Id;
                }
                if (moduleActiveStrut.TargeterId != null && moduleActiveStrut.TargeterId == oldId)
                {
                    moduleActiveStrut.TargeterId = this.Id;
                }
            }
            this.IdResetDone = true;
        }

        [KSPEvent(name = "AbortLink", active = false, guiName = "Abort Link", guiActiveEditor = true, guiActiveUnfocused = true, unfocusedRange = Config.UnfocusedRange)]
        public void AbortLink()
        {
            this.Mode = Mode.Unlinked;
            Utilities.ResetAllFromTargeting();
            ActiveStrutsAddon.Mode = AddonMode.None;
            ActiveStrutsAddon.FlexibleAttachActive = false;
            if (HighLogic.LoadedSceneIsEditor)
            {
                InputLockManager.RemoveControlLock(Config.Instance.EditorInputLockId);
            }
            OSD.PostMessage("Link aborted.");
            this.UpdateGui();
            this.RetractHead(NormalAniSpeed);
        }

        public void CreateJoint(Rigidbody originBody, Rigidbody targetBody, LinkType type, Vector3 anchorPosition)
        {
            if (HighLogic.LoadedSceneIsFlight && this.part != null && this.part.attachJoint != null && this.part.attachJoint.Joint != null)
            {
                this.part.attachJoint.Joint.breakForce = Mathf.Infinity;
                this.part.attachJoint.Joint.breakTorque = Mathf.Infinity;
                if (!this.IsFreeAttached && this.Target != null && this.Target.part != null && this.Target.part.attachJoint != null && this.Target.part.attachJoint.Joint != null)
                {
                    this.Target.part.attachJoint.Joint.breakForce = Mathf.Infinity;
                    this.Target.part.attachJoint.Joint.breakTorque = Mathf.Infinity;
                }
            }
            this.LinkType = type;
            if (this.IsFlexible)
            {
                this.StartCoroutine(this.WaitAndCreateFlexibleJoint());
            }
            var breakForce = type.GetJointStrength();
            if (!this.IsFreeAttached)
            {
                var moduleActiveStrut = this.Target;
                if (moduleActiveStrut != null)
                {
                    moduleActiveStrut.LinkType = type;
                    this.IsOwnVesselConnected = moduleActiveStrut.vessel == this.vessel;
                }
            }
            else
            {
                this.IsOwnVesselConnected = this.FreeAttachPart.vessel == this.vessel;
            }
            if (this.IsFlexible)
            {
                return;
            }
            if (!this.IsEnforced || Config.Instance.GlobalJointWeakness)
            {
                this._joint = originBody.gameObject.AddComponent<ConfigurableJoint>();
                this._joint.connectedBody = targetBody;
                this._joint.breakForce = this._joint.breakTorque = Mathf.Infinity;
                this._joint.xMotion = ConfigurableJointMotion.Locked;
                this._joint.yMotion = ConfigurableJointMotion.Locked;
                this._joint.zMotion = ConfigurableJointMotion.Locked;
                this._joint.angularXMotion = ConfigurableJointMotion.Locked;
                this._joint.angularYMotion = ConfigurableJointMotion.Locked;
                this._joint.angularZMotion = ConfigurableJointMotion.Locked;
                this._joint.projectionAngle = 0f;
                this._joint.projectionDistance = 0f;
                this._joint.targetPosition = anchorPosition;
                this._joint.anchor = anchorPosition;
            }
            else
            {
                this._manageAttachNode(breakForce);
            }
            this.PlayAttachSound();
        }

        public void CreateStrut(Vector3 target, float distancePercent = 1, float strutOffset = 0f)
        {
            if (this.IsFlexible)
            {
                if (this._flexStrut != null)
                {
                    this._flexStrut.SetActive(false);
                    var trans = this._flexStrut.transform;
                    trans.position = this.FlexOffsetOriginPosition;
                    trans.LookAt(this.Target.FlexOffsetOriginPosition);
                    trans.localScale = new Vector3(trans.position.x, trans.position.y, 1);
                    var dist = (Vector3.Distance(Vector3.zero, trans.InverseTransformPoint(this.Target.FlexOffsetOriginPosition))/2.0f);
                    trans.localScale = new Vector3(0.025f, dist, 0.025f);
                    trans.Rotate(new Vector3(0, 0, 1), 90f);
                    trans.Rotate(new Vector3(1, 0, 0), 90f);
                    trans.Translate(new Vector3(0f, dist, 0f));
                    this._flexStrut.SetActive(true);
                }
                if (this._flexFakeSlingLocal != null)
                {
                    this._moveFakeRopeSling(true, this._flexFakeSlingLocal);
                }
                if (this._flexFakeSlingTarget != null)
                {
                    this._moveFakeRopeSling(false, this._flexFakeSlingTarget);
                }
            }
            else
            {
                if (this.ModelFeatures[ModelFeaturesType.Animation])
                {
                    if (this.IsAnimationPlaying)
                    {
                        return;
                    }
                }
                if (this.Target != null && this.Target.ModelFeatures[ModelFeaturesType.Animation])
                {
                    if (this.Target.IsAnimationPlaying)
                    {
                        return;
                    }
                }
                if (this.Targeter != null && this.Targeter.ModelFeatures[ModelFeaturesType.Animation])
                {
                    if (this.Targeter.IsAnimationPlaying)
                    {
                        return;
                    }
                }
                if (this.ModelFeatures[ModelFeaturesType.Strut])
                {
                    var strut = this.Strut;
                    strut.LookAt(target);
                    strut.Rotate(new Vector3(0, 1, 0), 90f);
                    strut.localScale = new Vector3(1, 1, 1);
                    var distance = (Vector3.Distance(Vector3.zero, this.Strut.InverseTransformPoint(target))*distancePercent*this.StrutScaleFactor) + strutOffset; //*-1
                    if (this.IsFreeAttached)
                    {
                        distance += Config.Instance.FreeAttachStrutExtension;
                    }
                    this.Strut.localScale = new Vector3(distance, 1, 1);
                    this._transformLights(true, target, this.IsDocked);
                }
                else
                {
                    var localStrutOrigin = this.ModelFeatures[ModelFeaturesType.HeadExtension] ? this.StrutOrigin : this.Origin;
                    this._simpleStrut.SetActive(false);
                    var dist = Vector3.Distance(localStrutOrigin.position, target)*distancePercent/2f;
                    var trans = this._simpleStrut.transform;
                    trans.position = localStrutOrigin.position;
                    trans.LookAt(target);
                    trans.localScale = new Vector3(trans.position.x, trans.position.y, 1);
                    trans.localScale = new Vector3(0.025f, dist, 0.025f);
                    trans.Rotate(new Vector3(0, 0, 1), 90f);
                    trans.Rotate(new Vector3(1, 0, 0), 90f);
                    trans.Translate(new Vector3(0f, 1f, 0f)*dist);
                    this._simpleStrut.SetActive(true);
                }
                if (this.ModelFeatures[ModelFeaturesType.HeadExtension])
                {
                    this._headTransform.LookAt(target);
                }
                this._strutFinallyCreated = true;
            }
        }

        private void DeployHead(float speed)
        {
            this.AniExtended = true;
            this.PlayDeployAnimation(speed);
        }

        public void DestroyJoint()
        {
            if (this._flexJoint != null)
            {
                DestroyImmediate(this._flexJoint);
            }
            if (this._localFlexAnchorFixedJoint != null)
            {
                DestroyImmediate(this._localFlexAnchorFixedJoint);
            }
            if (this._localFlexAnchor != null)
            {
                DestroyImmediate(this._localFlexAnchor);
            }
            try
            {
                if (this._partJoint != null)
                {
                    this._partJoint.DestroyJoint();
                    this.part.attachNodes.Remove(this._jointAttachNode);
                    this._jointAttachNode.owner = null;
                }
                DestroyImmediate(this._partJoint);
            }
            catch (Exception)
            {
                //ahem...
            }
            DestroyImmediate(this._joint);
            this._partJoint = null;
            this._jointAttachNode = null;
            this._joint = null;
            this.LinkType = LinkType.None;
            if (this.IsDocked)
            {
                this.ProcessUnDock(true);
            }
            this._updateSimpleLights();
        }

        public void DestroyStrut()
        {
            if (this.IsFlexible)
            {
                if (this._flexStrut != null)
                {
                    this._flexStrut.SetActive(false);
                }
            }
            else
            {
                this.Strut.localScale = Vector3.zero;
                this.ShowGrappler(false, Vector3.zero, Vector3.zero, false, Vector3.zero);
                this.ShowHooks(false, Vector3.zero, Vector3.zero);
                this._transformLights(false, Vector3.zero);
            }
            this._strutFinallyCreated = false;
        }

        [KSPEvent(name = "Dock", active = false, guiName = "Dock with Target", guiActiveEditor = false, guiActiveUnfocused = true, unfocusedRange = Config.UnfocusedRange)]
        public void Dock()
        {
            if (HighLogic.LoadedSceneIsEditor || !this.IsLinked || !this.IsConnectionOrigin || this.IsTargetOnly || this.IsOwnVesselConnected || (this.IsFreeAttached ? this.FreeAttachPart == null : this.Target == null) || this.IsDocked)
            {
                OSD.PostMessage("Can't dock.");
                return;
            }
            if (this.IsFreeAttached ? this.FreeAttachPart != null && this.FreeAttachPart.vessel == this.vessel : this.Target != null && this.Target.part != null && this.Target.part.vessel == this.vessel)
            {
                OSD.PostMessage("Already docked");
                return;
            }
            this.DockingVesselName = this.vessel.GetName();
            this.DockingVesselTypeString = this.vessel.vesselType.ToString();
            this.DockingVesselId = this.vessel.rootPart.flightID;
            this.IsDocked = true;
            if (this.IsFreeAttached)
            {
                var freeAttachPart = this.FreeAttachPart;
                if (freeAttachPart != null)
                {
                    freeAttachPart.Couple(this.part);
                }
            }
            else
            {
                var moduleActiveStrut = this.Target;
                if (moduleActiveStrut != null)
                {
                    moduleActiveStrut.part.Couple(this.part);
                }
            }
            this.UpdateGui();
            foreach (var moduleActiveStrut in Utilities.GetAllActiveStruts())
            {
                moduleActiveStrut.UpdateGui();
            }
            OSD.PostMessage("Docked.");
        }

        [KSPEvent(name = "FreeAttach", active = false, guiActiveEditor = false, guiName = "FreeAttach Link", guiActiveUnfocused = true, unfocusedRange = Config.UnfocusedRange)]
        public void FreeAttach()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                InputLockManager.SetControlLock(EditorLockMask, Config.Instance.EditorInputLockId);
                var newPart = PartFactory.SpawnPartInEditor("ASTargetCube");
                Debug.Log("[AS] spawned part in editor");
                ActiveStrutsAddon.CurrentTargeter = this;
                ActiveStrutsAddon.Mode = AddonMode.FreeAttach;
                ActiveStrutsAddon.NewSpawnedPart = newPart;
            }
            this.StraightOutAttachAppliedInEditor = false;
            if (Config.Instance.ShowHelpTexts)
            {
                OSD.PostMessage(Config.Instance.FreeAttachHelpText, 5);
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                this.StartCoroutine(this.PreparePartForFreeAttach());
            }
        }

        [KSPEvent(name = "FreeAttachStraight", active = false, guiName = "Straight Up FreeAttach", guiActiveUnfocused = true, unfocusedRange = Config.UnfocusedRange)]
        public void FreeAttachStraight()
        {
            var raycast = this._performStraightOutRaycast();
            if (raycast.Item1)
            {
                var hittedPart = raycast.Item2.PartFromHit();
                var valid = hittedPart != null;
                if (valid)
                {
                    if (HighLogic.LoadedSceneIsEditor)
                    {
                        this.StraightOutAttachAppliedInEditor = true;
                        this.IsLinked = true;
                        this.IsFreeAttached = true;
                        this.UpdateGui();
                        this._straightOutAttached = true;
                        return;
                    }
                    this.StraightOutAttachAppliedInEditor = false;
                    this.IsLinked = false;
                    this.IsFreeAttached = false;
                    this._straightOutAttached = false;
                    if (HighLogic.LoadedSceneIsFlight)
                    {
                        //this.StartCoroutine(this.PreparePartForFreeAttach(true));
                        this.PlaceFreeAttach(hittedPart);
                        this._straightOutAttached = true;
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
            if (this.Mode == Mode.Unlinked && !this.IsTargetOnly)
            {
                this.FreeAttachStraight();
            }
        }

        [KSPEvent(name = "Link", active = false, guiName = "Link", guiActiveEditor = false, guiActiveUnfocused = true, unfocusedRange = Config.UnfocusedRange)]
        public void Link()
        {
            this.StraightOutAttachAppliedInEditor = false;
            if (HighLogic.LoadedSceneIsEditor)
            {
                InputLockManager.SetControlLock(EditorLockMask, Config.Instance.EditorInputLockId);
            }
            this.Mode = Mode.Targeting;
            foreach (var possibleTarget in this.GetAllPossibleTargets())
            {
                possibleTarget.SetTargetedBy(this);
                possibleTarget.UpdateGui();
            }
            ActiveStrutsAddon.Mode = AddonMode.Link;
            ActiveStrutsAddon.CurrentTargeter = this;
            if (this.IsFlexible)
            {
                ActiveStrutsAddon.FlexibleAttachActive = true;
            }
            if (Config.Instance.ShowHelpTexts)
            {
                OSD.PostMessage(Config.Instance.LinkHelpText, 5);
            }
            this.UpdateGui();
            this.DeployHead(NormalAniSpeed);
        }

        public void OnJointBreak(float breakForce)
        {
            try
            {
                this._partJoint.DestroyJoint();
                this.part.attachNodes.Remove(this._jointAttachNode);
                this._jointAttachNode.owner = null;
            }
            catch (NullReferenceException)
            {
                //already destroyed
            }
            this._jointBroken = true;
            this.PlayBreakSound();
            OSD.PostMessage("Joint broken!");
        }

        public override void OnStart(StartState state)
        {
            this._findModelFeatures();
            if (this.ModelFeatures[ModelFeaturesType.SimpleLights])
            {
                this._updateSimpleLights();
            }
            if (this.ModelFeatures[ModelFeaturesType.Animation])
            {
                if (this.AniExtended)
                {
                    this.DeployHead(FastAniSpeed);
                }
                else
                {
                    this.RetractHead(FastAniSpeed);
                }
            }
            if (!this.IsFlexible)
            {
                if (!this.IsTargetOnly)
                {
                    if (this.ModelFeatures[ModelFeaturesType.LightsBright] || this.ModelFeatures[ModelFeaturesType.LightsDull])
                    {
                        this.LightsOffset *= 0.5f;
                    }
                    if (this.ModelFeatures[ModelFeaturesType.Strut])
                    {
                        this.DestroyStrut();
                    }
                }
            }
            if (HighLogic.LoadedSceneIsEditor)
            {
                this.part.OnEditorAttach += this.ProcessOnPartCopy;
            }
            this.Origin = this.part.transform;
            this._delayedStartFlag = true;
            this._ticksForDelayedStart = HighLogic.LoadedSceneIsEditor ? 0 : Config.Instance.StartDelay;
            this._strutRealignCounter = Config.Instance.StrutRealignInterval*(HighLogic.LoadedSceneIsEditor ? 3 : 0);
            if (this.SoundAttach == null || this.SoundBreak == null || this.SoundDetach == null ||
                !GameDatabase.Instance.ExistsAudioClip(Config.Instance.SoundAttachFileUrl) ||
                !GameDatabase.Instance.ExistsAudioClip(Config.Instance.SoundDetachFileUrl) ||
                !GameDatabase.Instance.ExistsAudioClip(Config.Instance.SoundBreakFileUrl))
            {
                Debug.Log("[AS] sounds cannot be loaded." + (this.SoundAttach == null ? "FXGroup not instantiated" : "sound file not found"));
                this._soundFlag = false;
            }
            else
            {
                SetupFxGroup(this.SoundAttach, this.gameObject, Config.Instance.SoundAttachFileUrl);
                SetupFxGroup(this.SoundDetach, this.gameObject, Config.Instance.SoundDetachFileUrl);
                SetupFxGroup(this.SoundBreak, this.gameObject, Config.Instance.SoundBreakFileUrl);
                this._soundFlag = true;
            }
            this._initialized = true;
        }

        public void PlaceFreeAttach(Part targetPart)
        {
            lock (this._freeAttachStrutUpdateLock)
            {
                this._oldTargetPosition = Vector3.zero;
                ActiveStrutsAddon.Mode = AddonMode.None;
                var target = targetPart.Modules[Config.Instance.ModuleActiveStrutFreeAttachTarget] as ModuleActiveStrutFreeAttachTarget;
                if (target != null)
                {
                    this.FreeAttachTarget = target;
                    target.Targeter = this;
                    Debug.Log("[AS] connected to targetpart with ID: " + this.FreeAttachTarget.ID);
                    if (HighLogic.LoadedSceneIsFlight && target.vessel != null)
                    {
                        this.IsOwnVesselConnected = target.vessel == this.vessel;
                    }
                    else if (HighLogic.LoadedSceneIsEditor)
                    {
                        this.IsOwnVesselConnected = true;
                    }
                }
                this._freeAttachPart = targetPart;
                this.Mode = Mode.Linked;
                this.IsLinked = true;
                this.IsFreeAttached = true;
                this.IsConnectionOrigin = true;
                this.DestroyJoint();
                this.DestroyStrut();
                this.IsEnforced = Config.Instance.GlobalJointEnforcement;
                if (HighLogic.LoadedSceneIsFlight)
                {
                    this.CreateJoint(this.part.Rigidbody, this.IsFreeAttached ? targetPart.parent.Rigidbody : targetPart.Rigidbody, LinkType.Weak, targetPart.transform.position);
                }
                this.Target = null;
                this.Targeter = null;
                this.DeployHead(NormalAniSpeed);
                OSD.PostMessage("FreeAttach Link established!");
            }
            this.UpdateGui();
        }

        public void PlayAttachSound()
        {
            this.PlayAudio(this.SoundAttach);
        }

        private void PlayAudio(FXGroup group)
        {
            if (!this._soundFlag || group == null || group.audio == null)
            {
                return;
            }
            group.audio.Play();
        }

        public void PlayBreakSound()
        {
            this.PlayAudio(this.SoundBreak);
        }

        private void PlayDeployAnimation(float speed)
        {
            if (!this.ModelFeatures[ModelFeaturesType.Animation])
            {
                return;
            }
            var ani = this.DeployAnimation;
            if (ani == null)
            {
                Debug.Log("[AS] animation is null!");
                return;
            }
            if (this.IsAnimationPlaying)
            {
                ani.Stop(this.AnimationName);
            }
            if (!this.AniExtended)
            {
                speed *= -1;
            }
            if (speed < 0)
            {
                ani[this.AnimationName].time = ani[this.AnimationName].length;
            }
            ani[this.AnimationName].speed = speed;
            ani.Play(this.AnimationName);
        }

        public void PlayDetachSound()
        {
            this.PlayAudio(this.SoundDetach);
        }

        private IEnumerator PreparePartForFreeAttach(bool straightOut = false, int tryCount = 0)
        {
            const int maxWaits = 30;
            const int maxTries = 5;
            var currWaits = 0;
            var newPart = PartFactory.SpawnPartInFlight("ASTargetCube", this.part, new Vector3(2, 2, 2), this.part.transform.rotation);
            OSD.PostMessageLowerRightCorner("waiting for Unity to catch up...", 1.5f);
            while (!newPart.rigidbody && currWaits < maxWaits && newPart.vessel != null)
            {
                Debug.Log("[AS] rigidbody not ready - waiting");
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
                    newPart.transform.position = this.part.transform.position;
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
            if (newPart.vessel == null || (maxWaits == currWaits && newPart.rigidbody == null))
            {
                if (tryCount < maxTries)
                {
                    var nextTryCount = ++tryCount;
                    Debug.Log(string.Format("[AS] part spawning failed => retry (vessel is null = {0} | waits = {1}/{2})", (newPart.vessel == null), currWaits, maxWaits));
                    this.StartCoroutine(this.PreparePartForFreeAttach(straightOut, nextTryCount));
                }
                else
                {
                    Debug.Log(string.Format("[AS] part spawning failed more than {3} times => aborting FreeAttach (vessel is null = {0} | waits = {1}/{2})", (newPart.vessel == null), currWaits, maxWaits, maxTries));
                    OSD.PostMessage("FreeAttach failed because target part can not be prepared!");
                    try
                    {
                        this.AbortLink();
                    }
                    catch (NullReferenceException e)
                    {
                        Debug.Log("[AS] tried to abort link because part spawning failed, but abort throw exception: " + e.Message);
                    }
                }
                try
                {
                    newPart.Die();
                    Destroy(newPart);
                }
                catch (Exception e)
                {
                    Debug.Log("[AS] tried to destroy a part which failed to spawn properly in time, but operation throw exception: " + e.Message);
                }
                yield break;
            }
            newPart.mass = 0.000001f;
            newPart.maximum_drag = 0f;
            newPart.minimum_drag = 0f;
            if (straightOut)
            {
                this._continueWithStraightOutAttach(newPart);
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
            if (allModules != null && allModules.Any(m => m.ID == this.ID))
            {
                this.ResetActiveStrutToDefault();
            }
            else
            {
                this.Unlink();
                this.Update();
            }
        }

        private void ProcessUnDock(bool undockByUnlink = false)
        {
            if (HighLogic.LoadedSceneIsEditor || (!this.IsLinked && !undockByUnlink) || !this.IsConnectionOrigin || this.IsTargetOnly || (this.IsOwnVesselConnected && !this.IsDocked) ||
                (this.IsFreeAttached ? this.FreeAttachPart == null : this.Target == null) ||
                !this.IsDocked)
            {
                OSD.PostMessage("Can't undock");
                return;
            }
            var vi = new DockedVesselInfo
                     {
                         name = this.DockingVesselName,
                         rootPartUId = this.DockingVesselId,
                         vesselType = (VesselType) Enum.Parse(typeof(VesselType), this.DockingVesselTypeString)
                     };
            this.IsDocked = false;
            if (this.IsFreeAttached)
            {
                this.FreeAttachPart.Undock(vi);
            }
            else
            {
                this.Target.part.Undock(vi);
            }
            this.UpdateGui();
            OSD.PostMessage("Undocked");
        }

        public void ProcessUnlink(bool fromUserAction, bool secondary)
        {
            this.StraightOutAttachAppliedInEditor = false;
            this._straightOutAttached = false;
            if (this.AniExtended)
            {
                this.RetractHead(NormalAniSpeed);
            }
            if (this.IsFlexible)
            {
                if (this._flexFakeSlingLocal != null)
                {
                    DestroyImmediate(this._flexFakeSlingLocal);
                }
                if (this._flexFakeSlingTarget != null)
                {
                    DestroyImmediate(this._flexFakeSlingTarget);
                }
                if (this._flexStrut != null)
                {
                    DestroyImmediate(this._flexStrut);
                }
            }
            if (!this.IsTargetOnly && (this.Target != null || this.Targeter != null))
            {
                if (!this.IsConnectionOrigin && !secondary && this.Targeter != null)
                {
                    try
                    {
                        this.Targeter.Unlink();
                    }
                    catch (NullReferenceException)
                    {
                        //fail silently
                    }
                    return;
                }
                if (this.IsFreeAttached)
                {
                    this.IsFreeAttached = false;
                }
                this.Mode = Mode.Unlinked;
                this.IsLinked = false;
                this.DestroyJoint();
                this.DestroyStrut();
                this._oldTargetPosition = Vector3.zero;
                this.LinkType = LinkType.None;
                if (this.IsConnectionOrigin)
                {
                    if (this.Target != null)
                    {
                        try
                        {
                            this.Target.ProcessUnlink(false, true);
                            if (HighLogic.LoadedSceneIsEditor)
                            {
                                this.Target.Targeter = null;
                                this.Target = null;
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
                        this.PlayDetachSound();
                    }
                }
                this.IsConnectionOrigin = false;
                this.UpdateGui();
                return;
            }
            if (this.IsTargetOnly)
            {
                if (!this.AnyTargetersConnected())
                {
                    this.Mode = Mode.Unlinked;
                    this.IsLinked = false;
                }
                this.UpdateGui();
                return;
            }
            var targetPart = this.FreeAttachTarget;
            var destroyTarget = false;
            if (this.IsFreeAttached)
            {
                this.IsFreeAttached = false;
                destroyTarget = true;
            }
            this._oldTargetPosition = Vector3.zero;
            this.FreeAttachTarget = null;
            this.Mode = Mode.Unlinked;
            this.IsLinked = false;
            this.DestroyStrut();
            this.DestroyJoint();
            if (destroyTarget && targetPart != null)
            {
                targetPart.Die();
            }
            this.LinkType = LinkType.None;
            this.UpdateGui();
            if (!fromUserAction && HighLogic.LoadedSceneIsEditor)
            {
                OSD.PostMessage("Unlinked!");
                this.PlayDetachSound();
            }
        }

        private void Reconnect()
        {
            if (this.StraightOutAttachAppliedInEditor)
            {
                this.FreeAttachStraight();
                return;
            }
            if (this.IsFreeAttached)
            {
                if (this.FreeAttachTarget != null)
                {
                    this.PlaceFreeAttach(this.FreeAttachPart);
                    return;
                }
                this.IsFreeAttached = false;
                this.Mode = Mode.Unlinked;
                this.IsConnectionOrigin = false;
                this.LinkType = LinkType.None;
                this.UpdateGui();
                return;
            }
            var unlink = false;
            if (this.IsConnectionOrigin)
            {
                if (this.Target != null && this.IsPossibleTarget(this.Target))
                {
                    if (!this.Target.IsTargetOnly)
                    {
                        this.CreateStrut(this.Target.ModelFeatures[ModelFeaturesType.HeadExtension] ? this.Target.StrutOrigin.position : this.Target.Origin.position, 0.5f);
                    }
                    else
                    {
                        this.CreateStrut(this.Target.ModelFeatures[ModelFeaturesType.HeadExtension] ? this.Target.StrutOrigin.position : this.Target.Origin.position);
                    }
                    var type = this.IsFlexible ? LinkType.Flexible : this.Target.IsTargetOnly ? LinkType.Normal : LinkType.Maximum;
                    this.IsEnforced = Config.Instance.GlobalJointEnforcement || type == LinkType.Maximum;
                    this.CreateJoint(this.part.rigidbody, this.Target.part.parent.rigidbody, type, this.Target.transform.position);
                    this.Mode = Mode.Linked;
                    this.Target.Mode = Mode.Linked;
                    this.IsLinked = true;
                }
                else
                {
                    unlink = true;
                }
            }
            else
            {
                if (this.IsTargetOnly)
                {
                    this.Mode = Mode.Linked;
                    this.IsLinked = true;
                }
                else if (this.Targeter != null && this.IsPossibleTarget(this.Targeter))
                {
                    if (!this.IsFlexible)
                    {
                        this.CreateStrut(this.Targeter.ModelFeatures[ModelFeaturesType.HeadExtension] ? this.Targeter.StrutOrigin.position : this.Targeter.Origin.position, 0.5f);
                        this.LinkType = LinkType.Maximum;
                    }
                    else
                    {
                        this.LinkType = LinkType.Flexible;
                    }
                    this.Mode = Mode.Linked;
                    this.IsLinked = true;
                }
                else
                {
                    unlink = true;
                }
            }
            if (unlink)
            {
                this.Unlink();
            }
            this.UpdateGui();
        }

        private void ResetActiveStrutToDefault()
        {
            this.Target = null;
            this.Targeter = null;
            this.IsConnectionOrigin = false;
            this.IsFreeAttached = false;
            this.Mode = Mode.Unlinked;
            this.IsHalfWayExtended = false;
            this.Id = Guid.NewGuid().ToString();
            this.LinkType = LinkType.None;
            this.OldTargeter = null;
            this.FreeAttachTarget = null;
            this.IsFreeAttached = false;
            this.IsLinked = false;
            if (!this.IsTargetOnly)
            {
                this.DestroyJoint();
                this.DestroyStrut();
            }
        }

        private void RetractHead(float speed)
        {
            this.AniExtended = false;
            this.PlayDeployAnimation(speed);
        }

        [KSPEvent(name = "SetAsTarget", active = false, guiName = "Set as Target", guiActiveEditor = false, guiActiveUnfocused = true, unfocusedRange = Config.UnfocusedRange)]
        public void SetAsTarget()
        {
            this.IsLinked = true;
            this.part.SetHighlightDefault();
            this.Mode = Mode.Linked;
            this.IsConnectionOrigin = false;
            this.IsFreeAttached = false;
            if (!this.IsTargetOnly && !this.IsFlexible)
            {
                if (this.ModelFeatures[ModelFeaturesType.Animation])
                {
                    this.DeployHead(NormalAniSpeed);
                }
                this.CreateStrut(this.Targeter.ModelFeatures[ModelFeaturesType.HeadExtension] ? this.Targeter.StrutOrigin.position : this.Targeter.Origin.position, 0.5f);
            }
            this.Targeter.SetTarget(this);
            this.UpdateGui();
        }

        public void SetTarget(ModuleActiveStrut target)
        {
            if (this.ModelFeatures[ModelFeaturesType.Animation] && !this.AniExtended)
            {
                this.DeployHead(NormalAniSpeed);
            }
            this.Target = target;
            this.Mode = Mode.Linked;
            this.IsLinked = true;
            this.IsConnectionOrigin = true;
            var type = this.IsFlexible ? LinkType.Flexible : target.IsTargetOnly ? LinkType.Normal : LinkType.Maximum;
            this.IsEnforced = !this.IsFlexible && (Config.Instance.GlobalJointEnforcement || type == LinkType.Maximum);
            this.CreateJoint(this.part.rigidbody, target.part.parent.rigidbody, type, this.Target.transform.position);
            this.CreateStrut(target.ModelFeatures[ModelFeaturesType.HeadExtension] ? target.StrutOrigin.position : target.Origin.position, target.IsTargetOnly ? 1 : 0.5f);
            Utilities.ResetAllFromTargeting();
            OSD.PostMessage("Link established!");
            ActiveStrutsAddon.Mode = AddonMode.None;
            this.UpdateGui();
        }

        public void SetTargetedBy(ModuleActiveStrut targeter)
        {
            this.OldTargeter = this.Targeter;
            this.Targeter = targeter;
            this.Mode = Mode.Target;
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

        public void ShowGrappler(bool show, Vector3 targetPos, Vector3 lookAtPoint, bool applyOffset, Vector3 targetNormalVector, bool useNormalVector = false, bool inverseOffset = false)
        {
            if (!this.ModelFeatures[ModelFeaturesType.Grappler])
            {
                return;
            }
            if (show && !this.IsTargetOnly)
            {
                this.Grappler.localScale = new Vector3(1, 1, 1);
                this.Grappler.position = this.Origin.position;
                this.Grappler.LookAt(lookAtPoint);
                this.Grappler.position = targetPos;
                this.Grappler.Rotate(new Vector3(0, 1, 0), 90f);
                if (useNormalVector)
                {
                    this.Grappler.rotation = Quaternion.FromToRotation(this.Grappler.right, targetNormalVector)*this.Grappler.rotation;
                }
                if (applyOffset)
                {
                    var offset = inverseOffset ? -1*this.GrapplerOffset : this.GrapplerOffset;
                    this.Grappler.Translate(new Vector3(offset, 0, 0));
                }
            }
            if (!show)
            {
                this.Grappler.localScale = Vector3.zero;
            }
        }

        public void ShowHooks(bool show, Vector3 targetPos, Vector3 targetNormalVector, bool useNormalVector = false)
        {
            if (!this.ModelFeatures[ModelFeaturesType.Hooks])
            {
                return;
            }
            if (show && !this.IsTargetOnly)
            {
                this.Hooks.localScale = new Vector3(1, 1, 1)*this.HooksScaleFactor;
                this.Hooks.LookAt(targetPos);
                this.Hooks.position = targetPos;
                if (useNormalVector)
                {
                    this.Hooks.rotation = Quaternion.FromToRotation(this._featureOrientation[ModelFeaturesType.Hooks].GetAxis(this.Hooks), targetNormalVector)*this.Hooks.rotation;
                }
            }
            if (!show)
            {
                this.Hooks.localScale = Vector3.zero;
            }
        }

        [KSPEvent(name = "ToggleEnforcement", active = false, guiName = "Toggle Enforcement", guiActiveEditor = false)]
        public void ToggleEnforcement()
        {
            if (!this.IsLinked || !this.IsConnectionOrigin)
            {
                return;
            }
            this.IsEnforced = !this.IsEnforced;
            this.DestroyJoint();
            if (!this.IsFreeAttached)
            {
                this.CreateJoint(this.part.rigidbody, this.Target.part.parent.rigidbody, this.LinkType, this.Target.transform.position);
            }
            else
            {
                var rayRes = Utilities.PerformRaycast(this.Origin.position, this.FreeAttachTarget.PartOrigin.position, this.RealModelForward);
                if (rayRes.HittedPart != null && rayRes.DistanceFromOrigin <= Config.Instance.MaxDistance)
                {
                    var moduleActiveStrutFreeAttachTarget = rayRes.HittedPart.Modules[Config.Instance.ModuleActiveStrutFreeAttachTarget] as ModuleActiveStrutFreeAttachTarget;
                    if (moduleActiveStrutFreeAttachTarget != null)
                    {
                        this.CreateJoint(this.part.rigidbody, moduleActiveStrutFreeAttachTarget.PartRigidbody, LinkType.Weak, (rayRes.Hit.point + this.Origin.position)/2);
                    }
                }
            }
            OSD.PostMessage("Joint enforcement temporarily changed.");
            this.UpdateGui();
        }

        [KSPEvent(name = "ToggleLink", active = false, guiName = "Toggle Link", guiActiveUnfocused = true, unfocusedRange = Config.UnfocusedRange)]
        public void ToggleLink()
        {
            if (this.Mode == Mode.Linked)
            {
                if (this.IsConnectionOrigin)
                {
                    this.Unlink();
                }
                else
                {
                    if (this.Targeter != null)
                    {
                        this.Targeter.Unlink();
                    }
                }
            }
            else if (this.Mode == Mode.Unlinked && ((this.Target != null && this.Target.IsConnectionFree) || (this.Targeter != null && this.Targeter.IsConnectionFree)))
            {
                if (this.Target != null)
                {
                    if (this.IsPossibleTarget(this.Target))
                    {
                        this.Target.Targeter = this;
                        this.Target.SetAsTarget();
                    }
                    else
                    {
                        OSD.PostMessage("Can't relink at the moment, target may be obstructed.");
                    }
                }
                else if (this.Targeter != null)
                {
                    if (this.Targeter.IsPossibleTarget(this))
                    {
                        this.SetAsTarget();
                    }
                    else
                    {
                        OSD.PostMessage("Can't relink at the moment, targeter may be obstructed.");
                    }
                }
            }
            this.UpdateGui();
        }

        [KSPAction("ToggleLinkAction", KSPActionGroup.None, guiName = "Toggle Link")]
        public void ToggleLinkAction(KSPActionParam param)
        {
            if (this.Mode == Mode.Linked || (this.Mode == Mode.Unlinked && ((this.Target != null && this.Target.IsConnectionFree) || (this.Targeter != null && this.Targeter.IsConnectionFree))))
            {
                this.ToggleLink();
            }
        }

        [KSPEvent(name = "UnDock", active = false, guiName = "Undock from Target", guiActiveEditor = false, guiActiveUnfocused = true, unfocusedRange = Config.UnfocusedRange)]
        public void UnDock()
        {
            this.ProcessUnDock();
        }

        [KSPEvent(name = "Unlink", active = false, guiName = "Unlink", guiActiveEditor = false, guiActiveUnfocused = true, unfocusedRange = Config.UnfocusedRange)]
        public void Unlink()
        {
            this.ProcessUnlink(true, false);
        }

        public void Update()
        {
            if (!this._initialized)
            {
                return;
            }
            if (this._delayedStartFlag)
            {
                this._delayedStart();
                return;
            }
            if (this._jointBroken)
            {
                this._jointBroken = false;
                this.Unlink();
                return;
            }
            if (this.IsLinked)
            {
                if (this._strutRealignCounter > 0 && !this.IsFlexible && this._strutFinallyCreated && this.ModelFeatures[ModelFeaturesType.Strut])
                {
                    this._strutRealignCounter--;
                }
                else
                {
                    this._strutRealignCounter = Config.Instance.StrutRealignInterval;
                    this._updateSimpleLights();
                    this._realignStrut();
                    if (this.IsFreeAttached)
                    {
                        this.LinkType = LinkType.Weak;
                    }
                    else if (this.IsConnectionOrigin)
                    {
                        if (this.Target != null)
                        {
                            this.LinkType = this.IsFlexible ? LinkType.Flexible : this.Target.IsTargetOnly ? LinkType.Normal : LinkType.Maximum;
                        }
                    }
                    else
                    {
                        if (this.Targeter != null)
                        {
                            this.LinkType = this.IsFlexible ? LinkType.Flexible : this.IsTargetOnly ? LinkType.Normal : LinkType.Maximum;
                        }
                    }
                }
            }
            else
            {
                this.LinkType = LinkType.None;
            }
            if (this.Mode == Mode.Unlinked || this.Mode == Mode.Target || this.Mode == Mode.Targeting)
            {
                if (this.IsTargetOnly)
                {
                    this._showTargetGrappler(false);
                }
                return;
            }
            if (this.IsTargetOnly)
            {
                if (!this.AnyTargetersConnected())
                {
                    this._showTargetGrappler(false);
                    this.Mode = Mode.Unlinked;
                    this.UpdateGui();
                    return;
                }
                this._showTargetGrappler(true);
            }
            if (this.Mode == Mode.Linked)
            {
                if (HighLogic.LoadedSceneIsEditor)
                {
                    return;
                }
                if (this.IsOwnVesselConnected)
                {
                    if (this.IsFreeAttached)
                    {
                        if (this.FreeAttachPart != null)
                        {
                            if (this.FreeAttachPart.vessel != this.vessel)
                            {
                                this.IsOwnVesselConnected = false;
                            }
                        }
                    }
                    else if (this.IsTargetOnly)
                    {
                        foreach (var connectedTargeter in this.GetAllConnectedTargeters().Where(connectedTargeter => connectedTargeter.vessel != null && connectedTargeter.vessel != this.vessel))
                        {
                            connectedTargeter.Unlink();
                        }
                    }
                    else if (this.Target != null)
                    {
                        if (this.Target.vessel != this.vessel)
                        {
                            this.IsOwnVesselConnected = false;
                        }
                    }
                    if (!this.IsOwnVesselConnected)
                    {
                        this.Unlink();
                    }
                    this.UpdateGui();
                }
            }
        }

        public void UpdateGui()
        {
            this.Events["ToggleEnforcement"].active = this.Events["ToggleEnforcement"].guiActive = false;
            if (HighLogic.LoadedSceneIsEditor || this.IsFlexible || this.IsTargetOnly || !this.IsConnectionOrigin || !this.IsLinked || Config.Instance.GlobalJointWeakness)
            {
                this.Fields["IsEnforced"].guiActive = false;
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (this.vessel != null && this.vessel.isEVA)
                {
                    this.Events["UnDock"].active = this.Events["UnDock"].guiActive = false;
                    this.Events["Dock"].active = this.Events["UnDock"].guiActive = false;
                    this.Events["Link"].active = this.Events["Link"].guiActive = false;
                    this.Events["AbortLink"].active = this.Events["AbortLink"].guiActive = false;
                    this.Events["ToggleLink"].active = this.Events["ToggleLink"].guiActive = false;
                    this.Events["FreeAttach"].active = this.Events["FreeAttach"].guiActive = false;
                    this.Events["SetAsTarget"].active = this.Events["SetAsTarget"].guiActive = false;
                    this.Events["FreeAttachStraight"].active = this.Events["FreeAttachStraight"].guiActive = false;
                    return;
                }
                switch (this.Mode)
                {
                    case Mode.Linked:
                    {
                        this.Events["Link"].active = this.Events["Link"].guiActive = false;
                        this.Events["SetAsTarget"].active = this.Events["SetAsTarget"].guiActive = false;
                        this.Events["FreeAttach"].active = this.Events["FreeAttach"].guiActive = false;
                        if (!this.IsTargetOnly)
                        {
                            this.Events["AbortLink"].active = this.Events["AbortLink"].guiActive = false;
                            if (!this.IsFlexible && this.IsConnectionOrigin && !Config.Instance.GlobalJointWeakness)
                            {
                                this.Events["ToggleEnforcement"].active = this.Events["ToggleEnforcement"].guiActive = true;
                            }
                            if (!this.IsFlexible)
                            {
                                this.Fields["IsEnforced"].guiActive = true;
                            }
                            if (this.IsFreeAttached)
                            {
                                this.Events["ToggleLink"].active = this.Events["ToggleLink"].guiActive = false;
                                this.Events["Unlink"].active = this.Events["Unlink"].guiActive = true;
                            }
                            else
                            {
                                this.Events["ToggleLink"].active = this.Events["ToggleLink"].guiActive = true;
                                this.Events["Unlink"].active = this.Events["Unlink"].guiActive = false;
                            }
                            if (!this.IsOwnVesselConnected && !this.IsDocked)
                            {
                                if (Config.Instance.DockingEnabled &&
                                    !(this.IsFreeAttached ? this.FreeAttachPart != null && this.FreeAttachPart.vessel == this.vessel : this.Target != null && this.Target.part != null && this.Target.part.vessel == this.vessel))
                                {
                                    this.Events["Dock"].active = this.Events["Dock"].guiActive = true;
                                }
                                this.Events["UnDock"].active = this.Events["UnDock"].guiActive = false;
                            }
                            if (!this.IsOwnVesselConnected && this.IsDocked)
                            {
                                this.Events["Dock"].active = this.Events["Dock"].guiActive = false;
                                this.Events["UnDock"].active = this.Events["UnDock"].guiActive = true;
                            }
                        }
                        else
                        {
                            this.Events["Unlink"].active = this.Events["Unlink"].guiActive = false;
                            this.Events["ToggleLink"].active = this.Events["ToggleLink"].guiActive = false;
                        }
                    }
                        break;
                    case Mode.Unlinked:
                    {
                        this.Events["SetAsTarget"].active = this.Events["SetAsTarget"].guiActive = false;
                        this.Events["Unlink"].active = this.Events["Unlink"].guiActive = false;
                        this.Events["UnDock"].active = this.Events["UnDock"].guiActive = false;
                        this.Events["Dock"].active = this.Events["Dock"].guiActive = false;
                        if (this.IsTargetOnly)
                        {
                            this.Events["Link"].active = this.Events["Link"].guiActive = false;
                        }
                        else
                        {
                            this.Events["Link"].active = this.Events["Link"].guiActive = true;
                            if (!this.IsFlexible)
                            {
                                this.Events["FreeAttach"].active = this.Events["FreeAttach"].guiActive = true;
                            }
                            else
                            {
                                this.Events["FreeAttach"].active = this.Events["FreeAttach"].guiActive = false;
                            }
                            this.Events["AbortLink"].active = this.Events["AbortLink"].guiActive = false;
                            if ((this.Target != null && this.Target.IsConnectionFree) || (this.Targeter != null && this.Targeter.IsConnectionFree))
                            {
                                this.Events["ToggleLink"].active = this.Events["ToggleLink"].guiActive = true;
                            }
                            else
                            {
                                this.Events["ToggleLink"].active = this.Events["ToggleLink"].guiActive = false;
                            }
                        }
                    }
                        break;
                    case Mode.Target:
                    {
                        this.Events["UnDock"].active = this.Events["UnDock"].guiActive = false;
                        this.Events["Dock"].active = this.Events["Dock"].guiActive = false;
                        this.Events["SetAsTarget"].active = this.Events["SetAsTarget"].guiActive = true;
                        if (!this.IsTargetOnly)
                        {
                            this.Events["Link"].active = this.Events["Link"].guiActive = false;
                        }
                        this.Events["ToggleLink"].active = this.Events["ToggleLink"].guiActive = false;
                        this.Events["FreeAttach"].active = this.Events["FreeAttach"].guiActive = false;
                    }
                        break;
                    case Mode.Targeting:
                    {
                        this.Events["UnDock"].active = this.Events["UnDock"].guiActive = false;
                        this.Events["Dock"].active = this.Events["Dock"].guiActive = false;
                        this.Events["Link"].active = this.Events["Link"].guiActive = false;
                        this.Events["AbortLink"].active = this.Events["AbortLink"].guiActive = true;
                        this.Events["ToggleLink"].active = this.Events["ToggleLink"].guiActive = false;
                        this.Events["FreeAttach"].active = this.Events["FreeAttach"].guiActive = false;
                    }
                        break;
                }
                this.Events["FreeAttachStraight"].active = this.Events["FreeAttachStraight"].guiActive = this.Events["FreeAttach"].active;
            }
            else if (HighLogic.LoadedSceneIsEditor)
            {
                this.Events["ToggleLink"].active = this.Events["ToggleLink"].guiActive = this.Events["ToggleLink"].guiActiveEditor = false;
                this.Events["UnDock"].active = this.Events["UnDock"].guiActive = this.Events["UnDock"].guiActiveEditor = false;
                this.Events["Dock"].active = this.Events["Dock"].guiActive = this.Events["Dock"].guiActiveEditor = false;
                switch (this.Mode)
                {
                    case Mode.Linked:
                    {
                        if (!this.IsTargetOnly)
                        {
                            this.Events["Unlink"].active = this.Events["Unlink"].guiActive = this.Events["Unlink"].guiActiveEditor = true;
                        }
                        this.Events["Link"].active = this.Events["Link"].guiActive = this.Events["Link"].guiActiveEditor = false;
                        this.Events["SetAsTarget"].active = this.Events["SetAsTarget"].guiActive = this.Events["SetAsTarget"].guiActiveEditor = false;
                        this.Events["AbortLink"].active = this.Events["AbortLink"].guiActive = this.Events["AbortLink"].guiActiveEditor = false;
                        this.Events["FreeAttach"].active = this.Events["FreeAttach"].guiActive = this.Events["FreeAttach"].guiActiveEditor = false;
                    }
                        break;
                    case Mode.Unlinked:
                    {
                        this.Events["Unlink"].active = this.Events["Unlink"].guiActive = this.Events["Unlink"].guiActiveEditor = false;
                        this.Events["SetAsTarget"].active = this.Events["SetAsTarget"].guiActive = this.Events["SetAsTarget"].guiActiveEditor = false;
                        this.Events["AbortLink"].active = this.Events["AbortLink"].guiActive = this.Events["AbortLink"].guiActiveEditor = false;
                        if (!this.IsTargetOnly)
                        {
                            this.Events["Link"].active = this.Events["Link"].guiActive = this.Events["Link"].guiActiveEditor = true;
                            if (!this.IsFlexible)
                            {
                                this.Events["FreeAttach"].active = this.Events["FreeAttach"].guiActive = this.Events["FreeAttach"].guiActiveEditor = true;
                            }
                            else
                            {
                                this.Events["FreeAttach"].active = this.Events["FreeAttach"].guiActive = this.Events["FreeAttach"].guiActiveEditor = false;
                            }
                        }
                    }
                        break;
                    case Mode.Target:
                    {
                        this.Events["Unlink"].active = this.Events["Unlink"].guiActive = this.Events["Unlink"].guiActiveEditor = false;
                        this.Events["Link"].active = this.Events["Link"].guiActive = this.Events["Link"].guiActiveEditor = false;
                        this.Events["SetAsTarget"].active = this.Events["SetAsTarget"].guiActive = this.Events["SetAsTarget"].guiActiveEditor = true;
                        this.Events["AbortLink"].active = this.Events["AbortLink"].guiActive = this.Events["AbortLink"].guiActiveEditor = false;
                        this.Events["FreeAttach"].active = this.Events["FreeAttach"].guiActive = this.Events["FreeAttach"].guiActiveEditor = false;
                    }
                        break;
                    case Mode.Targeting:
                    {
                        this.Events["Unlink"].active = this.Events["Unlink"].guiActive = this.Events["Unlink"].guiActiveEditor = false;
                        this.Events["Link"].active = this.Events["Link"].guiActive = this.Events["Link"].guiActiveEditor = false;
                        this.Events["SetAsTarget"].active = this.Events["SetAsTarget"].guiActive = this.Events["SetAsTarget"].guiActiveEditor = false;
                        this.Events["AbortLink"].active = this.Events["AbortLink"].guiActive = this.Events["AbortLink"].guiActiveEditor = true;
                        this.Events["FreeAttach"].active = this.Events["FreeAttach"].guiActive = this.Events["FreeAttach"].guiActiveEditor = false;
                    }
                        break;
                }
                this.Events["FreeAttachStraight"].active = this.Events["FreeAttachStraight"].guiActive = this.Events["FreeAttachStraight"].guiActiveEditor = this.Events["FreeAttach"].active;
                if (!Config.Instance.AllowFreeAttachInEditor)
                {
                    this.Events["FreeAttach"].guiActiveEditor = false;
                }
            }
        }

        private IEnumerator WaitAndCreateFlexibleJoint()
        {
            this._localFlexAnchor = Utilities.CreateLocalAnchor("ActiveFlexJoint", true);
            this._localFlexAnchor.transform.position = this.Target.FlexOffsetOriginPosition;
            this._flexStrut = Utilities.CreateFlexStrut("ActiveFlexJointStrut", false, Color.black);
            this._flexFakeSlingLocal = Utilities.CreateFakeRopeSling("ActiveFlexJointStrutSling", false, Color.black);
            this._flexFakeSlingTarget = Utilities.CreateFakeRopeSling("ActiveFlexJointStrutSling", false, Color.black);
            yield return new WaitForSeconds(0.1f);
            yield return new WaitForFixedUpdate();
            this._localFlexAnchor.transform.position = this.Target.FlexOffsetOriginPosition;
            var distance = Vector3.Distance(this.FlexOffsetOriginPosition, this.Target.FlexOffsetOriginPosition);
            this._flexJoint = this._localFlexAnchor.AddComponent<SpringJoint>();
            this._flexJoint.spring = this.FlexibleStrutSpring;
            this._flexJoint.damper = this.FlexibleStrutDamper;
            this._flexJoint.anchor = this._localFlexAnchor.transform.position;
            this._flexJoint.connectedBody = this.Target.part.parent.rigidbody;
            this._flexJoint.maxDistance = distance + 0.25f;
            this._flexJoint.breakForce = Mathf.Infinity;
            this._flexJoint.breakTorque = Mathf.Infinity;
            yield return new WaitForSeconds(0.1f);
            yield return new WaitForFixedUpdate();
            this._localFlexAnchor.transform.position = this.FlexOffsetOriginPosition;
            yield return new WaitForFixedUpdate();
            this._localFlexAnchorFixedJoint = this._localFlexAnchor.AddComponent<FixedJoint>();
            this._localFlexAnchorFixedJoint.connectedBody = this.part.rigidbody;
            this._localFlexAnchorFixedJoint.breakForce = this._localFlexAnchorFixedJoint.breakTorque = Mathf.Infinity;
        }

        private void _continueWithStraightOutAttach(Part newPart)
        {
            var rayres = this._performStraightOutRaycast();
            if (rayres.Item1)
            {
                ActiveStrutsAddon.NewSpawnedPart = newPart;
                ActiveStrutsAddon.CurrentTargeter = this;
                this.StartCoroutine(ActiveStrutsAddon.PlaceNewPart(rayres.Item2.PartFromHit(), rayres.Item2));
                return;
            }
            OSD.PostMessage("Straight Out Attach failed!");
            Debug.Log("[AS] straight out raycast didn't hit anything after part creation");
            DestroyImmediate(newPart);
        }

        private void _delayedStart()
        {
            if (this._ticksForDelayedStart > 0)
            {
                this._ticksForDelayedStart--;
                return;
            }
            this._delayedStartFlag = false;
            if (this.Id == Guid.Empty.ToString())
            {
                this.Id = Guid.NewGuid().ToString();
            }
            if (HighLogic.LoadedSceneIsFlight && !this.IdResetDone)
            {
                ActiveStrutsAddon.Enqueue(this);
            }
            if (this.IsLinked)
            {
                if (this.IsTargetOnly)
                {
                    this.Mode = Mode.Linked;
                }
                else
                {
                    this.Reconnect();
                }
            }
            else
            {
                this.Mode = Mode.Unlinked;
            }
            this.Events.Sort((l, r) =>
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
            this.UpdateGui();
        }

        private void _findModelFeatures()
        {
            this.ModelFeatures = new Dictionary<ModelFeaturesType, bool>();
            this._featureOrientation = new Dictionary<ModelFeaturesType, OrientationInfo>();
            if (!string.IsNullOrEmpty(this.GrapplerName))
            {
                this.Grappler = this.part.FindModelTransform(this.GrapplerName);
                this.ModelFeatures.Add(ModelFeaturesType.Grappler, true);
            }
            else
            {
                this.ModelFeatures.Add(ModelFeaturesType.Grappler, false);
            }
            if (!string.IsNullOrEmpty(this.StrutName))
            {
                this.Strut = this.part.FindModelTransform(this.StrutName);
                this.ModelFeatures.Add(ModelFeaturesType.Strut, true);
                DestroyImmediate(this.Strut.collider);
            }
            else
            {
                if (!this.IsTargetOnly)
                {
                    this._simpleStrut = Utilities.CreateSimpleStrut("Targeterstrut");
                    this._simpleStrut.SetActive(true);
                    this._simpleStrut.transform.localScale = Vector3.zero;
                    this.Strut = this._simpleStrut.transform;
                }
                this.ModelFeatures.Add(ModelFeaturesType.Strut, false);
            }
            if (!string.IsNullOrEmpty(this.HooksName))
            {
                this.Hooks = this.part.FindModelTransform(this.HooksName);
                this.ModelFeatures.Add(ModelFeaturesType.Hooks, true);
                this._featureOrientation.Add(ModelFeaturesType.Hooks, new OrientationInfo(this.HooksForward));
                DestroyImmediate(this.Hooks.collider);
            }
            else
            {
                this.ModelFeatures.Add(ModelFeaturesType.Hooks, false);
            }
            if (!string.IsNullOrEmpty(this.LightsBrightName))
            {
                this.LightsBright = this.part.FindModelTransform(this.LightsBrightName);
                this.ModelFeatures.Add(ModelFeaturesType.LightsBright, true);
                DestroyImmediate(this.LightsBright.collider);
            }
            else
            {
                this.ModelFeatures.Add(ModelFeaturesType.LightsBright, false);
            }
            if (!string.IsNullOrEmpty(this.LightsDullName))
            {
                this.LightsDull = this.part.FindModelTransform(this.LightsDullName);
                this.ModelFeatures.Add(ModelFeaturesType.LightsDull, true);
                DestroyImmediate(this.LightsDull.collider);
            }
            else
            {
                this.ModelFeatures.Add(ModelFeaturesType.LightsDull, false);
            }
            if (!string.IsNullOrEmpty(this.HeadName))
            {
                var head = this.part.FindModelTransform(this.HeadName);
                this.StrutOrigin = head.transform;
                this._headTransform = head;
                this.ModelFeatures.Add(ModelFeaturesType.HeadExtension, true);
            }
            else
            {
                this.ModelFeatures.Add(ModelFeaturesType.HeadExtension, false);
            }
            if (!string.IsNullOrEmpty(this.SimpleLightsName))
            {
                this._simpleLights = this.part.FindModelTransform(this.SimpleLightsName);
                this._simpleLightsSecondary = this.part.FindModelTransform(this.SimpleLightsSecondaryName);
                this._featureOrientation.Add(ModelFeaturesType.SimpleLights, new OrientationInfo(this.SimpleLightsForward));
                this.ModelFeatures.Add(ModelFeaturesType.SimpleLights, true);
            }
            else
            {
                this.ModelFeatures.Add(ModelFeaturesType.SimpleLights, false);
            }
            if (!string.IsNullOrEmpty(this.AnimationName))
            {
                this.ModelFeatures.Add(ModelFeaturesType.Animation, true);
            }
            else
            {
                this.ModelFeatures.Add(ModelFeaturesType.Animation, false);
            }
        }

        private void _manageAttachNode(float breakForce)
        {
            if (!this.IsConnectionOrigin || this.IsTargetOnly || this._jointAttachNode != null || !HighLogic.LoadedSceneIsFlight)
            {
                return;
            }
            try
            {
                var targetPart = this.IsFreeAttached ? this.FreeAttachPart : this.Target.part;
                if (targetPart == null)
                {
                    return;
                }
                var freeAttachedHitPoint = this.IsFreeAttached ? this.FreeAttachPart.transform.position : Vector3.zero;
                var normDir = (this.Origin.position - (this.IsFreeAttached ? this.FreeAttachPart.transform.position : this.Target.Origin.position)).normalized;
                this._jointAttachNode = new AttachNode {id = Guid.NewGuid().ToString(), attachedPart = targetPart};
                this._jointAttachNode.breakingForce = this._jointAttachNode.breakingTorque = breakForce;
                this._jointAttachNode.position = targetPart.partTransform.InverseTransformPoint(this.IsFreeAttached ? freeAttachedHitPoint : targetPart.partTransform.position);
                this._jointAttachNode.orientation = targetPart.partTransform.InverseTransformDirection(normDir);
                this._jointAttachNode.size = 1;
                this._jointAttachNode.ResourceXFeed = false;
                this._jointAttachNode.attachMethod = AttachNodeMethod.FIXED_JOINT;
                this.part.attachNodes.Add(this._jointAttachNode);
                this._jointAttachNode.owner = this.part;
                this._partJoint = PartJoint.Create(this.part, this.IsFreeAttached ? (targetPart.parent ?? targetPart) : targetPart, this._jointAttachNode, null, AttachModes.SRF_ATTACH);
            }
            catch (Exception e)
            {
                this._jointAttachNode = null;
                Debug.Log("[AS] failed to create attachjoint: " + e.Message + " " + e.StackTrace);
            }
        }

        private void _moveFakeRopeSling(bool local, GameObject sling)
        {
            sling.SetActive(false);
            var trans = sling.transform;
            trans.rotation = local ? this.Origin.rotation : this.Target.Origin.rotation;
            trans.Rotate(new Vector3(0, 0, 1), 90f);
            trans.Rotate(new Vector3(1, 0, 0), 90f);
            trans.LookAt(local ? this.Target.FlexOffsetOriginPosition : this.FlexOffsetOriginPosition);
            var dir = this.Target.FlexOffsetOriginPosition - this.FlexOffsetOriginPosition;
            if (!local)
            {
                dir *= -1;
            }
            trans.position = (local ? this.FlexOffsetOriginPosition : this.Target.FlexOffsetOriginPosition) + (dir.normalized*this.FlexibleStrutSlingOffset);
            sling.SetActive(true);
        }

        private Tuple<bool, RaycastHit> _performStraightOutRaycast()
        {
            var rayRes = Utilities.PerformRaycastIntoDir(this.Origin.position, this.RealModelForward, this.RealModelForward, this.part);
            return new Tuple<bool, RaycastHit>(rayRes.HitResult, rayRes.Hit);
        }

        private void _realignStrut()
        {
            if (this.IsFreeAttached)
            {
                lock (this._freeAttachStrutUpdateLock)
                {
                    Vector3[] targetPos;
                    if (this.StraightOutAttachAppliedInEditor || this._straightOutAttached)
                    {
                        var raycast = this._performStraightOutRaycast();
                        if (!raycast.Item1)
                        {
                            this.DestroyStrut();
                            this.IsLinked = false;
                            this.IsFreeAttached = false;
                            return;
                        }
                        targetPos = new[] {raycast.Item2.point, raycast.Item2.normal};
                    }
                    else
                    {
                        var raycast = Utilities.PerformRaycast(this.Origin.position, this.FreeAttachPart.transform.position, this.Origin.up, new[] {this.FreeAttachPart, this.part});
                        targetPos = new[] {this.FreeAttachPart.transform.position, raycast.Hit.normal};
                    }
                    if (this._strutFinallyCreated && this.ModelFeatures[ModelFeaturesType.Strut] && !this.IsFlexible && (Vector3.Distance(targetPos[0], this._oldTargetPosition) <= Config.Instance.StrutRealignDistanceTolerance))
                    {
                        return;
                    }
                    this._oldTargetPosition = targetPos[0];
                    this.DestroyStrut();
                    this.CreateStrut(targetPos[0]);
                    this.ShowGrappler(true, targetPos[0], targetPos[0], false, targetPos[1], true);
                    this.ShowHooks(true, targetPos[0], targetPos[1], true);
                }
            }
            else if (!this.IsTargetOnly)
            {
                if (this.Target == null || !this.IsConnectionOrigin)
                {
                    return;
                }
                var refPos = this.IsFlexible ? this.Target.FlexOffsetOriginPosition : this.Target.ModelFeatures[ModelFeaturesType.HeadExtension] ? this.Target.StrutOrigin.position : this.Target.Origin.position;
                if ((this._strutFinallyCreated && this.ModelFeatures[ModelFeaturesType.Strut] && !this.IsFlexible && Vector3.Distance(refPos, this._oldTargetPosition) <= Config.Instance.StrutRealignDistanceTolerance))
                {
                    return;
                }
                this._oldTargetPosition = refPos;
                this.DestroyStrut();
                if (this.IsFlexible)
                {
                    this.CreateStrut(refPos);
                }
                else if (this.Target.IsTargetOnly)
                {
                    this.CreateStrut(this.Target.ModelFeatures[ModelFeaturesType.HeadExtension] ? this.Target.StrutOrigin.position : this.Target.Origin.position);
                    this.ShowGrappler(false, Vector3.zero, Vector3.zero, false, Vector3.zero);
                }
                else
                {
                    var targetStrutPos = this.Target.ModelFeatures[ModelFeaturesType.HeadExtension] ? this.Target.StrutOrigin.position : this.Target.Origin.position;
                    var localStrutPos = this.ModelFeatures[ModelFeaturesType.HeadExtension] ? this.StrutOrigin.position : this.Origin.position;
                    this.Target.DestroyStrut();
                    this.CreateStrut(targetStrutPos, 0.5f, -1*this.GrapplerOffset);
                    this.Target.CreateStrut(localStrutPos, 0.5f, -1*this.GrapplerOffset);
                    this.ShowHooks(false, Vector3.zero, Vector3.zero);
                    this.Target.ShowHooks(false, Vector3.zero, Vector3.zero);
                    var grapplerTargetPos = ((targetStrutPos - localStrutPos)*0.5f) + localStrutPos;
                    this.ShowGrappler(true, grapplerTargetPos, targetStrutPos, true, Vector3.zero);
                    this.Target.ShowGrappler(true, grapplerTargetPos, localStrutPos, true, Vector3.zero);
                }
            }
        }

        private void _showTargetGrappler(bool show)
        {
            if (!this.IsTargetOnly || !this.ModelFeatures[ModelFeaturesType.Grappler])
            {
                return;
            }
            if (show && !this._targetGrapplerVisible)
            {
                this.Grappler.Translate(new Vector3(-this.GrapplerOffset, 0, 0));
                this._targetGrapplerVisible = true;
            }
            else if (!show && this._targetGrapplerVisible)
            {
                this.Grappler.Translate(new Vector3(this.GrapplerOffset, 0, 0));
                this._targetGrapplerVisible = false;
            }
        }

        private void _transformLights(bool show, Vector3 lookAtTarget, bool bright = false)
        {
            if (!(this.ModelFeatures[ModelFeaturesType.LightsBright] && this.ModelFeatures[ModelFeaturesType.LightsDull]))
            {
                return;
            }
            if (!show)
            {
                this.LightsBright.localScale = Vector3.zero;
                this.LightsDull.localScale = Vector3.zero;
                if (this._dullLightsExtended)
                {
                    this.LightsDull.Translate(new Vector3(this.LightsOffset, 0, 0));
                    this._dullLightsExtended = false;
                }
                if (this._brightLightsExtended)
                {
                    this.LightsBright.Translate(new Vector3(this.LightsOffset, 0, 0));
                    this._brightLightsExtended = false;
                }
                return;
            }
            if (bright)
            {
                this.LightsDull.localScale = Vector3.zero;
                this.LightsBright.LookAt(lookAtTarget);
                this.LightsBright.Rotate(new Vector3(0, 1, 0), 90f);
                this.LightsBright.localScale = new Vector3(1, 1, 1);
                if (!this._brightLightsExtended)
                {
                    this.LightsBright.Translate(new Vector3(-this.LightsOffset, 0, 0));
                }
                if (this._dullLightsExtended)
                {
                    this.LightsDull.Translate(new Vector3(this.LightsOffset, 0, 0));
                }
                this._dullLightsExtended = false;
                this._brightLightsExtended = true;
                return;
            }
            this.LightsBright.localScale = Vector3.zero;
            this.LightsDull.LookAt(lookAtTarget);
            this.LightsDull.Rotate(new Vector3(0, 1, 0), 90f);
            this.LightsDull.position = this.Origin.position;
            this.LightsDull.localScale = new Vector3(1, 1, 1);
            if (!this._dullLightsExtended)
            {
                this.LightsDull.Translate(new Vector3(-this.LightsOffset, 0, 0));
            }
            if (this._brightLightsExtended)
            {
                this.LightsBright.Translate(new Vector3(this.LightsOffset, 0, 0));
            }
            this._dullLightsExtended = true;
            this._brightLightsExtended = false;
        }

        private void _updateSimpleLights()
        {
            try
            {
                if (!this.ModelFeatures[ModelFeaturesType.SimpleLights])
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
            if (this.IsLinked)
            {
                if (this.IsDocked)
                {
                    col = Utilities._setColorForEmissive(Color.blue);
                }
                else
                {
                    col = Utilities._setColorForEmissive(Color.green);
                }
            }
            else
            {
                col = Utilities._setColorForEmissive(Color.yellow);
            }
            foreach (var m in new[] {this._simpleLights, this._simpleLightsSecondary}.Select(lightTransform => lightTransform.renderer.material))
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
            private bool Invert { get; set; }
            private Orientations Orientation { get; set; }

            internal OrientationInfo(string stringToParse)
            {
                if (string.IsNullOrEmpty(stringToParse))
                {
                    this.Orientation = Orientations.Up;
                    this.Invert = false;
                    return;
                }
                var substrings = stringToParse.Split(',').Select(s => s.Trim().ToUpperInvariant()).ToList();
                if (substrings.Count == 2)
                {
                    var oS = substrings[0];
                    if (oS == "RIGHT")
                    {
                        this.Orientation = Orientations.Right;
                    }
                    else if (oS == "FORWARD")
                    {
                        this.Orientation = Orientations.Forward;
                    }
                    else
                    {
                        this.Orientation = Orientations.Up;
                    }
                    bool outBool;
                    bool.TryParse(substrings[1], out outBool);
                    this.Invert = outBool;
                }
            }

            internal OrientationInfo(Orientations orientation, bool invert)
            {
                this.Orientation = orientation;
                this.Invert = invert;
            }

            internal Vector3 GetAxis(Transform transform)
            {
                var axis = Vector3.zero;
                if (transform == null)
                {
                    return axis;
                }
                switch (this.Orientation)
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
                axis = this.Invert ? axis*-1f : axis;
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