using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppRUMBLE.Combat.ShiftStones;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Pools;
using MelonLoader;
using RumbleModdingAPI.RMAPI;
using UIFramework;
using System.Collections;
using System.Text.RegularExpressions;
using HarmonyLib;
using MelonLoader.Preferences;
using UnityEngine;
using UnityEngine.VFX;
using BuildInfo = RumbleTrees.BuildInfo;
using Random = System.Random;

[assembly: MelonInfo(typeof(RumbleTrees.Core), BuildInfo.Name, BuildInfo.Version, BuildInfo.Author, BuildInfo.DownloadLink)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]

namespace RumbleTrees
{
    public static class BuildInfo
    {
        public const string Version = "3.0.0";
        public const string Name = "RumbleTrees";
        public const string Author = "Orangenal";
        public const string DownloadLink = "https://thunderstore.io/c/rumble/p/Orangenal/RumbleTrees/";
    }

    public class Validator : ValueValidator
    {
        private string[] themes = ["cherry", "orange", "yellow", "red", "rainbow"];
        public static string[] stones = ["flow", "vigor", "volatile", "adamant", "charge", "guard", "stubborn", "surge"];
        public static string[] mats = ["flow", "vigor", "volatile", "adamant", "charge", "guard", "stubborn", "surge", "vanilla"];
        private string type;

        public Validator(string type)
        {
            this.type = type;
        }

        public override bool IsValid(object value)
        {
            if (value.GetType() != typeof(string))
            {
                return false;
            }

            string Input = (string)value;
            
            string rgbPattern = @"^(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\s(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\s(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)$";
            string hexPattern = @"^#?[0-9A-Fa-f]{6}$";

            if (Input.ToLower() == "random") return true;
            if (Input.ToLower() == "vanilla") return true;

            if (type.EndsWith("Mat"))
            {
                return mats.Contains(Input.ToLower());
            }

            if (Regex.IsMatch(Input, rgbPattern) || Regex.IsMatch(Input, hexPattern))
            {
                return true;
            }
            else
            {
                Input = Input.ToLower();
                if (themes.Contains(Input) && type == "leaf") return true;
                return false;
            }
        }

        public override object EnsureValid(object value)
        {
            return value;
        }
    }

    public class Core : MelonMod
    {
        private string currentScene = "Loader";
        private bool enabled = true;
        private int sceneID = -1;
        
        private AssetBundle assetBundle = null;

        Random rand = new();

        private List<GameObject> leafObjects = new List<GameObject>();
        private List<GameObject> fruitObjects = new List<GameObject>();
        private GameObject VFXsObject = null;
        private Color originalLeafColour = new Color();
        private Material originalLeafMaterial = null;
        private Material originalFruitMaterial = null;
        private Il2CppStructArray<GradientColorKey> originalVFXColours = null;

        private MelonPreferences_Category mprefsCategoryMod;
        private MelonPreferences_Category mprefsCategoryLeaves;
        private MelonPreferences_Category mprefsCategoryFruits;

        private List<MelonPreferences_Entry<bool>> mprefs_scenes = new();
        private MelonPreferences_Entry<bool> mpref_VFXEnabled;
        private MelonPreferences_Entry<int> mpref_rainbowSpeed;
        
        private MelonPreferences_Entry<string> mpref_leafMaterial;
        private MelonPreferences_Entry<string> mpref_fruitMaterial;
        private MelonPreferences_Entry<string> mpref_leafColour;
        private MelonPreferences_Entry<string> mpref_fruitColour;
        
        //private string selectedLeafMaterial = "vanilla";
        //private string selectedFruitMaterial = "vanilla";
        private Color selectedLeafColour = default;
        //private string strSelectedLeafColour = "Cherry";
        private Color selectedFruitColour = Color.white;
        private Material customFruitMat;

        //private string strSelectedFruitColour = "FFFFFF";
        private object rainbowLeafCoroutine = null;
        private object rainbowFruitCoroutine = null;

        // Tree object locations
        private string[] GymTrees = [
            "SCENE/GYMMoss"
        ];
        private string[] RingTrees = [
            "Scene/Map0Leaves",
        ];
        private string[] ParkTrees = [
            "SCENE/PARKMos",
            "INTERACTABLES/Fruit/Fruit"
        ];

        // Presets
        private Color cherryColour = new Color(0.86f, 0.54f, 0.9f, 1f);
        private Color orangeColour = new Color(1.0f, 0.44f, 0.0f, 1f);
        private Color yellowColour = new Color(1.0f, 0.78f, 0.0f, 1f);
        private Color redColour = new Color(0.66f, 0.0f, 0.0f, 1f);
        
        private const string USER_DATA = "UserData/RumbleTrees/";
        private const string CONFIG_FILE = "config.cfg";

        // Takes a shiftstone as a string and return the material of that stone
        public static Material stringToStone(string stoneName)
        {
            if (!Validator.stones.Contains(stoneName))
            {
                MelonLogger.Error($"\"{stoneName}\" is not the name of a valid shiftstone!");
                throw new Exception("Provided stone name is invalid");
            }

            string accurateStoneName = "";

            if (stoneName.EndsWith("Stone"))
            {
                accurateStoneName = stoneName;
            }
            else
            {
                switch (stoneName)
                {
                    case "flow":
                        accurateStoneName = "FlowStone";
                        break;
                    case "volatile":
                        accurateStoneName = "VolatileStone";
                        break;
                    case "adamant":
                        accurateStoneName = "AdamantStone";
                        break;
                    case "charge":
                        accurateStoneName = "ChargeStone";
                        break;
                    case "stubborn":
                        accurateStoneName = "StubbornStone";
                        break;
                    case "guard":
                        accurateStoneName = "GuardStone";
                        break;
                    case "vigor":
                        accurateStoneName = "VigorStone";
                        break;
                    case "surge":
                        accurateStoneName = "SurgeStone";
                        break;
                }
            }

            List<GameObject> HiddenStones = new List<GameObject>();

            for (int i = 0; i < ShiftstoneLookupTable.instance.availableShiftstones.Count; i++)
            {
                HiddenStones.Add(ShiftstoneLookupTable.instance.availableShiftstones[i].gameObject);
            }
            GameObject stone = HiddenStones.Where(i => i.name == accurateStoneName).First();
            Material material = stone.transform.GetChild(0).GetComponent<MeshRenderer>().material;
            return material;
        }
        
        // Dynamic in case I want to load other things in future
        private dynamic LoadAsset<T>(string assetName, AssetBundle assetBundle)
        {
            if (assetBundle == null)
            {
                MelonLogger.Error("AssetBundle cannot be null!");
                return null;
            }

            else if (assetName == null || assetName == "")
            {
                MelonLogger.Error("No asset name provided!");
                return null;
            }

            if (typeof(T) == typeof(Material))
            {
                Material mat;
                if (assetName == "FruitMat")
                {
                    foreach (string asset in assetBundle.GetAllAssetNames())
                    {
                        MelonLogger.Msg(asset);
                    }
                    mat = assetBundle.LoadAsset<Material>("assets/rumbletrees/fruitfinal.mat");
                    if (mat == null)
                    {
                        throw new NullReferenceException("Material is null");
                    }
                    return mat;
                }
                else
                {
                    MelonLogger.Msg("Asset not found!");
                    return null;
                }

            }
            else if (typeof(T) == typeof(Shader))
            {
                Shader shader;
                if (assetName == "FruitMat")
                {
                    shader = assetBundle.LoadAsset<Shader>("assets/rumbletrees/editedrumbleprop.shadergraph");
                    if (shader == null)
                    {
                        throw new NullReferenceException("Shader is null");
                    }
                    return shader;
                }
                else
                {
                    MelonLogger.Msg("Asset not found!");
                    return null;
                }
            }
            else
            {
                MelonLogger.Error("No valid type provided!");
                return null;
            }
        }

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("started init");
            mprefsCategoryMod = MelonPreferences.CreateCategory("RumbleTrees_Mod", "General");
            mprefsCategoryLeaves = MelonPreferences.CreateCategory("RumbleTrees_Trees", "Trees");
            mprefsCategoryFruits = MelonPreferences.CreateCategory("RumbleTrees_Fruits", "Fruits");
            
            if (!Directory.Exists(USER_DATA))
            {
                Directory.CreateDirectory(USER_DATA);
            }
            
            mprefsCategoryMod.SetFilePath(Path.Combine(USER_DATA, CONFIG_FILE));
            mprefsCategoryLeaves.SetFilePath(Path.Combine(USER_DATA, CONFIG_FILE));
            mprefsCategoryFruits.SetFilePath(Path.Combine(USER_DATA, CONFIG_FILE));

            mpref_leafColour = mprefsCategoryLeaves.CreateEntry("LeafColour", "Cherry", "Leaf Colour",
                "The colour to set the tree leaves (moss) to. Can be either \"Vanilla,\" \"Random,\" \"Rainbow,\" or a custom colour in one of the supported formats: 255 255 255 / FFFFFF",
                validator: new Validator("leaf"));
            mpref_leafMaterial = mprefsCategoryLeaves.CreateEntry("LeafMaterial", "Vanilla", "Leaf Material",
                "Allows you to turn the trees into shiftstones.", validator: new Validator("leafMat"));
            mpref_fruitColour = mprefsCategoryFruits.CreateEntry("FruitColour", "Vanilla", "Fruit Colour",
                "The colour to set the fruits to. Can be either \"Vanilla,\" \"Random,\" \"Rainbow,\" or a custom colour in one of the supported formats: 255 255 255 / FFFFFF",
                validator: new Validator("fruit"));
            mpref_fruitMaterial = mprefsCategoryFruits.CreateEntry("FruitMaterial", "Vanilla", "Fruit Material",
                "Allows you to turn the fruit into shiftstones.", validator: new Validator("fruitMat"));

            foreach (string scene in new[] {"Gym", "Park", "Ring"})
            {
                mprefs_scenes.Add(mprefsCategoryMod.CreateEntry("EnabledIn" + scene, true, "Enabled in " + scene, "Whether the mod should be active in the " + scene.ToLower()));
                
                mprefs_scenes.Last().OnEntryValueChanged.Subscribe((bool oldVal, bool newVal) => OnSave(oldVal, newVal, scene));
            }
            
            mpref_VFXEnabled = mprefsCategoryMod.CreateEntry("VFXEnabled", false, "Falling Leaf VFX Enabled");
            mpref_rainbowSpeed = mprefsCategoryMod.CreateEntry("RainbowSpeed", 1, "Rainbow speed",
                "The speed of rainbow leaves / fruits (if selected)");
            
            mpref_leafColour.OnEntryValueChanged.Subscribe(OnLeafColourChange);
            mpref_fruitColour.OnEntryValueChanged.Subscribe(OnFruitColourChange);
            mpref_leafMaterial.OnEntryValueChanged.Subscribe(OnLeafMaterialChange);
            mpref_fruitMaterial.OnEntryValueChanged.Subscribe(OnFruitMaterialChange);

            if (mpref_leafColour.Value.ToLower() != "vanilla" && mpref_leafColour.Value.ToLower() != "rainbow") setSelectedLeafColour(mpref_leafColour.Value);
            if (mpref_fruitColour.Value.ToLower() != "vanilla" && mpref_fruitColour.Value.ToLower() != "rainbow") setSelectedFruitColour(mpref_fruitColour.Value);

            mpref_VFXEnabled.OnEntryValueChanged.Subscribe(OnToggleVFXs);
            
            mpref_leafColour.OnEntryValueChanged.Subscribe(OnLeafColourChange);
            mpref_fruitColour.OnEntryValueChanged.Subscribe(OnFruitColourChange);
            
            mpref_leafMaterial.OnEntryValueChanged.Subscribe(OnLeafMaterialChange);
            mpref_fruitMaterial.OnEntryValueChanged.Subscribe(OnFruitMaterialChange);

            UI.RegisterMelon(this, mprefsCategoryMod, mprefsCategoryFruits, mprefsCategoryLeaves);
            
            assetBundle = AssetBundles.LoadAssetBundleFromStream(this, "RumbleTrees.rumbletrees");
            customFruitMat = new(LoadAsset<Material>("FruitMat", assetBundle));
            customFruitMat.hideFlags = HideFlags.DontUnloadUnusedAsset;

            LoggerInstance.Msg("Initialised.");
        }

        public override void OnLateInitializeMelon()
        {
            MelonLogger.Msg("£" + mpref_leafColour.Value);
            Il2CppSystem.Collections.Generic.List<PooledMonoBehaviour> fruitPool = PoolManager.Instance.GetPool("Fruit").PooledObjects;
            
            foreach (PooledMonoBehaviour fruit in fruitPool)
            {
                fruitObjects.Add(fruit.gameObject);
                MeshRenderer renderer = fruit.gameObject.GetComponent<MeshRenderer>();
                List<Material> materials = new();
                materials.Add(renderer.material);
                materials.Add(customFruitMat);
                renderer.materials = materials.ToArray();
            }
            
            if (mpref_fruitMaterial.Value.ToLower() != "vanilla")
                MelonCoroutines.Start(UpdateFruitMaterial(mpref_fruitMaterial.Value));
            else if (mpref_fruitColour.Value.ToLower() != "vanilla")
                UpdateFruitColour(selectedFruitColour);
            else ResetFruitColour();
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Z))
            {
                if (customFruitMat == null)
                {
                    MelonLogger.Msg("Fruit mat is null");
                }
                else
                {
                    MelonLogger.Msg("Nope");
                }
            }
        }

        private void OnSave(bool oldVal, bool newVal, string scene)
        {
            // If the trees in the current scene should be enabled / disabled, do that
            if (scene == currentScene)
            {
                enabled = !enabled;
                if (enabled)
                {
                    if (mpref_leafColour.Value.ToLower() != "vanilla") UpdateLeafColour(selectedLeafColour);
                    if (mpref_fruitColour.Value.ToLower() != "vanilla") UpdateFruitColour(selectedFruitColour);
                    if (mpref_leafMaterial.Value.ToLower() != "vanilla") MelonCoroutines.Start(UpdateLeafMaterial(mpref_leafMaterial.Value));
                    if (mpref_fruitMaterial.Value.ToLower() != "vanilla") MelonCoroutines.Start(UpdateFruitMaterial(mpref_fruitMaterial.Value));

                    if (mpref_leafColour.Value.ToLower() == "rainbow")
                    {
                        rainbowLeafCoroutine = MelonCoroutines.Start(RAINBOWLEAVES());
                    }

                    if (mpref_leafColour.Value.ToLower() == "rainbow")
                    {
                        rainbowFruitCoroutine = MelonCoroutines.Start(RAINBOWFRUITS());
                    }

                    //InitLightmaps();
                }
                else
                {
                    ResetLeafColour();
                    ResetFruitColour();
                    ResetLeafMaterial();
                    ResetFruitMaterial();
                }
            }
        }

        // Update leaves upon colour change
        private void OnLeafColourChange(string oldVal, string newVal)
        {
            if (mpref_leafColour.Value.ToLower() == "vanilla")
            {
                //InitLightmaps();
            }

            if (rainbowLeafCoroutine != null)
            {
                MelonCoroutines.Stop(rainbowLeafCoroutine);
                rainbowLeafCoroutine = null;
            }

            if (mpref_leafColour.Value.ToLower() == "vanilla")
            {
                ResetLeafColour();
                return;
            }
            else if (mpref_leafColour.Value.ToLower() == "rainbow")
            {
                if (enabled) rainbowLeafCoroutine = MelonCoroutines.Start(RAINBOWLEAVES());
                return;
            }

            setSelectedLeafColour(mpref_leafColour.Value);
            UpdateLeafColour(selectedLeafColour);
        }

        private void OnToggleVFXs(bool oldVal, bool newVal)
        {
            ToggleVFXs(newVal);
        }

        private void ToggleVFXs(bool enable)
        {
            if (VFXsObject != null)
            {
                Transform parent = VFXsObject.transform.parent;

                // I don't want to activate other stuff if the user doesn't want it and afaik there aren't any other mods that enable these so I'm not worried about compatibility
                parent.GetChild(0).gameObject.active = false;
                parent.GetChild(1).gameObject.active = false;

                parent.gameObject.active = enable;
            }
        }

        // Update leaves upon material change
        private void OnLeafMaterialChange(string oldVal, string newVal)
        {
            if (newVal.ToLower() == "vanilla")
            {
                ResetLeafMaterial();
                return;
            }

            MelonCoroutines.Start(UpdateLeafMaterial(newVal.ToLower()));
        }

        // Update fruits on colour change
        private void OnFruitColourChange(string oldVal, string newVal)
        {
            if (rainbowFruitCoroutine != null)
            {
                MelonCoroutines.Stop(rainbowFruitCoroutine);
                rainbowFruitCoroutine = null;
            }

            if (newVal.ToLower() == "rainbow")
            {
                if (enabled) rainbowFruitCoroutine = MelonCoroutines.Start(RAINBOWFRUITS());
                return;
            }
            if (newVal.ToLower() == "vanilla")
            {
                ResetFruitColour();
                return;
            }

            setSelectedFruitColour(newVal.ToLower());
            UpdateFruitColour(selectedFruitColour);
        }

        // Update fruits on material change
        private void OnFruitMaterialChange(string oldVal, string newVal)
        {
            if (newVal.ToLower() == "vanilla")
            {
                ResetFruitMaterial();
                return;
            }

            MelonCoroutines.Start(UpdateFruitMaterial(newVal.ToLower()));
        }

        // Converts preset names to colours
        private void setSelectedLeafColour(string colour)
        {
            switch (colour.ToLower())
            {
                case "cherry":
                    selectedLeafColour = cherryColour;
                    return;
                case "orange":
                    selectedLeafColour = orangeColour;
                    return;
                case "yellow":
                    selectedLeafColour = yellowColour;
                    return;
                case "red":
                    selectedLeafColour = redColour;
                    return;
                case "random":
                    return;
            }

            selectedLeafColour = stringToColour(colour.ToLower());
        }

        // This is actually really unnecessary
        private void setSelectedFruitColour(string colour)
        {
            if (colour != "random") selectedFruitColour = stringToColour(colour);
        }

        // You'll never guess what this one does
        public Color stringToColour(string colour)
        {
            colour = colour.Replace("#", "");
            colour = colour.Replace("0x", "");
            if (colour.Contains(" "))
            {
                string[] parts = colour.Split(' ');
                if (parts.Length == 3 &&
                    byte.TryParse(parts[0], out byte rByte) &&
                    byte.TryParse(parts[1], out byte gByte) &&
                    byte.TryParse(parts[2], out byte bByte))
                {
                    return new Color(rByte / 255f, gByte / 255f, bByte / 255f);
                }
                else
                {
                    MelonLogger.Error("Somehow not a valid colour");
                    throw new Exception("Provided string is not in a valid format.");
                }
            }
            else if (colour.Length == 6 &&
                int.TryParse(colour, System.Globalization.NumberStyles.HexNumber, null, out int hex))
            {
                float r = ((hex >> 16) & 0xFF) / 255f;
                float g = ((hex >> 8) & 0xFF) / 255f;
                float b = (hex & 0xFF) / 255f;
                return new Color(r, g, b);
            }
            else
            {
                MelonLogger.Error($"\"{colour}\" is somehow not a valid colour");
                throw new Exception("Provided string is not in a valid format.");
            }
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            if (sceneName == "Park")
            {
                fruitObjects.RemoveAt(fruitObjects.Count - 1);
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            // Reset all the variables
            leafObjects = new List<GameObject>();
            VFXsObject = null;
            originalLeafColour = new Color();
            originalLeafMaterial = null;
            //originalFruitMaterial = null;
            originalVFXColours = null;

            // Stop the rainbows to avoid errors and epilepsy
            if (rainbowLeafCoroutine != null)
            {
                MelonCoroutines.Stop(rainbowLeafCoroutine);
                rainbowLeafCoroutine = null;
            }

            if (rainbowFruitCoroutine != null)
            {
                MelonCoroutines.Stop(rainbowFruitCoroutine);
                rainbowFruitCoroutine = null;
            }

            // Add the leaf and root objects
            if (sceneName == "Gym")
            {
                currentScene = sceneName;
                sceneID = 0;
                leafObjects.Add(GameObject.Find(GymTrees[0]));

                VFXsObject = GameObjects.Gym.SCENEVFXSFX.VisualEffects.FallingLeafVFXs.GetGameObject();
            }

            if (sceneName == "Map0")
            {
                currentScene = "Ring";
                sceneID = 2;
                GameObject leaves = GameObject.Find(RingTrees[0]);
                leafObjects.Add(leaves);
            }

            if (sceneName == "Park")
            {
                currentScene = "Park";
                sceneID = 1;
                GameObject leaves = GameObject.Find(ParkTrees[0]);
                leafObjects.Add(leaves);

                VFXsObject = GameObjects.Park.SCENEVFXSFX.VisualEffects.FallingLeafVFXs.GetGameObject();
                fruitObjects.Add(GameObject.Find(ParkTrees[1]));
                if (mpref_fruitColour.Value.ToLower() != "vanilla") UpdateFruitColour(selectedFruitColour);
            }

            if (sceneName == "Loader")
            {
                return;
            }

            // UPDATE EVERYTHING!!!
            enabled = mprefs_scenes[sceneID].Value;
            if (enabled)
            {
                if (mpref_leafColour.Value.ToLower() != "vanilla") UpdateLeafColour(selectedLeafColour);
                ToggleVFXs(mpref_VFXEnabled.Value);
                if (mpref_leafMaterial.Value.ToLower() != "vanilla") MelonCoroutines.Start(UpdateLeafMaterial(mpref_leafMaterial.Value));
            }
            if (mpref_leafColour.Value.ToLower() == "rainbow" && rainbowLeafCoroutine == null)
            {
                rainbowLeafCoroutine = MelonCoroutines.Start(RAINBOWLEAVES());
            }

            if (mpref_fruitColour.Value.ToLower() == "rainbow" && rainbowFruitCoroutine == null)
            {
                rainbowFruitCoroutine = MelonCoroutines.Start(RAINBOWFRUITS());
            }
        }

        // I'm not gonna comment on these functions cause it should be obvious from the name
        private void UpdateLeafColour(Color colour)
        {
            if (!enabled) return;

            if (mpref_leafColour.Value.ToLower() == "random") colour = new Color((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
            if (leafObjects.Count != 0) // This shouldn't be false but better safe than sorry
            {

                foreach (GameObject leafObject in leafObjects)
                {
                    MeshRenderer renderer = leafObject.GetComponent<MeshRenderer>();
                    Material material = renderer.material;

                    // Can't reset it later without saving it
                    if (originalLeafColour == default)
                    {
                        originalLeafColour = material.GetColor("_Main_color");
                    }

                    material.SetColor("_Main_color", colour);
                }

                // If it's a shiftstone then the VFXs colour is handled in UpdateLeafMaterial
                if (mpref_leafMaterial.Value.ToLower() == "vanilla")
                {
                    UpdateVFXs(colour);
                }
            }
        }

        private void UpdateVFXs(Color colour)
        {
            if (!enabled) return;
            if (VFXsObject != null)
            {
                VisualEffect leafVFX;
                for (int i = 0; i < VFXsObject.transform.GetChildCount(); i++)
                {
                    leafVFX = VFXsObject.transform.GetChild(i).gameObject.GetComponent<VisualEffect>();
                    GradientColorKey[] keys = new GradientColorKey[2]; // Why is it two colours there's literally no gradient
                    Gradient gradient = leafVFX.GetGradient("Leaf Color Gradient");

                    if (originalVFXColours == null)
                    {
                        originalVFXColours = gradient.colorKeys;
                    }

                    keys[0].color = colour;
                    keys[1].color = colour;

                    gradient.colorKeys = keys;

                    leafVFX.SetGradient("Leaf Color Gradient", gradient);
                }
            }
            else if (currentScene != "Ring") // There aren't VFXs on ring so we don't need a warning
            {
                MelonLogger.Warning("Leaf VFX object not found!");
            }
        }

        private void UpdateFruitColour(Color colour)
        {
            if (mpref_fruitMaterial.Value.ToLower() != "vanilla") return;

            GameObject fruit = fruitObjects.First();

            foreach (GameObject fruitObject in fruitObjects)
            {
                MeshRenderer renderer = fruitObject.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    if (renderer.materials.Length == 2)
                    {
                        renderer.materials.Last().color = colour;
                        renderer.materials.Last().SetInt("_Enabled", 1);
                    }
                }
            }
        }

        private void ResetLeafColour()
        {
            if (rainbowLeafCoroutine != null)
            {
                MelonCoroutines.Stop(rainbowLeafCoroutine);
                rainbowLeafCoroutine = null;
            }

            if (originalLeafColour != default) // If the originalLeafColours aren't set then nothing was changed and we don't need to reset anything.
            {
                if (leafObjects.Count != 0)
                {
                    foreach (GameObject leafObject in leafObjects)
                    {
                        MeshRenderer renderer = leafObject.GetComponent<MeshRenderer>();
                        Material material = renderer.material;

                        material.SetColor("_Main_color", originalLeafColour);
                    }
                }
            }
            if (mpref_leafMaterial.Value.ToLower() == "vanilla")
            {
                ResetVFXs();
            }
        }

        private void ResetVFXs()
        {
            if (originalVFXColours != null)
            {
                if (VFXsObject != null)
                {
                    VisualEffect leafVFX;
                    for (int i = 0; i < VFXsObject.transform.GetChildCount(); i++)
                    {
                        leafVFX = VFXsObject.transform.GetChild(i).gameObject.GetComponent<VisualEffect>();
                        Gradient gradient = leafVFX.GetGradient("Leaf Color Gradient");

                        gradient.colorKeys = originalVFXColours;

                        leafVFX.SetGradient("Leaf Color Gradient", gradient);
                    }
                }
            }
        }

        private void ResetFruitColour()
        {
            if (rainbowFruitCoroutine != null)
            {
                MelonCoroutines.Stop(rainbowFruitCoroutine);
                rainbowFruitCoroutine = null;
            }
            
            foreach (GameObject fruit in fruitObjects)
            {
                MeshRenderer renderer = fruit.GetComponent<MeshRenderer>();

                if (renderer != null)
                {
                    if (renderer.materials.Length == 2)
                    {
                        renderer.materials.Last().SetInt("_Enabled", 0);
                    }
                }

            }
        }

        private IEnumerator UpdateLeafMaterial(string materialName)
        {
            if (currentScene == "Loader") yield break;
            if (!enabled) yield break;
            if (leafObjects.Count != 0)
            {
                if (originalLeafMaterial == null)
                {
                    originalLeafMaterial = leafObjects.First().GetComponent<MeshRenderer>().sharedMaterial;
                }
                yield return null; // Resetting doesn't work if we don't wait a bit
                yield return null; // THREE FRAMES!? (This used to work after only one)
                yield return null;
                if (materialName == "random" && enabled) materialName = Validator.mats[rand.Next(Validator.mats.Length)];
                foreach (GameObject leafObject in leafObjects)
                {
                    MeshRenderer renderer = leafObject.GetComponent<MeshRenderer>();
                    
                    MelonLogger.Msg("Got here " + materialName);
                    if (materialName == "vanilla")
                    {
                        ResetLeafMaterial();
                        UpdateLeafColour(selectedLeafColour);
                    }
                    else
                    {
                        MelonLogger.Msg("Got heregasyuhdgyuawtfgd");
                        renderer.material = stringToStone(materialName);
                        UpdateVFXs(renderer.material.color);
                    }
                }
            }
            yield break;
        }

        IEnumerator UpdateFruitMaterial(string materialName)
        {
            if (currentScene == "Loader") yield break;
            if (!enabled) yield break;
            if (fruitObjects.Count != 0)
            {
                if (originalFruitMaterial == null)
                {
                    originalFruitMaterial = new Material(fruitObjects.First().GetComponent<MeshRenderer>().sharedMaterial);

                    originalFruitMaterial.hideFlags = HideFlags.DontUnloadUnusedAsset;
                }
                yield return null;
                if (materialName == "random") materialName = Validator.mats[rand.Next(Validator.mats.Length)];

                foreach (GameObject rootObject in fruitObjects)
                {
                    MeshRenderer renderer = rootObject.GetComponent<MeshRenderer>();

                    if (materialName == "vanilla")
                    {
                        ResetFruitMaterial();
                    }
                    else renderer.material = stringToStone(materialName);

                    if (renderer.materials.Length == 2)
                    {
                        renderer.materials.Last().SetInt("Enabled", 0);
                    }
                }
            }
        }

        private void ResetLeafMaterial()
        {
            if (currentScene == "Loader") return;
            if (originalLeafMaterial == null) return;
            if (leafObjects.Count != 0)
            {
                foreach (GameObject leafObject in leafObjects)
                {
                    MeshRenderer renderer = leafObject.GetComponent<MeshRenderer>();

                    renderer.material = originalLeafMaterial;
                }
                if (enabled && mpref_leafColour.Value.ToLower() != "vanilla")
                {
                    UpdateLeafColour(selectedLeafColour); // Because if the colour was changed it would've used the wrong property
                }
                else ResetLeafColour();
            }
        }

        private void ResetFruitMaterial()
        {
            if (currentScene == "Loader") return;
            if (originalFruitMaterial == null)
            {
                MelonLogger.Msg("originalfruitmat is null");
                return;
            }
            if (fruitObjects.Count != 0)
            {
                foreach (GameObject rootObject in fruitObjects)
                {
                    MeshRenderer renderer = rootObject.GetComponent<MeshRenderer>();

                    renderer.material = originalFruitMaterial;
                }
                if (enabled && mpref_fruitColour.Value.ToLower() != "vanilla")
                {
                    UpdateFruitColour(selectedFruitColour);
                }
                else ResetFruitColour();
            }
        }

        IEnumerator RAINBOWLEAVES()
        {
            WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
            int FrameCounter = 0;
            int rainbowHue = 0;
            while (true)
            {
                if (FrameCounter >= 2)
                {
                    if (rainbowHue >= 360) rainbowHue = 0;
                    selectedLeafColour = Color.HSVToRGB(rainbowHue / 360f, 1f, 1f);
                    UpdateLeafColour(selectedLeafColour);
                    rainbowHue += mpref_rainbowSpeed.Value;
                    FrameCounter = 0;
                }
                FrameCounter++;
                yield return waitForFixedUpdate;
            }
        }
        
        IEnumerator RAINBOWFRUITS()
        {
            WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
            int FrameCounter = 0;
            int rainbowHue = 0;
            while (true)
            {
                if (FrameCounter >= 2)
                {
                    if (rainbowHue >= 360) rainbowHue = 0;
                    selectedFruitColour = Color.HSVToRGB(rainbowHue / 360f, 1f, 1f);
                    UpdateFruitColour(selectedFruitColour);
                    rainbowHue += mpref_rainbowSpeed.Value;
                    FrameCounter = 0;
                }
                FrameCounter++;
                yield return waitForFixedUpdate;
            }
        }
    }
}