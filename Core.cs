using MelonLoader;
using UnityEngine;
using UnityEngine.VFX;
using RumbleModUI;
using BuildInfo = RUMBLECherryBlossoms.BuildInfo;
using System.Collections;
using System.Text.RegularExpressions;

[assembly: MelonInfo(typeof(RUMBLECherryBlossoms.Core), "RumbleTrees", BuildInfo.Version, "Orangenal", null)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]
[assembly: MelonColor(255, 219, 138, 230)]

namespace RUMBLECherryBlossoms
{
    public static class BuildInfo
    {
        public const string Version = "1.4.0";
    }

    public class Validation : ValidationParameters
    {
        private string[] themes = ["cherry", "orange", "yellow", "red", "rainbow", "vanilla"];
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
                return true;
            }
            else if (Regex.IsMatch(Input, hexPattern))
            {
                return true;
            }
            else // Must be a preset
            {
                if (themes.Contains(Input.ToLower()) && type == "leaf") return true;
                if (Input.ToLower() == "rainbow" && type == "root") return true;
                if (Input.ToLower() == "vanilla" && type == "root") return true;
                return false;
            }
        }
    }

    public class Core : MelonMod
    {
        internal static Color[] shades = new Color[3];
        internal static Color[] originalShades = new Color[5];
        private bool[] originalSaved = [false, false, false];
        private Color selectedLeafColour;
        private Color selectedRootColour;
        private Color cherryColour = new Color(0.86f, 0.54f, 0.9f, 1f);
        private Color orangeColour = new Color(1.0f, 0.44f, 0.0f, 1f);
        private Color yellowColour = new Color(1.0f, 0.78f, 0.0f, 1f);
        private Color redColour = new Color(0.66f, 0.0f, 0.0f, 1f);
        private int LCT1 = 1475; // Top of Leaves
        private int LCB1 = 1476; // Bottom of Leaves
        private int LCT2 = 1477; // Top of Leaves
        private int LCB2 = 1479; // Bottom of Leaves
        private int FLG = 1110; // Falling leaves gradient
        private int RC1 = 1203;
        private int RC2 = 1303;
        private bool gotPropertyIDs = false;
        private Material leafMaterial;
        private Material rootMaterial;
        private List<MeshRenderer> renderers = new List<MeshRenderer>();
        private List<GameObject> leafObjects = new List<GameObject>();
        private List<GameObject> rootObjects = new List<GameObject>();
        private GameObject VFXsObject;
        private int sceneID = -1;
        private bool wasSceneChanged = false;
        private bool wasLightmapChanged = false;
        private Texture2D lightmap = null;
        private Il2CppAssetBundle assetBundle = null;
        private int rainbowHue = 0;
        private object rainbowCoroutine;
        private object rainbowRootCoroutine;
        private bool isRainbow = false;
        private bool isRainbowRoot = false;
        private bool leavesEnabled = true;
        private bool rootsEnabled = false;
        Mod RumbleTrees = new Mod();

        // Code for loading custom lightmaps (they're just desaturated versions of the default ones)
        private Texture2D LoadAsset()
        {
            if (assetBundle == null)
            {
                using (Stream bundleStream = MelonAssembly.Assembly.GetManifestResourceStream("RUMBLECherryBlossoms.Resources.rumbletrees"))
                {
                    if (bundleStream == null)
                    {
                        MelonLogger.Error("Failed to find resource stream!");
                        return null;
                    }

                    byte[] bundleBytes = new byte[bundleStream.Length];
                    bundleStream.Read(bundleBytes, 0, bundleBytes.Length);
                    assetBundle = Il2CppAssetBundleManager.LoadFromMemory(bundleBytes);
                }
            }

            if (assetBundle == null)
            {
                MelonLogger.Error("AssetBundle failed to load.");
                return null;
            }
            Texture2D asset;
            if (sceneID == 2)
            {
                asset = assetBundle.LoadAsset<Texture2D>("MAP0_final");
            }
            else if (sceneID == 4)
            {
                asset = assetBundle.LoadAsset<Texture2D>("park0_final");
            }
            else
            {
                MelonLogger.Error("Wrong scene!");
                return null;
            }

            if (asset == null)
                MelonLogger.Error("Failed to load texture from AssetBundle!");
            else
                asset.Apply(true, false); // Make sure it's marked readable

            return asset;
        }

        public override void OnLateInitializeMelon()
        {
            base.OnLateInitializeMelon();

            // Setup ModUI settings & Description
            RumbleTrees.ModName = "RumbleTrees";
            RumbleTrees.ModVersion = BuildInfo.Version;
            RumbleTrees.SetFolder("RumbleTrees");
            RumbleTrees.AddDescription("Description", "", "Make them pretty!\n\nCurrent presets:\nCherry\nOrange\nYellow\nRed\nRainbow\nVanilla (literally does nothing)", new Tags { IsSummary = true });

            RumbleTrees.AddToList("Enabled on Pit", true, 0, "Enables custom leaf colours on the pit map", new Tags());
            RumbleTrees.AddToList("Enabled on Ring", true, 0, "Enables custom leaf colours on the ring map", new Tags());
            RumbleTrees.AddToList("Enabled in Gym", true, 0, "Enables custom leaf colours in the gym", new Tags());
            RumbleTrees.AddToList("Enabled in Parks", true, 0, "Enables custom leaf colours in parks", new Tags());
            RumbleTrees.AddToList("Legacy shaders", false, 0, "Enables the vanilla lightmaps in Ring and Parks, which look different and don't work properly with all colours", new Tags());

            RumbleTrees.AddToList("Leaf colour", "Cherry", "Type in either a preset name or a custom colour in one of the supported formats: \n255 255 255\nFFFFFF", new Tags());
            RumbleTrees.AddToList("Root colour", "FFFFFF", "Type in either \"Rainbow,\" \"Vanilla,\" or a custom colour in one of the supported formats: \n255 255 255\nFFFFFF", new Tags());

            RumbleTrees.AddToList("Rainbow speed", 1, "The speed of rainbow leaves (if selected)", new Tags());

            RumbleTrees.AddValidation("Leaf colour", new Validation("leaf"));
            RumbleTrees.AddValidation("Root colour", new Validation("root"));

            RumbleTrees.GetFromFile();

            // These three are for updating the colour immediately instead of waiting for the next scene load
            RumbleTrees.ModSaved += OnSave;
            RumbleTrees.Settings[5].SavedValueChanged += OnLegacyChange;
            RumbleTrees.Settings[6].SavedValueChanged += OnColourChange;
            RumbleTrees.Settings[7].SavedValueChanged += OnRootChange;

            UI.instance.UI_Initialized += OnUIInit;

            // Set the selected colour without updating cause we're not in a valid scene right now
            string colour = (string)RumbleTrees.Settings[6].SavedValue;
            if (checkCustom(colour))
                setCustom(colour, "leaves");
            else setSelectedColour(colour);

            string rootColour = (string)RumbleTrees.Settings[7].SavedValue;
            setCustom(rootColour, "roots");
        }

        public void OnColourChange(object sender = null, EventArgs e = null)
        {
            string newColour = ((ValueChange<string>)e).Value;

            if (checkCustom(newColour))
                setCustom(newColour, "leaves");
            else setSelectedColour(newColour);

            UpdateColours(type:"leaves");
        }

        public void OnRootChange(object sender = null, EventArgs e = null)
        {
            string newColour = ((ValueChange<string>)e).Value;
            setCustom(newColour, "roots");
            UpdateColours(type:"roots");
        }

        public void OnLegacyChange(object sender = null, EventArgs e = null)
        {
            if (sceneID == 2 || sceneID == 4 && leavesEnabled)
                MelonCoroutines.Start(SwapLightmap(((ValueChange<bool>)e).Value));
        }

        public void OnSave()
        {
            if ((bool)RumbleTrees.Settings[sceneID].SavedValue != wasSceneChanged)
            {
                UpdateColours(wasSceneChanged);
                
                if (!wasSceneChanged && isRainbow)
                {
                    rainbowCoroutine = MelonCoroutines.Start(RAINBOW());
                }
                if (!wasSceneChanged && isRainbowRoot)
                {
                    rainbowRootCoroutine = MelonCoroutines.Start(RAINBOWROOT());
                }
                if ((sceneID == 2 || sceneID == 4) && leavesEnabled)
                {
                    MelonCoroutines.Start(SwapLightmap(wasSceneChanged || (bool)RumbleTrees.Settings[5].Value));
                }
                wasSceneChanged = !wasSceneChanged;
            }
        }

        public void OnUIInit()
        {
            UI.instance.AddMod(RumbleTrees);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            // Hardcoding the IDs seemed to change depending on certain conditions so doing it this way makes it always work
            if (!gotPropertyIDs)
            {
                LCT1 = Shader.PropertyToID("Color_133d236fee76457eb89bac53e692f8a3"); // Found in sharedassets3 path ID 2 (Material Root leave_Map0)
                LCB1 = Shader.PropertyToID("Color_6f942b46fda341409751e4e7f292de58"); // Ditto
                LCT2 = Shader.PropertyToID("Vector3_fa2c0c5c11884cdfbeb10e0460312f5c"); // Ditto
                LCB2 = Shader.PropertyToID("Color_d75e4f72dbfb4e7187f1c2d80558c414"); // Ditto
                FLG = Shader.PropertyToID("Leaf Color Gradient"); // Found in sharedassets0 path ID 371 (VisualEffectAsset Falling leaves)
                RC1 = Shader.PropertyToID("Color_FA790384"); // Found in Material Rumble root
                RC2 = Shader.PropertyToID("Color_c7120f3b741f4dd48575e89d95f9641d"); // Ditto
                gotPropertyIDs = true;
            }

            leafObjects = new List<GameObject>();
            rootObjects = new List<GameObject>();
            renderers.Clear();
            VFXsObject = null;
            wasSceneChanged = false;
            wasLightmapChanged = false;

            if (sceneName == "Map0")
            {
                leafObjects.Add(GameObject.Find("Map0_production/Main static group/leave"));
                rootObjects.Add(GameObject.Find("Map0_production/Main static group/Root"));
                sceneID = 2;
            }
            else if (sceneName == "Map1")
            {
                leafObjects.Add(GameObject.Find("Map1_production/Main static group/Leaves_Map2"));
                VFXsObject = GameObject.Find("Lighting & Effects/Visual Effects/Falling Leaf VFXs");
                sceneID = 1;
            }
            else if (sceneName == "Gym")
            {
                leafObjects.Add(GameObject.Find("--------------SCENE--------------/Gym_Production/Main static group/Foliage/Root_leaves (1)"));
                leafObjects.Add(GameObject.Find("--------------SCENE--------------/Gym_Production/Main static group/Foliage/Root_leaves_001 (1)"));
                leafObjects.Add(GameObject.Find("--------------SCENE--------------/Gym_Production/Main static group/Foliage/Root_leaves_002 (1)"));
                leafObjects.Add(GameObject.Find("--------------SCENE--------------/Gym_Production/Main static group/Foliage/Root_leaves_003 (1)"));
                leafObjects.Add(GameObject.Find("--------------SCENE--------------/Gym_Production/Main static group/Gymarena/Leave_sphere__23_"));
                leafObjects.Add(GameObject.Find("--------------SCENE--------------/Gym_Production/Main static group/Gymarena/Leave_sphere__24_"));

                rootObjects.Add(GameObject.Find("--------------SCENE--------------/Gym_Production/Sub static group/Scene_roots/Test_root_1_middetail/Cylinder_014__6_"));
                rootObjects.Add(GameObject.Find("--------------SCENE--------------/Gym_Production/Sub static group/Scene_roots/Test_root_1_middetail/Cylinder_015__1_"));
                rootObjects.Add(GameObject.Find("--------------SCENE--------------/Gym_Production/Sub static group/Scene_roots/Test_root_1_middetail/Cylinder_015__4__1"));
                rootObjects.Add(GameObject.Find("--------------SCENE--------------/Gym_Production/Sub static group/Scene_roots/Test_root_1_middetail/Cylinder_018__2_"));
                rootObjects.Add(GameObject.Find("--------------SCENE--------------/Gym_Production/Sub static group(buildings)/Rumble_station/Root"));
                rootObjects.Add(GameObject.Find("--------------SCENE--------------/Gym_Production/Sub static group(buildings)/School/Cylinder_011"));
                rootObjects.Add(GameObject.Find("--------------SCENE--------------/Gym_Production/Main static group/Gymarena/Cylinder_015__4_"));

                VFXsObject = GameObject.Find("--------------SCENE--------------/Lighting and effects/Visual Effects/Falling Leaf VFXs");
                sceneID = 3;
            }
            else if (sceneName == "Park")
            {
                leafObjects.Add(GameObject.Find("________________SCENE_________________/Park/Main static group/Leaves/Leave_sphere_park"));
                leafObjects.Add(GameObject.Find("________________SCENE_________________/Park/Main static group/Leaves/Leave_sphere_park_001"));
                leafObjects.Add(GameObject.Find("________________SCENE_________________/Park/Main static group/Leaves/Leave_sphere_park_002"));
                leafObjects.Add(GameObject.Find("________________SCENE_________________/Park/Main static group/Leaves/Leave_sphere_park_003"));

                GameObject Roots = GameObject.Find("________________SCENE_________________/Park/Main static group/Root/");
                for (int i = 0; i < Roots.transform.childCount; i++) {
                    rootObjects.Add(Roots.transform.GetChild(i).gameObject);
                }

                VFXsObject = GameObject.Find("Lighting and effects/Visual Effects/Falling Leaf VFXs");
                sceneID = 4;
            }
            else
            {
                // Execute the code, or else...
                return; // (This only prevents errors in the loader)
            }

            if (!(bool)RumbleTrees.Settings[sceneID].SavedValue) return;

            UpdateColours();
            if (isRainbow)
            {
                rainbowCoroutine = MelonCoroutines.Start(RAINBOW());
            }
            if (isRainbowRoot)
            {
                rainbowRootCoroutine = MelonCoroutines.Start(RAINBOWROOT());
            }
            wasSceneChanged = true;
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            if (rainbowCoroutine != null) MelonCoroutines.Stop(rainbowCoroutine);
            if (rainbowRootCoroutine != null) MelonCoroutines.Stop(rainbowRootCoroutine);
        }

        public bool checkCustom(string Input)
        {
            string rgbPattern = @"^(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\s(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\s(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)$";
            string hexPattern = @"^#?[0-9A-Fa-f]{6}$";

            if (Regex.IsMatch(Input, rgbPattern))
            {
                return true;
            }
            else if (Regex.IsMatch(Input, hexPattern))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void setCustom(string input, string type)
        {
            rootsEnabled = true;
            if (rainbowRootCoroutine != null) MelonCoroutines.Stop(rainbowRootCoroutine);
            isRainbowRoot = false;
            if (input.ToLower() == "rainbow" && type == "roots")
            {
                isRainbowRoot = true;
                if (sceneID != -1)
                {
                    rainbowRootCoroutine = MelonCoroutines.Start(RAINBOWROOT());
                }
                return;
            }
            else if (input.ToLower() == "vanilla" && type == "roots")
            {
                rootsEnabled = false;
                return;
            }
            Color colour;
            if (input.Contains(" "))
            {
                string[] parts = input.Split(' ');
                if (parts.Length == 3 &&
                    byte.TryParse(parts[0], out byte rByte) &&
                    byte.TryParse(parts[1], out byte gByte) &&
                    byte.TryParse(parts[2], out byte bByte))
                {
                    colour = new Color(rByte / 255f, gByte / 255f, bByte / 255f);
                }
                else
                {
                    MelonLogger.Error("Somehow not a valid colour");
                    return;
                }
            }
            else if (input.Length == 6 &&
                int.TryParse(input, System.Globalization.NumberStyles.HexNumber, null, out int hex))
            {
                float r = ((hex >> 16) & 0xFF) / 255f;
                float g = ((hex >> 8) & 0xFF) / 255f;
                float b = (hex & 0xFF) / 255f;
                colour = new Color(r, g, b);
            }
            else
            {
                MelonLogger.Error("Somehow not a valid colour");
                return;
            }

            if (type == "leaves")
            {
                selectedLeafColour = colour;
            }
            else
            {
                selectedRootColour = colour;
            }
        }

        public void setSelectedColour(string colour, bool custom = false)
        {
            if (rainbowCoroutine != null) MelonCoroutines.Stop(rainbowCoroutine);
            isRainbow = false;

            if (!leavesEnabled && (sceneID == 2 || sceneID == 4))
            {
                MelonCoroutines.Start(SwapLightmap());
            }
            leavesEnabled = true;

            switch (colour.ToLower())
            {
                case "cherry":
                    selectedLeafColour = cherryColour;
                    break;
                case "orange":
                    selectedLeafColour = orangeColour;
                    break;
                case "yellow":
                    selectedLeafColour = yellowColour;
                    break;
                case "red":
                    selectedLeafColour = redColour;
                    break;
                case "rainbow":
                    isRainbow = true;
                    if (sceneID != -1)
                    {
                        rainbowCoroutine = MelonCoroutines.Start(RAINBOW());
                    }
                    break;
                case "vanilla":
                    leavesEnabled = false;
                    if (isRainbow) isRainbow = false;
                    if (rainbowCoroutine != null)
                    {
                        MelonCoroutines.Stop(rainbowCoroutine);
                    }
                    if (sceneID == 2 || sceneID == 4)
                    {
                        MelonCoroutines.Start(SwapLightmap(true));
                    }
                    break;
            }
        }

        IEnumerator RAINBOW()
        {
            WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
            int speed = (int)RumbleTrees.Settings[8].SavedValue;
            int FrameCounter = 10/(speed*10)-1;
            while (true)
            {
                if (FrameCounter >= 2)
                {
                    if (rainbowHue >= 360) rainbowHue = 0;
                    selectedLeafColour = Color.HSVToRGB(rainbowHue / 360f, 1f, 1f);
                    UpdateColours(type:"leaves");
                    if (speed > 10) rainbowHue += speed / 10;
                    rainbowHue++;
                    FrameCounter = 0;
                }
                FrameCounter++;
                yield return waitForFixedUpdate;
            }
        }

        IEnumerator RAINBOWROOT()
        {
            WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
            int speed = (int)RumbleTrees.Settings[8].SavedValue;
            int FrameCounter = 10 / (speed * 10) - 1;
            while (true)
            {
                if (FrameCounter >= 2)
                {
                    if (rainbowHue >= 360) rainbowHue = 0;
                    selectedRootColour = Color.HSVToRGB(rainbowHue / 360f, 1f, 1f);
                    UpdateColours(type: "roots");
                    if (speed > 10) rainbowHue += speed / 10;
                    rainbowHue++;
                    FrameCounter = 0;
                }
                FrameCounter++;
                yield return waitForFixedUpdate;
            }
        }

        IEnumerator SwapLightmap(bool legacy = false)
        {
            // Setting immediately on scene change doesn't change the lightmap index??
            yield return new WaitForSeconds(0.2f);

            if (!legacy && !wasLightmapChanged)
            {
                lightmap = LoadAsset();
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

                foreach (MeshRenderer r in renderers)
                {
                    r.lightmapIndex = newLightmaps.Length - 1;
                    r.lightmapScaleOffset = new Vector4(1, 1, 0, 0); // full map
                }
                wasLightmapChanged = true;
            }
            else if (!legacy && wasLightmapChanged) // If we already added our lightmap we don't need to do it again
            {
                foreach (MeshRenderer r in renderers)
                {
                    r.lightmapIndex = 2;
                }
            }
            else
            {
                foreach (MeshRenderer r in renderers)
                {
                    r.lightmapIndex = 1;
                }
            }
        }

        private void UpdateColours(bool reset = false, string type = "all")
        {
            if (type == "leaves" || type == "all")
            {
                if (leafObjects.Count != 0)
                {
                    foreach (GameObject leafObject in leafObjects)
                    {
                        MeshRenderer renderer = leafObject.GetComponent<MeshRenderer>();
                        renderers.Add(renderer);
                        leafMaterial = renderer.material;

                        if (reset || !leavesEnabled)
                        {
                            if (!originalSaved[0]) continue;
                            if (isRainbow)
                            {
                                if (rainbowCoroutine != null) MelonCoroutines.Stop(rainbowCoroutine);
                            }
                            leafMaterial.SetColor(LCT1, originalShades[2]);
                            leafMaterial.SetColor(LCB1, originalShades[0]);
                            leafMaterial.SetColor(LCT2, originalShades[2]);
                            leafMaterial.SetColor(LCB2, originalShades[0]);

                            continue;
                        }

                        Color.RGBToHSV(selectedLeafColour, out float hue, out float sat, out float val);

                        if (sat > 0.9f) sat = 0.9f;
                        if (sat < 0.1f) sat = 0.1f;
                        if (val > 0.9f) val = 0.9f;
                        if (val < 0.1f) val = 0.1f;

                        shades[0] = Color.HSVToRGB(hue, sat - 0.1f, val - 0.1f);
                        shades[1] = Color.HSVToRGB(hue, sat, val);
                        shades[2] = Color.HSVToRGB(hue, sat + 0.1f, val + 0.1f);

                        if (leafMaterial != null)
                        {
                            // Save the original tree shades so we can reset it if needed
                            if (!originalSaved[0])
                            {
                                originalShades[2] = leafMaterial.GetColor(LCT1);
                                originalShades[0] = leafMaterial.GetColor(LCB1);
                                originalSaved[0] = true;
                            }
                            leafMaterial.SetColor(LCT1, shades[2]);
                            leafMaterial.SetColor(LCB1, shades[0]);
                            leafMaterial.SetColor(LCT2, shades[2]);
                            if (sceneID != 3) leafMaterial.SetColor(LCB2, shades[0]);
                        }
                    }

                    if ((sceneID == 2 || sceneID == 4) && !(bool)RumbleTrees.Settings[5].SavedValue && leavesEnabled)
                    {
                        MelonCoroutines.Start(SwapLightmap());
                    }
                }
                else
                {
                    MelonLogger.Warning("Leaf object not found!");
                }

                if (VFXsObject != null)
                {
                    VisualEffect leafVFX;
                    for (int i = 0; i < VFXsObject.transform.GetChildCount(); i++)
                    {
                        leafVFX = VFXsObject.transform.GetChild(i).gameObject.GetComponent<VisualEffect>();
                        GradientColorKey[] keys = new GradientColorKey[2];
                        Gradient gradient = leafVFX.GetGradient(FLG);

                        if (reset || !leavesEnabled)
                        {
                            if (!originalSaved[1]) continue;

                            keys[0].color = originalShades[1]; keys[1].color = originalShades[1];
                            gradient.colorKeys = keys;
                            leafVFX.SetGradient(FLG, gradient);
                            continue;
                        }

                        if (!originalSaved[1])
                        {
                            originalShades[1] = gradient.colorKeys[0].color;
                            originalSaved[1] = true;
                        }

                        keys[0].color = shades[1]; keys[1].color = shades[1];
                        gradient.colorKeys = keys;

                        leafVFX.SetGradient(FLG, gradient);
                    }
                }
                else if (sceneID == 0 || sceneID == 3)
                {
                    MelonLogger.Warning("Leaf VFX object not found!");
                }
            }
            if ((type == "roots" || type == "all") && sceneID != 1)
            {
                // Set root colours
                if (rootObjects.Count != 0)
                {
                    foreach (GameObject rootObject in rootObjects)
                    {
                        MeshRenderer renderer = rootObject.GetComponent<MeshRenderer>();
                        rootMaterial = renderer.material;
                        if (reset || !rootsEnabled)
                        {
                            if (!originalSaved[2]) continue;
                            if (isRainbowRoot)
                            {
                                if (rainbowRootCoroutine != null) MelonCoroutines.Stop(rainbowRootCoroutine);
                            }
                            rootMaterial.SetColor(RC1, originalShades[3]);
                            rootMaterial.SetColor(RC2, originalShades[4]);

                            continue;
                        }

                        if (rootMaterial != null)
                        {
                            // Save the original tree shades so we can reset it if needed
                            if (!originalSaved[2])
                            {
                                originalShades[3] = rootMaterial.GetColor(RC1);
                                originalShades[4] = rootMaterial.GetColor(RC2);
                                originalSaved[2] = true;
                            }

                            Color.RGBToHSV(selectedRootColour, out float hue, out float sat, out float val);

                            if (sat > 0.9f) sat = 0.9f;
                            if (sat < 0.1f) sat = 0.1f;
                            if (val > 0.9f) val = 0.9f;
                            if (val < 0.1f) val = 0.1f;

                            shades[0] = Color.HSVToRGB(hue, sat - 0.1f, val - 0.3f);
                            shades[1] = Color.HSVToRGB(hue, sat, val);

                            rootMaterial.SetColor(RC1, shades[0]);
                            rootMaterial.SetColor(RC2, shades[1]);
                        }
                    }
                }
            }
        }
    }
}
