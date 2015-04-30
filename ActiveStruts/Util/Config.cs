using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ActiveStruts.Util
{
    public class Config
    {
        private const string FREE_ATTACH_HELP_TEXT =
            "Left-Click on a valid position to establish a link. Press 'x' to abort.";

        private const string LINK_HELP_TEXT =
            "Left-Click on a possible target to establish a link. Press 'x' to abort or use the 'Abort Link' button.";

        private const string MODULE_NAME = "ModuleActiveStrut";
        private const string EDITOR_INPUT_LOCK_ID = "[IRAS] editor lock";
        public const float UNFOCUSED_RANGE = 3f;
        private const string MODULE_ACTIVE_STRUT_FREE_ATTACH_TARGET = "ModuleActiveStrutFreeAttachTarget";
        private const string MODULE_KERBAL_HOOK = "ModuleKerbalHook";
        private const string CONFIG_FILE_PATH = "GameData/MagicSmokeIndustries/IRActiveStruts/Plugins/ActiveStruts.cfg";

        private const string SETTINGS_NODE_NAME = "ACTIVE_STRUTS_SETTINGS";
        public const int ID_RESET_CHECK_INTERVAL = 120;

        private static readonly Dictionary<string, SettingsEntry> values = new Dictionary<string, SettingsEntry>
        {
            {"MaxDistance", new SettingsEntry(15f)},
            {"MaxAngle", new SettingsEntry(95f)},
            {"WeakJointStrength", new SettingsEntry(1f)},
            {"NormalJointStrength", new SettingsEntry(5f)},
            {"MaximalJointStrength", new SettingsEntry(50f)},
            {"ConnectorDimension", new SettingsEntry(0.5f)},
            {"ColorTransparency", new SettingsEntry(0.25f)},
            {"FreeAttachDistanceTolerance", new SettingsEntry(0.1f)},
            {"FreeAttachStrutExtension", new SettingsEntry(-0.02f)},
            {"StartDelay", new SettingsEntry(60)},
            {"StrutRealignInterval", new SettingsEntry(2)},
            {"SoundAttachFile", new SettingsEntry("MagicSmokeIndustries/IRActiveStruts/Sounds/AS_Attach")},
            {"SoundDetachFile", new SettingsEntry("MagicSmokeIndustries/IRActiveStruts/Sounds/AS_Detach")},
            {"SoundBreakFile", new SettingsEntry("MagicSmokeIndustries/IRActiveStruts/Sounds/AS_Break")},
            {"GlobalJointEnforcement", new SettingsEntry(false)},
            {"GlobalJointWeakness", new SettingsEntry(false)},
            {"StrutRealignDistanceTolerance", new SettingsEntry(0.02f)},
            {"EnableDocking", new SettingsEntry(false)},
            {"ShowHelpTexts", new SettingsEntry(true)},
            {"ShowStraightOutHint", new SettingsEntry(true)},
            {"StraightOutHintDuration", new SettingsEntry(1)},
            {"TargetHighlightDuration", new SettingsEntry(3)},
            {"KerbalTetherSpringForce", new SettingsEntry(5000f)},
            {"KerbalTetherDamper", new SettingsEntry(500f)},
            {"AllowFreeAttachInEditor", new SettingsEntry(false)},
            {"MaxAngleAutostrutter", new SettingsEntry(100f)},
            {"AutoStrutterConnectToOwnGroup", new SettingsEntry(false)},
            {"EnableFreeAttach", new SettingsEntry(false)},
            {"EnableFreeAttachKerbalTether", new SettingsEntry(false)}
        };

        private static Config instance;

        private Config()
        {
            if (!_configFileExists())
            {
                _initialSave();
                Thread.Sleep(500);
            }
            _load();
        }

        public bool EnableFreeAttach
        {
            get { return _getValue<bool>("EnableFreeAttach"); }
        }

        public bool EnableFreeAttachKerbalTether
        {
            get { return _getValue<bool>("EnableFreeAttachKerbalTether"); }
        }

        public bool AllowFreeAttachInEditor
        {
            get { return _getValue<bool>("AllowFreeAttachInEditor"); }
        }

        public bool AutoStrutterConnectToOwnGroup
        {
            get { return _getValue<bool>("AutoStrutterConnectToOwnGroup"); }
        }

        public float ColorTransparency
        {
            get { return (float) _getValue<double>("ColorTransparency"); }
        }

        private static string ConfigFilePath
        {
            get { return KSPUtil.ApplicationRootPath + CONFIG_FILE_PATH; }
        }

        public float ConnectorDimension
        {
            get { return (float) _getValue<double>("ConnectorDimension"); }
        }

        public bool DockingEnabled
        {
            get { return _getValue<bool>("EnableDocking"); }
        }

        public string EditorInputLockId
        {
            get { return EDITOR_INPUT_LOCK_ID; }
        }

        public float FreeAttachDistanceTolerance
        {
            get { return (float) _getValue<double>("FreeAttachDistanceTolerance"); }
        }

        public string FreeAttachHelpText
        {
            get { return FREE_ATTACH_HELP_TEXT; }
        }

        public float FreeAttachStrutExtension
        {
            get { return (float) _getValue<double>("FreeAttachStrutExtension"); }
        }

        public bool GlobalJointEnforcement
        {
            get { return _getValue<bool>("GlobalJointEnforcement"); }
        }

        public bool GlobalJointWeakness
        {
            get { return _getValue<bool>("GlobalJointWeakness"); }
        }

        public static Config Instance
        {
            get { return instance ?? (instance = new Config()); }
        }

        public float KerbalTetherDamper
        {
            get { return _getValue<float>("KerbalTetherDamper"); }
        }

        public float KerbalTetherSpringForce
        {
            get { return _getValue<float>("KerbalTetherSpringForce"); }
        }

        public string LinkHelpText
        {
            get { return LINK_HELP_TEXT; }
        }

        public float MaxAngle
        {
            get { return (float) _getValue<double>("MaxAngle"); }
        }

        public float MaxAngleAutostrutter
        {
            get { return _getValue<float>("MaxAngleAutostrutter"); }
        }

        public float MaxDistance
        {
            get { return (float) _getValue<double>("MaxDistance"); }
        }

        public float MaximalJointStrength
        {
            get { return (float) _getValue<double>("MaximalJointStrength"); }
        }

        public string ModuleActiveStrutFreeAttachTarget
        {
            get { return MODULE_ACTIVE_STRUT_FREE_ATTACH_TARGET; }
        }

        public string ModuleKerbalHook
        {
            get { return MODULE_KERBAL_HOOK; }
        }

        public string ModuleName
        {
            get { return MODULE_NAME; }
        }

        public float NormalJointStrength
        {
            get { return (float) _getValue<double>("NormalJointStrength"); }
        }

        public bool ShowHelpTexts
        {
            get { return _getValue<bool>("ShowHelpTexts"); }
        }

        public bool ShowStraightOutHint
        {
            get { return _getValue<bool>("ShowStraightOutHint"); }
        }

        public string SoundAttachFileUrl
        {
            get { return _getValue<string>("SoundAttachFile"); }
        }

        public string SoundBreakFileUrl
        {
            get { return _getValue<string>("SoundBreakFile"); }
        }

        public string SoundDetachFileUrl
        {
            get { return _getValue<string>("SoundDetachFile"); }
        }

        public int StartDelay
        {
            get { return _getValue<int>("StartDelay"); }
        }

        public int StraightOutHintDuration
        {
            get { return _getValue<int>("StraightOutHintDuration"); }
        }

        public float StrutRealignDistanceTolerance
        {
            get { return (float) _getValue<double>("StrutRealignDistanceTolerance"); }
        }

        public int StrutRealignInterval
        {
            get { return _getValue<int>("StrutRealignInterval"); }
        }

        public int TargetHighlightDuration
        {
            get { return _getValue<int>("TargetHighlightDuration"); }
        }

        public float WeakJointStrength
        {
            get { return (float) _getValue<double>("WeakJointStrength"); }
        }

        private static bool _configFileExists()
        {
            return File.Exists(ConfigFilePath);
        }

        private static T _getValue<T>(string key)
        {
            if (!values.ContainsKey(key))
            {
                throw new ArgumentException("config key unknown");
            }
            var val = values[key];
            var ret = val.Value ?? val.DefaultValue;
            return (T) Convert.ChangeType(ret, typeof (T));
        }

        private static void _initialSave()
        {
            ConfigNode node = new ConfigNode(), settings = new ConfigNode(SETTINGS_NODE_NAME);
            foreach (var settingsEntry in values)
            {
                settings.AddValue(settingsEntry.Key, settingsEntry.Value.DefaultValue);
            }
            node.AddNode(settings);
            node.Save(ConfigFilePath);
        }

        private static void _load()
        {
            var node = ConfigNode.Load(ConfigFilePath);
            var settings = node.GetNode(SETTINGS_NODE_NAME);
            foreach (var settingsEntry in values)
            {
                var val = settings.GetValue(settingsEntry.Key);
                if (val != null)
                {
                    settingsEntry.Value.Value = val;
                }
            }
        }
    }
}