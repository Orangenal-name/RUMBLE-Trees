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
        public const string Version = "1.3.0";
    }

    public class Validation : ValidationParameters
    {
        private string[] themes = ["cherry", "orange", "yellow", "red"];

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
                if (themes.Contains(Input.ToLower())) return true;
                return false;
            }
        }
    }

    public class Core : MelonMod
    {
        internal static Color[] shades = new Color[3];
        internal static Color[] originalShades = new Color[3];
        private bool[] originalSaved = [false, false];
        private Color selectedColour;
        private Color cherryColour = new Color(0.86f, 0.54f, 0.9f, 1f);
        private Color orangeColour = new Color(1.0f, 0.44f, 0.0f, 1f);
        private Color yellowColour = new Color(1.0f, 0.78f, 0.0f, 1f);
        private Color redColour = new Color(0.66f, 0.0f, 0.0f, 1f);
        private int LCT1 = 1475; // Top of Leaves
        private int LCB1 = 1476; // Bottom of Leaves
        private int LCT2 = 1477; // Top of Leaves
        private int LCB2 = 1479; // Bottom of Leaves
        private int FLG = 1110; // Falling leaves gradient
        private Material leafMaterial;
        private List<MeshRenderer> renderers = new List<MeshRenderer>();
        private List<GameObject> leafObjects = new List<GameObject>();
        private GameObject VFXsObject;
        private int sceneID = -1;
        private bool wasSceneChanged = false;
        private bool wasLightmapChanged = false;
        private Texture2D lightmap = null;
        private Il2CppAssetBundle assetBundle = null;
        Mod RumbleTrees = new Mod();

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            LCT1 = Shader.PropertyToID("Color_133d236fee76457eb89bac53e692f8a3"); // Found in sharedassets3 path ID 2 (Material Root leave_Map0)
            LCB1 = Shader.PropertyToID("Color_6f942b46fda341409751e4e7f292de58"); // Ditto
            LCT2 = Shader.PropertyToID("Vector3_fa2c0c5c11884cdfbeb10e0460312f5c"); // Ditto
            LCB2 = Shader.PropertyToID("Color_d75e4f72dbfb4e7187f1c2d80558c414"); // Ditto
            FLG = Shader.PropertyToID("Leaf Color Gradient"); // Found in sharedassets0 path ID 371 (VisualEffectAsset Falling leaves)

            leafObjects = new List<GameObject>();
            renderers.Clear();
            VFXsObject = null;
            wasSceneChanged = false;
            wasLightmapChanged = false;

            if (sceneName == "Map0")
            {
                leafObjects.Add(GameObject.Find("Map0_production/Main static group/leave"));
                sceneID = 2;
                // MelonLogger.Msg($"Vector: {Shader.PropertyToID("Vector3_fa2c0c5c11884cdfbeb10e0460312f5c")}"); // idk what this is for but it's certainly there
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
                VFXsObject = GameObject.Find("--------------SCENE--------------/Lighting and effects/Visual Effects/Falling Leaf VFXs");
                sceneID = 3;
            }
            else if (sceneName == "Park")
            {
                leafObjects.Add(GameObject.Find("________________SCENE_________________/Park/Main static group/Leaves/Leave_sphere_park"));
                leafObjects.Add(GameObject.Find("________________SCENE_________________/Park/Main static group/Leaves/Leave_sphere_park_001"));
                leafObjects.Add(GameObject.Find("________________SCENE_________________/Park/Main static group/Leaves/Leave_sphere_park_002"));
                leafObjects.Add(GameObject.Find("________________SCENE_________________/Park/Main static group/Leaves/Leave_sphere_park_003"));
                VFXsObject = GameObject.Find("Lighting and effects/Visual Effects/Falling Leaf VFXs");
                sceneID = 4;
            }
            else
            {
                // Execute the code, or else...
                return;
            }

            if (!(bool)RumbleTrees.Settings[sceneID].SavedValue) return;

            UpdateColours();
            wasSceneChanged = true;
        }

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

            // Setup UI + Description
            RumbleTrees.ModName = "RumbleTrees";
            RumbleTrees.ModVersion = BuildInfo.Version;
            RumbleTrees.SetFolder("RumbleTrees");
            RumbleTrees.AddDescription("Description", "", "Make them pretty!\n\nCurrent presets:\nCherry\nOrange\nYellow\nRed", new Tags { IsSummary = true });

            RumbleTrees.AddToList("Enabled on Pit", true, 0, "Enables custom leaf colours on the pit map", new Tags());
            RumbleTrees.AddToList("Enabled on Ring", true, 0, "Enables custom leaf colours on the ring map", new Tags());
            RumbleTrees.AddToList("Enabled in Gym", true, 0, "Enables custom leaf colours in the gym", new Tags());
            RumbleTrees.AddToList("Enabled in Parks", true, 0, "Enables custom leaf colours in parks", new Tags());
            RumbleTrees.AddToList("Legacy shaders", false, 0, "Enables the vanilla lightmaps in Ring and Parks, which look different and don't work properly with all colours", new Tags());

            RumbleTrees.AddToList("Colour", "Cherry", "Type in either a preset name or a custom colour in one of the supported formats: \n255 255 255\nFFFFFF", new Tags());

            RumbleTrees.AddValidation("Colour", new Validation());

            RumbleTrees.GetFromFile();
            RumbleTrees.ModSaved += OnSave;
            RumbleTrees.Settings[5].SavedValueChanged += OnLegacyChange;
            RumbleTrees.Settings[6].SavedValueChanged += OnColourChange;
            UI.instance.UI_Initialized += OnUIInit;

            // Set the selected colour without updating cause we're not in a valid scene right now
            string colour = (string)RumbleTrees.Settings[6].SavedValue;
            if (checkCustom(colour))
                setCustom(colour);
            else setSelectedColour(colour);
        }

        public void setCustom(string input)
        {
            if (input.Contains(" "))
            {
                var parts = input.Split(' ');
                if (parts.Length == 3 &&
                    byte.TryParse(parts[0], out byte rByte) &&
                    byte.TryParse(parts[1], out byte gByte) &&
                    byte.TryParse(parts[2], out byte bByte))
                {
                    selectedColour = new Color(rByte / 255f, gByte / 255f, bByte / 255f);
                }
            }

            if (input.Length == 6 &&
                int.TryParse(input, System.Globalization.NumberStyles.HexNumber, null, out int hex))
            {
                float r = ((hex >> 16) & 0xFF) / 255f;
                float g = ((hex >> 8) & 0xFF) / 255f;
                float b = (hex & 0xFF) / 255f;
                selectedColour = new Color(r, g, b);
            }
        }

        public void setSelectedColour(string colour, bool custom = false)
        {
            switch (colour.ToLower())
            {
                case "cherry":
                    selectedColour = cherryColour;
                    break;
                case "orange":
                    selectedColour = orangeColour;
                    break;
                case "yellow":
                    selectedColour = yellowColour;
                    break;
                case "red":
                    selectedColour = redColour;
                    break;
            }
        }

        public void OnSave()
        {
            if ((bool)RumbleTrees.Settings[sceneID].SavedValue != wasSceneChanged)
            {
                UpdateColours(wasSceneChanged);
                if (sceneID == 2 || sceneID == 4)
                {
                    MelonCoroutines.Start(SwapLightmap(wasSceneChanged || (bool)RumbleTrees.Settings[5].Value));
                }
                wasSceneChanged = !wasSceneChanged;
            }
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

        public void OnColourChange(object sender = null, EventArgs e = null)
        {
            string newColour = ((ValueChange<string>)e).Value;

            if (checkCustom(newColour))
                setCustom(newColour);
            else setSelectedColour(newColour);

            UpdateColours();
        }

        public void OnLegacyChange(object sender = null, EventArgs e = null)
        {
            if (sceneID == 2 || sceneID == 4)
                MelonCoroutines.Start(SwapLightmap(((ValueChange<bool>)e).Value));
        }

        public void OnUIInit()
        {
            UI.instance.AddMod(RumbleTrees);
        }

        IEnumerator SwapLightmap(bool legacy = false)
        {
            yield return new WaitForSeconds(0.2f);

            if (!legacy && !wasLightmapChanged)
            {
                lightmap = LoadAsset();
                if (lightmap == null)
                {
                    MelonLogger.Error("Lightmap is null!");
                    yield break;
                }

                LightmapData[] oldLightmaps = LightmapSettings.lightmaps;
                LightmapData[] newLightmaps = new LightmapData[oldLightmaps.Length + 1];
                for (int i = 0; i < oldLightmaps.Length; i++)
                    newLightmaps[i] = oldLightmaps[i];

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
            else if (!legacy && wasLightmapChanged)
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

        private void UpdateColours(bool reset = false)
        {
            if (leafObjects.Count != 0)
            {
                foreach (GameObject leafObject in leafObjects)
                {
                    MeshRenderer renderer = leafObject.GetComponent<MeshRenderer>();
                    renderers.Add(renderer);
                    leafMaterial = renderer.material;

                    if (reset)
                    {
                        leafMaterial.SetColor(LCT1, originalShades[2]);
                        leafMaterial.SetColor(LCB1, originalShades[0]);
                        leafMaterial.SetColor(LCT2, originalShades[2]);
                        leafMaterial.SetColor(LCB2, originalShades[0]);

                        continue;
                    }

                    Color.RGBToHSV(selectedColour, out float hue, out float sat, out float val);

                    if (sat > 0.9f) sat = 0.9f;
                    if (sat < 0.1f) sat = 0.1f;
                    if (val > 0.9f) val = 0.9f;
                    if (val < 0.1f) val = 0.1f;

                    shades[0] = Color.HSVToRGB(hue, sat - 0.1f, val - 0.1f);
                    shades[1] = Color.HSVToRGB(hue, sat, val);
                    shades[2] = Color.HSVToRGB(hue, sat + 0.1f, val + 0.1f);

                    if (leafMaterial != null)
                    {
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
                        // MelonLogger.Msg($"Resulting colour: {material.GetColor(LCT2)}");
                    }
                }

                if ((sceneID == 2 || sceneID == 4) && !(bool)RumbleTrees.Settings[5].Value)
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

                    if (!originalSaved[1])
                    {
                        originalShades[1] = gradient.colorKeys[0].color;
                        originalSaved[1] = true;
                    }

                    keys[0].color = shades[1]; keys[1].color = shades[1];
                    gradient.colorKeys = keys;

                    if (reset)
                    {
                        keys[0].color = originalShades[1]; keys[1].color = originalShades[1];
                        gradient.colorKeys = keys;
                    }
                    leafVFX.SetGradient(FLG, gradient);
                }
            }
            else if (sceneID == 0 || sceneID == 3)
            {
                MelonLogger.Warning("Leaf VFX object not found!");
            }
        }
    }
}