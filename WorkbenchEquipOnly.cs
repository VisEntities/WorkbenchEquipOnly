using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Workbench Equip Only", "VisEntities", "1.0.1")]
    [Description("Requires players to be near workbenches to equip weapon mods.")]
    public class WorkbenchEquipOnly : RustPlugin
    {
        #region Fields

        private static WorkbenchEquipOnly _plugin;
        private static Configuration _config;

        // Tracks whether a message has been sent to a player for the current attempt to equip a weapon mod item.
        // This's necessary because the 'CanAcceptItem' hook is called multiple times during the validation process and we want to avoid sending duplicate messages.
        private Dictionary<ulong, bool> _messageSent = new Dictionary<ulong, bool>();

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Required Workbench Level For Attachments")]
            public Dictionary<string, int> RequiredWorkbenchLevelForAttachments { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                RequiredWorkbenchLevelForAttachments = new Dictionary<string, int>
                {
                    {
                        "weapon.mod.flashlight", 1
                    },
                    {
                        "weapon.mod.simplesight", 1
                    },
                    {
                        "weapon.mod.muzzlebrake", 2
                    },
                    {
                        "weapon.mod.muzzleboost", 2
                    },
                    {
                        "weapon.mod.holosight", 2
                    },
                    {
                        "weapon.mod.lasersight", 2
                    },
                    {
                        "weapon.mod.extendedmags", 2
                    },
                    {
                        "weapon.mod.silencer", 3
                    },
                    {
                        "weapon.mod.small.scope", 3
                    },
                    {
                        "weapon.mod.8x.scope", 3
                    }
                }
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
        }

        private void Unload()
        {
            _config = null;
            _plugin = null;
        }

        private ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            if (item == null || container == null)
                return null;

            if (!(container.parent is Item weapon) || weapon.GetOwnerPlayer() == null)
                return null;

            BasePlayer player = weapon.GetOwnerPlayer();

            if (_config.RequiredWorkbenchLevelForAttachments.TryGetValue(item.info.shortname, out int requiredWorkbenchLevel))
            {
                if (PermissionUtil.HasPermission(player, PermissionUtil.IGNORE))
                    return ItemContainer.CanAcceptResult.CanAccept;

                if (player.currentCraftLevel < (float)requiredWorkbenchLevel)
                {
                    if (!_messageSent.ContainsKey(player.userID) || !_messageSent[player.userID])
                    {
                        _messageSent[player.userID] = true;
                        SendMessage(player, Lang.WrongWorkbenchLevel, requiredWorkbenchLevel);
                    }

                    timer.Once(1f, () => _messageSent[player.userID] = false);
                    return ItemContainer.CanAcceptResult.CannotAccept;
                }

                _messageSent[player.userID] = false;
            }

            return ItemContainer.CanAcceptResult.CanAccept;
        }

        #endregion Oxide Hooks

        #region Permissions

        private static class PermissionUtil
        {
            public const string IGNORE = "workbenchequiponly.ignore";
            private static readonly List<string> _permissions = new List<string>
            {
                IGNORE,
            };

            public static void RegisterPermissions()
            {
                foreach (var permission in _permissions)
                {
                    _plugin.permission.RegisterPermission(permission, _plugin);
                }
            }

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                return _plugin.permission.UserHasPermission(player.UserIDString, permissionName);
            }
        }

        #endregion Permissions

        #region Localization

        private class Lang
        {
            public const string WrongWorkbenchLevel = "WrongWorkbenchLevel";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.WrongWorkbenchLevel] = "You need to be near a level {0} workbench to equip this attachment."
            }, this, "en");
        }

        private void SendMessage(BasePlayer player, string messageKey, params object[] args)
        {
            string message = lang.GetMessage(messageKey, this, player.UserIDString);
            if (args.Length > 0)
                message = string.Format(message, args);

            SendReply(player, message);
        }

        #endregion Localization
    }
}