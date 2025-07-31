using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppRUMBLE.Combat.ShiftStones;
using MelonLoader;
using RumbleModUI;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.VFX;
using System.Reflection;
using BuildInfo = RumbleTrees.BuildInfo;

[assembly: MelonInfo(typeof(RumbleTrees.Core), BuildInfo.Name, BuildInfo.Version, BuildInfo.Author, BuildInfo.DownloadLink)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]

namespace RumbleTrees
{
    public static class BuildInfo
    {
        public const string Version = "2.0.0";
        public const string Name = "RumbleTrees";
        public const string Author = "Orangenal";
        public const string DownloadLink = "https://thunderstore.io/c/rumble/p/Orangenal/RumbleTrees/";
    }

    public class Validation : ValidationParameters
    {
        private string[] themes = ["cherry", "orange", "yellow", "red"];
        public static string[] stones = ["flow", "vigor", "volatile", "adamant", "charge", "guard", "stubborn", "surge"];
        public Validation(string type)
        {
            this.type = type;
        }
        private string type;
        public override bool DoValidation(string Input)
        {
            string rgbPattern = @"^(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\s(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\s(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)$";
            string hexPattern = @"^#?[0-9A-Fa-f]{6}$";

            if (Regex.IsMatch(Input, rgbPattern))
            {
                return !type.EndsWith("Mat");
            }
            else if (Regex.IsMatch(Input, hexPattern))
            {
                return !type.EndsWith("Mat");
            }
            else
            {
                Input = Input.ToLower();
                if (Input == "vanilla") return true;
                if (Input == "rainbow" && !type.EndsWith("Mat")) return true;
                if (stones.Contains(Input) && type.EndsWith("Mat")) return true;
                if (themes.Contains(Input) && type == "leaf") return true;
                if (Input == "leaves" && type == "rootMat") return true;
                if (Input == "roots" && type == "leafMat") return true;
                return false;
            }
        }
    }

    public class Core : MelonMod
    {
        private string currentScene = "Loader";
        private int sceneID = -1;
        private bool enabled = true;
        private bool wasLightmapChanged = false;
        private Mod RumbleTrees = new Mod();

        // Property IDs
        private int LCT1;
        private int LCB1;
        private int FLG;
        private int RC1;
        private int RC2;

        private List<GameObject> leafObjects = new List<GameObject>();
        private List<GameObject> rootObjects = new List<GameObject>();
        private GameObject VFXsObject = null;
        private Color[] originalLeafColours = new Color[2];
        private Color[] originalRootColours = new Color[2];
        private Material originalLeafMaterial = null;
        private Material originalRootMaterial = null;
        private Il2CppStructArray<GradientColorKey> originalVFXColours = null;

        private string selectedLeafMaterial = "vanilla";
        private string selectedRootMaterial = "vanilla";
        private Color selectedLeafColour = default;
        private string strSelectedLeafColour = "Cherry";
        private Color selectedRootColour = Color.white;
        private string strSelectedRootColour = "FFFFFF";
        private bool vanillaLightmaps = false;
        private object rainbowLeafCoroutine = null;
        private object rainbowRootCoroutine = null;

        // Tree object locations
        private string[] GymTrees = [
            "--------------SCENE--------------/Gym_Production/Main static group/Foliage/",
            "--------------SCENE--------------/Gym_Production/Main static group/Gymarena/Leave_sphere__23_",
            "--------------SCENE--------------/Gym_Production/Main static group/Gymarena/Leave_sphere__24_",
            "--------------SCENE--------------/Gym_Production/Sub static group/Scene_roots/Test_root_1_middetail",
            "--------------SCENE--------------/Gym_Production/Sub static group(buildings)/Rumble_station/Root",
            "--------------SCENE--------------/Gym_Production/Sub static group(buildings)/School/Cylinder_011",
            "--------------SCENE--------------/Gym_Production/Sub static group(buildings)/School/Cylinder_003",
            "--------------SCENE--------------/Gym_Production/Main static group/Gymarena/Cylinder_015__4_",
            "--------------SCENE--------------/Lighting and effects/Visual Effects/Falling Leaf VFXs"
        ];
        private string[] RingTrees = [
            "Map0_production/Main static group/leave",
            "Map0_production/Main static group/Root"
        ];
        private string[] PitTrees = [
            "Map1_production/Main static group/Leaves_Map2",
            "Lighting & Effects/Visual Effects/Falling Leaf VFXs"
        ];
        private string[] ParkTrees = [
            "________________SCENE_________________/Park/Main static group/Leaves/",
            "________________SCENE_________________/Park/Main static group/Root/",
            "Lighting and effects/Visual Effects/Falling Leaf VFXs"
        ];

        // Presets
        private Color cherryColour = new Color(0.86f, 0.54f, 0.9f, 1f);
        private Color orangeColour = new Color(1.0f, 0.44f, 0.0f, 1f);
        private Color yellowColour = new Color(1.0f, 0.78f, 0.0f, 1f);
        private Color redColour = new Color(0.66f, 0.0f, 0.0f, 1f);

        public static Texture2D LoadEmbeddedPNG(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            string fileName = "RumbleTrees.Resources.";

            if (resourceName == "Gym_Lightmap")
            {
                fileName += "GYM0_final.png";
            }
            else if (resourceName == "Ring_Lightmap")
            {
                fileName += "MAP0_final.png";
            }
            else if (resourceName == "Park_Lightmap")
            {
                fileName += "Park0_final.png";
            }
            else
            {
                MelonLogger.Error($"No texture found with the name: {resourceName}");
                return null;
            }

            using (Stream stream = assembly.GetManifestResourceStream(fileName))
            {
                if (stream == null)
                {
                    Debug.LogError($"Resource '{resourceName}' not found!");
                    return null;
                }

                byte[] data;
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    data = ms.ToArray();
                }

                Texture2D tex = new Texture2D(2, 2); // size doesn't matter, will be replaced
                if (!tex.LoadImage(data))
                {
                    Debug.LogError($"Failed to load texture from resource '{resourceName}'");
                    return null;
                }

                tex.Apply();
                return tex;
            }
        }

        // Takes a shiftstone as a string and return the material of that stone
        public static Material stringToStone(string stoneName)
        {
            if (!Validation.stones.Contains(stoneName))
            {
                MelonLogger.Error($"\"{stoneName}\" is not the name of a valid shiftstone!");
                throw new Exception("Provided stone name is invalid");
            }

            string accurateStoneName = "";

            if (stoneName.EndsWith("Stone"))
            {
                accurateStoneName = stoneName;
            }
            else {
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

        public override void OnInitializeMelon()
        {
            // Setup ModUI settings & description
            RumbleTrees.ModName = BuildInfo.Name;
            RumbleTrees.ModVersion = BuildInfo.Version;
            RumbleTrees.SetFolder(BuildInfo.Name);
            RumbleTrees.AddDescription("Description", "", "Make them pretty!\n\nCurrent presets:\nCherry\nOrange\nYellow\nRed\nRainbow\nVanilla (literally does nothing)", new Tags { IsSummary = true });

            RumbleTrees.AddToList("Enabled in Gym", true, 0, "Enables the mod in the gym", new Tags());
            RumbleTrees.AddToList("Enabled on Ring", true, 0, "Enables the mod on the ring map", new Tags());
            RumbleTrees.AddToList("Enabled on Pit", true, 0, "Enables the mod on the pit map", new Tags());
            RumbleTrees.AddToList("Enabled in Parks", true, 0, "Enables the mod in parks", new Tags());
            RumbleTrees.AddToList("Legacy shaders", false, 0, "Enables the vanilla lightmaps in Ring, the Gym, and Parks, which look different and may not work properly with all leaf colours", new Tags());

            RumbleTrees.AddToList("Leaf colour", "Cherry", "Type in either a preset name or a custom colour in one of the supported formats: \n255 255 255\nFFFFFF", new Tags());
            RumbleTrees.AddToList("Root colour", "FFFFFF", "Type in either \"Rainbow,\" \"Vanilla,\" a shiftstone, or a custom colour in one of the supported formats: \n255 255 255\nFFFFFF", new Tags());
            RumbleTrees.AddToList("Leaf material", "vanilla", "Type in either \"vanilla,\" a shiftstone, or \"roots\" to set the material of the leaves", new Tags());
            RumbleTrees.AddToList("Root material", "vanilla", "Type in either \"vanilla,\" a shiftstone, or \"leaves\" to set the material of the roots", new Tags());

            RumbleTrees.AddToList("Rainbow speed", 1, "The speed of rainbow leaves (if selected)", new Tags());

            RumbleTrees.AddValidation("Leaf colour", new Validation("leaf"));
            RumbleTrees.AddValidation("Root colour", new Validation("root"));
            RumbleTrees.AddValidation("Leaf material", new Validation("leafMat"));
            RumbleTrees.AddValidation("Root material", new Validation("rootMat"));

            RumbleTrees.GetFromFile();

            // Assign settings to their respective variables
            vanillaLightmaps = (bool)RumbleTrees.Settings[5].Value;
            strSelectedLeafColour = ((string)RumbleTrees.Settings[6].Value).ToLower();
            strSelectedRootColour = ((string)RumbleTrees.Settings[7].Value).ToLower();
            selectedLeafMaterial = ((string)RumbleTrees.Settings[8].Value).ToLower();
            selectedRootMaterial = ((string)RumbleTrees.Settings[9].Value).ToLower();

            if (strSelectedLeafColour != "vanilla" && strSelectedLeafColour != "rainbow") setSelectedLeafColour(strSelectedLeafColour);
            if (strSelectedRootColour != "vanilla" && strSelectedRootColour != "rainbow") setSelectedRootColour(strSelectedRootColour);

            RumbleTrees.ModSaved += OnSave;
            RumbleTrees.Settings[6].SavedValueChanged += OnLeafColourChange;
            RumbleTrees.Settings[7].SavedValueChanged += OnRootColourChange;

            RumbleTrees.Settings[8].SavedValueChanged += OnLeafMaterialChange;
            RumbleTrees.Settings[9].SavedValueChanged += OnRootMaterialChange;

            UI.instance.UI_Initialized += OnUIInit;

            // The property IDs aren't always the same for some reason, so we get them again every time
            LCT1 = Shader.PropertyToID("Color_133d236fee76457eb89bac53e692f8a3"); // Found in sharedassets3 path ID 2 (Material Root leave_Map0)
            LCB1 = Shader.PropertyToID("Color_6f942b46fda341409751e4e7f292de58"); // Ditto
            FLG = Shader.PropertyToID("Leaf Color Gradient"); // Found in sharedassets0 path ID 371 (VisualEffectAsset Falling leaves)
            RC1 = Shader.PropertyToID("Color_FA790384"); // Found in Material Rumble root
            RC2 = Shader.PropertyToID("Color_c7120f3b741f4dd48575e89d95f9641d"); // Ditto

            LoggerInstance.Msg("Initialised.");
        }

        private void OnSave()
        {
            // If the trees in the current scene should be enabled / disabled, do that
            if ((bool)RumbleTrees.Settings[sceneID].SavedValue != enabled) {
                enabled = !enabled;
                if (enabled)
                {
                    if (strSelectedLeafColour != "vanilla") UpdateLeafColour(selectedLeafColour);
                    if (strSelectedRootColour != "vanilla") UpdateRootColour(selectedRootColour);
                    if (selectedLeafMaterial != "vanilla") MelonCoroutines.Start(UpdateLeafMaterial(selectedLeafMaterial));
                    if (selectedRootMaterial != "vanilla") MelonCoroutines.Start(UpdateRootMaterial(selectedRootMaterial));

                    if (strSelectedLeafColour == "rainbow")
                    {
                        rainbowLeafCoroutine = MelonCoroutines.Start(RAINBOWLEAVES());
                    }

                    if (strSelectedRootColour == "rainbow")
                    {
                        rainbowRootCoroutine = MelonCoroutines.Start(RAINBOWROOTS());
                    }

                    InitLightmaps();
                }
                else
                {
                    ResetLeafColour();
                    ResetRootColour();
                    ResetLeafMaterial();
                    ResetRootMaterial();
                }
            }

            // If the lightmaps should be enabled / disabled, do that
            if ((bool)RumbleTrees.Settings[5].Value != vanillaLightmaps)
            {
                vanillaLightmaps = !vanillaLightmaps;
                if (leafObjects.Count != 0)
                {
                    foreach (GameObject leafObject in leafObjects)
                    {
                        MeshRenderer renderer = leafObject.GetComponent<MeshRenderer>();
                        MelonCoroutines.Start(SwapLightmap(renderer, currentScene, vanillaLightmaps));
                    }
                }
            }
        }

        // Update leaves upon colour change
        private void OnLeafColourChange(object sender = null, EventArgs e = null)
        {
            if (e != null)
            {
                if (strSelectedLeafColour == "vanilla")
                {
                    InitLightmaps();
                }
                
                ValueChange<string> valueChange = (ValueChange<string>) e;
                strSelectedLeafColour = valueChange.Value.ToLower();

                if (rainbowLeafCoroutine != null)
                {
                    MelonCoroutines.Stop(rainbowLeafCoroutine);
                    rainbowLeafCoroutine = null;
                }

                if (strSelectedLeafColour == "vanilla")
                {
                    ResetLeafColour();
                    return;
                }
                else if (strSelectedLeafColour == "rainbow")
                {
                    if (enabled) rainbowLeafCoroutine = MelonCoroutines.Start(RAINBOWLEAVES());
                    return;
                }
                
                setSelectedLeafColour(strSelectedLeafColour);
                UpdateLeafColour(selectedLeafColour);
            }
        }

        // Update leaves upon material change
        private void OnLeafMaterialChange(object sender = null, EventArgs e = null)
        {
            if (e != null)
            {
                ValueChange<string> valueChange = (ValueChange<string>)e;
                selectedLeafMaterial = valueChange.Value.ToLower();

                if (selectedLeafMaterial == "vanilla")
                {
                    ResetLeafMaterial();
                    return;
                }

                MelonCoroutines.Start(UpdateLeafMaterial(selectedLeafMaterial));
            }
        }

        // Update roots on colour change
        private void OnRootColourChange(object sender = null, EventArgs e = null)
        {
            if (e != null)
            {
                ValueChange<string> valueChange = (ValueChange<string>)e;
                strSelectedRootColour = valueChange.Value.ToLower();

                if (rainbowRootCoroutine != null)
                {
                    MelonCoroutines.Stop(rainbowRootCoroutine);
                    rainbowRootCoroutine = null;
                }

                if (strSelectedRootColour == "vanilla")
                {
                    ResetRootColour();
                    return;
                }
                else if (strSelectedRootColour == "rainbow")
                {
                    rainbowRootCoroutine = MelonCoroutines.Start(RAINBOWROOTS());
                    return;
                }

                setSelectedRootColour(strSelectedRootColour);
                UpdateRootColour(selectedRootColour);
            }
        }

        // Update roots on material change
        private void OnRootMaterialChange(object sender = null, EventArgs e = null)
        {
            if (e != null)
            {
                ValueChange<string> valueChange = (ValueChange<string>)e;
                selectedRootMaterial = valueChange.Value.ToLower();

                if (selectedRootMaterial == "vanilla")
                {
                    ResetRootMaterial();
                    return;
                }

                MelonCoroutines.Start(UpdateRootMaterial(selectedRootMaterial));
            }
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
            }

            selectedLeafColour = stringToColour(colour.ToLower());
        }

        // This is actually really unnecessary
        private void setSelectedRootColour(string colour)
        {
            selectedRootColour = stringToColour(colour);
        }

        // You'll never guess what this one does
        public Color stringToColour(string colour)
        {
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
                MelonLogger.Error("Somehow not a valid colour");
                throw new Exception("Provided string is not in a valid format.");
            }
        }

        public void OnUIInit()
        {
            UI.instance.AddMod(RumbleTrees);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            // Reset all the variables
            leafObjects = new List<GameObject>();
            rootObjects = new List<GameObject>();
            VFXsObject = null;
            wasLightmapChanged = false;
            originalLeafColours = new Color[2];
            originalRootColours = new Color[2];
            originalLeafMaterial = null;
            originalRootMaterial = null;
            originalVFXColours = null;

            // Stop the rainbows to avoid errors and epilepsy
            if (rainbowLeafCoroutine != null)
            {
                MelonCoroutines.Stop(rainbowLeafCoroutine);
                rainbowLeafCoroutine = null;
            }
            if (rainbowRootCoroutine != null)
            {
                MelonCoroutines.Stop(rainbowRootCoroutine);
                rainbowRootCoroutine = null;
            }

            // Add the leaf and root objects
            if (sceneName == "Gym")
            {
                sceneID = 1;
                currentScene = sceneName;
                GameObject foliage = GameObject.Find(GymTrees[0]);
                for (int i = 0; i < foliage.transform.childCount; i++)
                {
                    GameObject child = foliage.transform.GetChild(i).gameObject;
                    if (child.active) leafObjects.Add(child);
                }
                leafObjects.Add(GameObject.Find(GymTrees[1]));
                leafObjects.Add(GameObject.Find(GymTrees[2]));
                VFXsObject = GameObject.Find(GymTrees[8]);

                GameObject roots = GameObject.Find(GymTrees[3]);
                for (int i = 0; i < roots.transform.childCount; i++)
                {
                    GameObject child = roots.transform.GetChild(i).gameObject;
                    if (child.name != "GymCompRoot") // Because for SOME REASON there is a random empty gameobject here
                        rootObjects.Add(child);
                }

                for (int i = 4; i < 8; i++)
                {
                    GameObject rootObject = GameObject.Find(GymTrees[i]);
                    rootObjects.Add(rootObject);
                }
            }

            if (sceneName == "Map0")
            {
                sceneID = 2;
                currentScene = "Ring";
                GameObject leaves = GameObject.Find(RingTrees[0]);
                leafObjects.Add(leaves);
                GameObject roots = GameObject.Find(RingTrees[1]);
                rootObjects.Add(roots);
            }

            if (sceneName == "Map1")
            {
                sceneID = 3;
                currentScene = "Pit";
                GameObject leaves = GameObject.Find(PitTrees[0]);
                leafObjects.Add(leaves);
                VFXsObject = GameObject.Find(PitTrees[1]);
            }

            if (sceneName == "Park")
            {
                sceneID = 4;
                currentScene = "Park";
                GameObject leaves = GameObject.Find(ParkTrees[0]);
                for (int i = 0; i < leaves.transform.childCount; i++)
                {
                    GameObject child = leaves.transform.GetChild(i).gameObject;
                    leafObjects.Add(child);
                }
                VFXsObject = GameObject.Find(ParkTrees[2]);

                GameObject roots = GameObject.Find(ParkTrees[1]);
                for (int i = 0; i < roots.transform.childCount; i++)
                {
                    GameObject child = roots.transform.GetChild(i).gameObject;
                    rootObjects.Add(child);
                }
            }

            if (sceneName == "Loader")
            {
                return;
            }

            // UPDATE EVERYTHING!!!
            enabled = (bool)RumbleTrees.Settings[sceneID].Value;
            selectedLeafMaterial = ((string) RumbleTrees.Settings[8].SavedValue).ToLower();
            if (enabled)
            {
                if (strSelectedLeafColour != "vanilla") UpdateLeafColour(selectedLeafColour);
                if (strSelectedRootColour != "vanilla") UpdateRootColour(selectedRootColour);
                if (selectedLeafMaterial != "vanilla") MelonCoroutines.Start(UpdateLeafMaterial(selectedLeafMaterial));
                if (selectedRootMaterial != "vanilla") MelonCoroutines.Start(UpdateRootMaterial(selectedRootMaterial));
                if (strSelectedLeafColour != "vanilla") InitLightmaps();
            }
            if (strSelectedLeafColour == "rainbow" && rainbowLeafCoroutine == null)
            {
                rainbowLeafCoroutine = MelonCoroutines.Start(RAINBOWLEAVES());
            }
            if (strSelectedRootColour == "rainbow" && rainbowRootCoroutine == null)
            {
                rainbowRootCoroutine = MelonCoroutines.Start(RAINBOWROOTS());
            }
        }

        // Updates all the lightmaps in one neat function
        private void InitLightmaps()
        {
            if (currentScene != "Loader" && currentScene != "Pit")
            {
                if (leafObjects.Count == 0) return;
                foreach (GameObject leafObject in leafObjects)
                {
                    MeshRenderer renderer = leafObject.GetComponent<MeshRenderer>();
                    if (!vanillaLightmaps) MelonCoroutines.Start(SwapLightmap(renderer, currentScene));
                }
            }
        }

        // I'm not gonna comment on these functions cause it should be obvious from the name
        private void UpdateLeafColour(Color colour)
        {
            if (!enabled) return;
            if (leafObjects.Count != 0) // This shouldn't be false but better safe than sorry
            {
                Color[] shades = new Color[2];
                Color.RGBToHSV(colour, out float hue, out float sat, out float val);

                sat = Math.Clamp(sat, 0.1f, 0.9f);
                val = Math.Clamp(val, 0.1f, 0.9f);

                // Generate new colours from the colour in order to make the bottom darker and the top lighter
                shades[0] = Color.HSVToRGB(hue, sat, val - 0.2f);
                shades[1] = Color.HSVToRGB(hue, sat, val + 0.1f);

                foreach (GameObject leafObject in leafObjects)
                {
                    MeshRenderer renderer = leafObject.GetComponent<MeshRenderer>();
                    Material material = renderer.material;

                    // Can't reset it later without saving it
                    if (originalLeafColours[0] == default)
                    {
                        originalLeafColours[0] = material.GetColor(LCT1);
                        originalLeafColours[1] = material.GetColor(LCB1);
                    }

                    // If the material is different we need to use the correct property
                    if (selectedLeafMaterial == "roots")
                    {
                        material.SetColor(RC1, shades[1]);
                        material.SetColor(RC2, shades[0]);
                    }
                    else
                    {
                        material.SetColor(LCT1, shades[1]);
                        material.SetColor(LCB1, shades[0]);
                    }
                }

                // If it's a shiftstone then the VFXs colour is handled in UpdateLeafMaterial, otherwise just make it the same
                if (selectedLeafMaterial == "vanilla" || selectedLeafMaterial == "roots")
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
                    Gradient gradient = leafVFX.GetGradient(FLG);

                    if (originalVFXColours == null)
                    {
                        originalVFXColours = gradient.colorKeys;
                    }

                    keys[0].color = colour;
                    keys[1].color = colour;

                    gradient.colorKeys = keys;

                    leafVFX.SetGradient(FLG, gradient);
                }
            }
            else if (currentScene != "Ring") // There aren't VFXs on ring so we don't need a warning
            {
                MelonLogger.Warning("Leaf VFX object not found!");
            }
        }

        // This function is pretty much the same as UpdateLeafColour
        private void UpdateRootColour(Color colour)
        {
            if (currentScene == "Loader" || currentScene == "Pit" || !enabled) return;
            if (rootObjects.Count != 0)
            {
                Color.RGBToHSV(colour, out float hue, out float sat, out float val);
                Color[] shades = new Color[3];

                sat = Math.Clamp(sat, 0.1f, 0.9f);
                val = Math.Clamp(val, 0.1f, 0.9f);

                shades[0] = Color.HSVToRGB(hue, sat - 0.1f, val - 0.3f);
                shades[1] = Color.HSVToRGB(hue, sat, val);

                foreach (GameObject rootObject in rootObjects)
                {
                    MeshRenderer renderer = rootObject.GetComponent<MeshRenderer>();
                    Material material = renderer.material;


                    if (material != null)
                    {
                        if (originalRootColours[0] == default)
                        {
                            originalRootColours[0] = material.GetColor(RC1);
                            originalRootColours[1] = material.GetColor(RC2);
                        }

                        if (selectedRootMaterial == "leaves")
                        {
                            material.SetColor(LCT1, shades[0]);
                            material.SetColor(LCB1, shades[1]);
                        }
                        else
                        {
                            material.SetColor(RC1, shades[0]);
                            material.SetColor(RC2, shades[1]);
                        }
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

            if (originalLeafColours[0] != default) // If the originalLeafColours aren't set then nothing was changed and we don't need to reset anything.
            {
                if (leafObjects.Count != 0)
                {
                    foreach (GameObject leafObject in leafObjects)
                    {
                        MeshRenderer renderer = leafObject.GetComponent<MeshRenderer>();
                        Material material = renderer.material;

                        if (selectedLeafMaterial == "roots")
                        {
                            material.SetColor(RC1, originalLeafColours[0]);
                            material.SetColor(RC2, originalLeafColours[1]);
                        }
                        else
                        {
                            material.SetColor(LCT1, originalLeafColours[0]);
                            material.SetColor(LCB1, originalLeafColours[1]);
                        }

                        MelonCoroutines.Start(SwapLightmap(renderer, currentScene, true));
                    }
                }
            }
            if (selectedLeafMaterial == "vanilla" || selectedLeafMaterial == "roots")
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
                        Gradient gradient = leafVFX.GetGradient(FLG);

                        gradient.colorKeys = originalVFXColours;

                        leafVFX.SetGradient(FLG, gradient);
                    }
                }
            }
        }

        private void ResetRootColour()
        {
            if (rainbowRootCoroutine != null)
            {
                MelonCoroutines.Stop(rainbowRootCoroutine);
                rainbowRootCoroutine = null;
            }
            if (originalRootColours[0] != default)
            {
                if (rootObjects.Count != 0)
                {
                    foreach (GameObject rootObject in rootObjects)
                    {
                        MeshRenderer renderer = rootObject.GetComponent<MeshRenderer>();
                        Material material = renderer.material;

                        if (selectedRootMaterial == "leaves")
                        {
                            material.SetColor(LCT1, originalRootColours[0]);
                            material.SetColor(LCB1, originalRootColours[1]);
                        }
                        else
                        {
                            material.SetColor(RC1, originalRootColours[0]);
                            material.SetColor(RC2, originalRootColours[1]);
                        }
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
                yield return new WaitForFixedUpdate(); // The entire reason this is a coroutine. Resetting doesn't work properly otherwise for some reason
                foreach (GameObject leafObject in leafObjects)
                {
                    MeshRenderer renderer = leafObject.GetComponent<MeshRenderer>();

                    if (originalLeafMaterial == null)
                    {
                        originalLeafMaterial = renderer.material;
                    }

                    if (materialName == "roots")
                    {
                        if (originalRootMaterial == null)
                        {
                            originalRootMaterial = rootObjects.First().GetComponent<MeshRenderer>().material; // Not for resetting pruposes, but so we can just use this variable no matter when the coroutine is run
                        }
                        renderer.material = originalRootMaterial;
                        UpdateLeafColour(selectedLeafColour);
                    }
                    else
                    {
                        renderer.material = stringToStone(materialName);
                        UpdateVFXs(renderer.material.color);
                    }
                }
            }
            yield break;
        }

        IEnumerator UpdateRootMaterial(string materialName)
        {
            if (currentScene == "Loader") yield break;
            if (!enabled) yield break;
            if (rootObjects.Count != 0)
            {
                yield return new WaitForFixedUpdate();
                foreach (GameObject rootObject in rootObjects)
                {
                    MeshRenderer renderer = rootObject.GetComponent<MeshRenderer>();

                    if (originalRootMaterial == null)
                    {
                        originalRootMaterial = renderer.material;
                    }

                    if (materialName == "leaves")
                    {
                        if (originalLeafMaterial == null)
                        {
                            originalLeafMaterial = leafObjects.First().GetComponent<MeshRenderer>().material;
                        }
                        renderer.material = originalLeafMaterial;
                        UpdateRootColour(selectedRootColour);
                    }
                    else renderer.material = stringToStone(materialName);
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
                if (enabled && strSelectedLeafColour != "vanilla")
                {
                    UpdateLeafColour(selectedLeafColour); // Because if the colour was changed it would've used the wrong property
                }
                else ResetLeafColour();
            }
        }

        private void ResetRootMaterial()
        {
            if (currentScene == "Loader") return;
            if (originalRootMaterial == null) return;
            if (rootObjects.Count != 0)
            {
                foreach (GameObject rootObject in rootObjects)
                {
                    MeshRenderer renderer = rootObject.GetComponent<MeshRenderer>();

                    renderer.material = originalRootMaterial;
                }
                if (enabled && strSelectedRootColour != "vanilla")
                {
                    UpdateRootColour(selectedRootColour);
                }
                else ResetRootColour();
            }
        }

        IEnumerator SwapLightmap(MeshRenderer renderer, string sceneName, bool original = false)
        {
            if (sceneName == "Pit") yield break;
            // Setting immediately on scene change doesn't change the lightmap index??
            yield return new WaitForFixedUpdate();

            if (!original && !wasLightmapChanged)
            {
                string lightmapName = sceneName.Trim() + "_Lightmap";
                Texture2D lightmap = LoadEmbeddedPNG(lightmapName);

                if (lightmap == null)
                {
                    MelonLogger.Error("Lightmap is null!");
                    yield break;
                }

                // Make a copy of the existing lightmap
                LightmapData[] oldLightmaps = LightmapSettings.lightmaps;
                LightmapData[] newLightmaps = new LightmapData[oldLightmaps.Length + 1];
                for (int i = 0; i < oldLightmaps.Length; i++)
                    newLightmaps[i] = oldLightmaps[i];

                // Swap in our greyscale lightmap
                LightmapData customLightmapData = new LightmapData
                {
                    lightmapColor = lightmap
                };
                newLightmaps[oldLightmaps.Length] = customLightmapData;

                LightmapSettings.lightmaps = newLightmaps;

                renderer.lightmapIndex = newLightmaps.Length - 1;
                renderer.lightmapScaleOffset = new Vector4(1, 1, 0, 0); // full map

                wasLightmapChanged = true;
            }
            else if (!original && wasLightmapChanged) // If we already added our lightmap we don't need to do it again
            {
                renderer.lightmapIndex = LightmapSettings.lightmaps.Length - 1;
            }
            else if (sceneName == "Gym")
            {
                renderer.lightmapIndex = 0;
            }
            else
            {
                renderer.lightmapIndex = 1;
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
                    rainbowHue += (int)RumbleTrees.Settings[10].SavedValue;
                    FrameCounter = 0;
                }
                FrameCounter++;
                yield return waitForFixedUpdate;
            }
        }

        IEnumerator RAINBOWROOTS()
        {
            WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
            int FrameCounter = 0;
            int rainbowHue = 0;
            while (true)
            {
                if (FrameCounter >= 2)
                {
                    if (rainbowHue >= 360) rainbowHue = 0;
                    selectedRootColour = Color.HSVToRGB(rainbowHue / 360f, 1f, 1f);
                    UpdateRootColour(selectedRootColour);
                    rainbowHue += (int)RumbleTrees.Settings[10].SavedValue;
                    FrameCounter = 0;
                }
                FrameCounter++;
                yield return waitForFixedUpdate;
            }
        }
    }
}