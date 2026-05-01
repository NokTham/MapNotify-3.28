using ExileCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MapNotify_3_28
{
    public partial class MapNotify_3_28 : BaseSettingsPlugin<MapNotifySettings>
    {

        private string GetProfileDirectory()
        {
            var path = Path.Combine(ConfigDirectory, "Profiles", Settings.SelectedProfile?.Value ?? "Default");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        private void EnsureProfileStructure()
        {
            var profilesRoot = Path.Combine(ConfigDirectory, "Profiles");
            if (!Directory.Exists(profilesRoot)) Directory.CreateDirectory(profilesRoot);

            var defaultPath = Path.Combine(profilesRoot, "Default");
            if (!Directory.Exists(defaultPath)) Directory.CreateDirectory(defaultPath);

            // Migration: Move old files to Default profile
            string[] filesToMigrate = { "GoodMods.txt", "BadMods.txt" };
            foreach (var file in filesToMigrate)
            {
                var oldPath = Path.Combine(ConfigDirectory, file);
                var newPath = Path.Combine(defaultPath, file);
                if (File.Exists(oldPath) && !File.Exists(newPath))
                {
                    File.Move(oldPath, newPath);
                }
            }
        }

        private void RefreshProfileList()
        {
            var profilesRoot = Path.Combine(ConfigDirectory, "Profiles");
            if (!Directory.Exists(profilesRoot)) return;
            _availableProfiles = Directory.GetDirectories(profilesRoot).Select(Path.GetFileName).ToList();
        }

        public void ResetConfigs()
        {
            LogMessage("Deleting existing config files...");
            string[] files = { "GoodMods.txt", "BadMods.txt", "ExpectedNodes.txt", "ExpectedBonusNodes.txt" };
            foreach (var file in files)
            {
                var path = (file.Contains("Mods")) ? Path.Combine(GetProfileDirectory(), file) : Path.Combine(ConfigDirectory, file);
                if (File.Exists(path)) File.Delete(path);
            }
            ExpectedNodes = LoadExpectedNodes();
            ExpectedBonusNodes = LoadExpectedBonusNodes();
            GoodModsDictionary = LoadConfigGoodMod();
            BadModsDictionary = LoadConfigBadMod();

        }

        private HashSet<string> LoadExpectedNodes()
        {
            return LoadInternalList("ExpectedNodes.txt");
        }

        private HashSet<string> LoadExpectedBonusNodes()
        {
            return LoadInternalList("ExpectedBonusNodes.txt");
        }

        private HashSet<string> LoadInternalList(string fileName)
        {
            var configPath = Path.Combine(ConfigDirectory, fileName);
            if (File.Exists(configPath)) return ParseFileToSet(configPath);

            // Fallback 1: Standard data folder
            var dataPath = Path.Combine(DirectoryFullName, "data", fileName);
            if (File.Exists(dataPath)) return ParseFileToSet(dataPath);

            // Fallback 2: Source directory (needed for many ExileCore setups)
            var sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Source", "MapNotify-3.28", "data", fileName);
            if (File.Exists(sourcePath)) return ParseFileToSet(sourcePath);

            LogError($"MapNotify: Could not find {fileName} in config or data folders. Atlas highlighting will not work.", 10);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private HashSet<string> ParseFileToSet(string path)
        {
            return File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase);
        }

        private Dictionary<string, StyledText> LoadConfigGoodMod()
        {
            LogMessage("Loading Good Mods...");
            var FullDict = new Dictionary<string, StyledText>();
            
            // Load Good Mods. Using indexer assignment prevents crashes if keys overlap between files.
            foreach (var mod in LoadConfigGood(Path.Combine(GetProfileDirectory(), "GoodMods.txt")))
                FullDict[mod.Key] = mod.Value;
            
            return FullDict;
        }
        private Dictionary<string, StyledText> LoadConfigBadMod()
        {
            LogMessage("Loading Bad Mods...");
            return LoadConfigBad(Path.Combine(GetProfileDirectory(), "BadMods.txt"));
        }


        public Dictionary<string, StyledText> LoadConfigGood(string path)
        {
            if (!File.Exists(path))
                if (path.Contains("GoodMods"))
                    CreateModConfig(path);

            return SafeLoadDictionary(path);
        }

        public Dictionary<string, StyledText> LoadConfigBad(string path)
        {
            var CreateDefaultPath1 = Path.Combine(GetProfileDirectory(), "BadMods.txt");

            if (!File.Exists(CreateDefaultPath1))
                if (CreateDefaultPath1.Contains("BadMods"))
                    CreateBadModConfig(CreateDefaultPath1);

            return SafeLoadDictionary(path);
        }

        private Dictionary<string, StyledText> SafeLoadDictionary(string path)
        {
            var dict = new Dictionary<string, StyledText>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in GenDictionary(path))
            {
                var bricking = false;
                if (line.Length > 3)
                    bool.TryParse(line[3] ?? null, out bricking);

                var baseKey = BaseModExtractor.GetBaseMod(line[0]);
                dict[baseKey] = new StyledText { Text = line[1], Color = SharpToNu(HexToSDXVector4(line[2])), Bricking = bricking };
            }
            return dict;
        }

        public IEnumerable<string[]> GenDictionary(string path)
        {
            return File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line) &&
                line.IndexOf(';') >= 0 && !line.StartsWith("#"))
                .Select(line => line.Split('#')[0].Split(';')
                .Select(parts => parts.Trim()).ToArray());
        }

        public void CreateModConfig(string path)
        {
            #region Create Default Map ModConfig
            new FileInfo(path).Directory.Create();
            var outFile =
@"#Contains;Name in tooltip;RGBA colour code;Bricked (true/false, optional, default false)
# GOOD MODS
MapBeyondLeague;Beyond;00FF00FF
MapMonsterIncreasedItemQuantityAndRarity;Increased Quantity and Rarity;00FF00FF";
            File.WriteAllText(path, outFile);
            LogMessage("Created GoodMods.txt...");
            #endregion
        }

        public void CreateBadModConfig(string path)
        {
            #region Create Default Map Bad Config
            new FileInfo(path).Directory.Create();
            var outFile =
@"#Contains;Name in tooltip;RGBA colour code;Bricked (true/false, optional, default false)
# BAD MODS
ElementalReflect;Elemental Reflect;FF0000FF
PhysicalReflect;Physical Reflect;FF0000FF
MapMonsterLifeLeechImmunity;Cannot Leech Life or Mana;FF0000FF
NoLifeESRegen;No Regen;FF007FFF
MapPlayerReducedRegen;60%% Less Regen;FF007FFF
MapPlayerMaxResists;Max Res Down;FF007FFF
MapPlayerCurseEnfeeble;Enfeeble;FF00FFFF
MapPlayerCurseElementalWeak;Elemental Weakness;FF00FFFF
MapPlayerCurseVuln;Vulnerability;FF00FFFF
MapPlayerCurseTemp;Temporal Chains;FF00FFFF
MapPlayerElementalEquilibrium;Elemental Equilibrium;FF00FFFF
MapTwoBosses;Twinned;00FF00FF
MapDangerousBoss;Boss Damage & Speed;00FF00FF
MapMassiveBoss;Boss AoE & Life;00FF00FF
MapMonsterColdDamage;Extra Phys as Cold;FF007FFF
MapMonsterFireDamage;Extra Phys as Fire;FF007FFF
MapMonsterLightningDamage;Extra Phys as Lightning;FF007FFF
MapMonsterLife;More Monster Life;FF007FFF
MapMonsterFast;Monster Speed;FF007FFF
MapBloodlinesModOnMagicsMapWorld;Bloodlines;FF7F00FF
MapNemesis;Nemesis;FF7F00FF
MapDesecratedGround;Desecrated Ground;CCCC00FF
MapShockedGround;Shocked Ground;CCCC00FF
MapChilledGround;Chilled Ground;CCCC00FF
MapBurningGround;Burning Ground;CCCC00FF";
            File.WriteAllText(path, outFile);
            LogMessage("Created BadMods.txt...");
            #endregion
        }
    }
}
