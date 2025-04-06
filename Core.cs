using MelonLoader;
using UnityEngine;
using UnityEngine.VFX;
using RumbleModUI;
using BuildInfo = RUMBLECherryBlossoms.BuildInfo;

[assembly: MelonInfo(typeof(RUMBLECherryBlossoms.Core), "RumbleTrees", BuildInfo.Version, "Orangenal", null)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]
[assembly: MelonColor(255, 219, 138, 230)]

namespace RUMBLECherryBlossoms
{
    public static class BuildInfo
    {
        public const string Version = "1.1.0";
    }

    public class Core : MelonMod
    {
        internal static Color[] shades = new Color[3];
        internal static Color[] originalShades = new Color[3];
        private bool[] originalSaved = [false, false];
        private Color cherryColour = new Color(0.86f, 0.54f, 0.9f, 1f);
        private int LCT1 = 1475; // Top of Leaves
        private int LCB1 = 1476; // Bottom of Leaves
        private int LCT2 = 1477; // Top of Leaves
        private int LCB2 = 1479; // Bottom of Leaves
        private int FLG = 1110; // Falling leaves gradient
        private Material leafMaterial;
        private MeshRenderer renderer;
        private List<GameObject> leafObjects = new List<GameObject>();
        private GameObject VFXsObject;
        private int sceneID = -1;
        private bool wasSceneChanged = false;
        Mod RumbleTrees = new Mod();

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            LCT1 = Shader.PropertyToID("Color_133d236fee76457eb89bac53e692f8a3"); // Found in sharedassets3 path ID 2 (Material Root leave_Map0)
            LCB1 = Shader.PropertyToID("Color_6f942b46fda341409751e4e7f292de58"); // Ditto
            LCT2 = Shader.PropertyToID("Color_703ec75398a14bf19c79ac19fa909a6f"); // Ditto
            LCB2 = Shader.PropertyToID("Color_d75e4f72dbfb4e7187f1c2d80558c414"); // Ditto
            FLG = Shader.PropertyToID("Leaf Color Gradient"); // Found in sharedassets0 path ID 371 (VisualEffectAsset Falling leaves)

            leafObjects = new List<GameObject>();
            VFXsObject = null;
            wasSceneChanged = false;

            if (sceneName == "Map0")
            {
                leafObjects.Add(GameObject.Find("Map0_production/Main static group/leave"));
                sceneID = 2;
                // MelonLogger.Msg($"Colour 3: {Shader.PropertyToID("Color_703ec75398a14bf19c79ac19fa909a6f")}"); // The alpha is 0??
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

            UpdateColours(cherryColour);
            wasSceneChanged = true;
        }

        public override void OnLateInitializeMelon()
        {
            base.OnLateInitializeMelon();

            // Setup UI + Description
            RumbleTrees.ModName = "RumbleTrees";
            RumbleTrees.ModVersion = BuildInfo.Version;
            RumbleTrees.SetFolder("RumbleTrees");
            RumbleTrees.AddDescription("Description", "", "Make them pretty!", new Tags { IsSummary = true });

            RumbleTrees.AddToList("Enabled on Pit", true, 0, "Enables custom leaf colours on the pit map", new Tags());
            RumbleTrees.AddToList("Enabled on Ring", true, 0, "Enables custom leaf colours on the ring map", new Tags());
            RumbleTrees.AddToList("Enabled in Gym", true, 0, "Enables custom leaf colours in the gym", new Tags());
            RumbleTrees.AddToList("Enabled in Parks", true, 0, "Enables custom leaf colours in parks", new Tags());

            RumbleTrees.GetFromFile();
            RumbleTrees.ModSaved += OnSave;
            UI.instance.UI_Initialized += OnUIInit;
        }

        public void OnSave()
        {
            MelonLogger.Msg("Saving...");
            MelonLogger.Msg($"Current map no: {sceneID}");
            MelonLogger.Msg($"Current map value: {RumbleTrees.Settings[sceneID].SavedValue}");
            MelonLogger.Msg($"wasSceneChanged: {wasSceneChanged}");
            if ((bool)RumbleTrees.Settings[sceneID].SavedValue != wasSceneChanged)
            {
                UpdateColours(cherryColour, wasSceneChanged);
                wasSceneChanged = !wasSceneChanged;
            }
        }

        public void OnUIInit()
        {
            UI.instance.AddMod(RumbleTrees);
        }

        private void UpdateColours(Color colour, bool reset = false)
        {
            if (leafObjects.Count != 0)
            {
                foreach (GameObject leafObject in leafObjects)
                {
                    renderer = leafObject.GetComponent<MeshRenderer>();
                    leafMaterial = renderer.material;

                    if (reset)
                    {
                        leafMaterial.SetColor(LCT1, originalShades[2]);
                        leafMaterial.SetColor(LCB1, originalShades[0]);
                        leafMaterial.SetColor(LCT2, originalShades[2]);
                        leafMaterial.SetColor(LCB2, originalShades[0]);
                    }

                    Color.RGBToHSV(colour, out float hue, out float sat, out float val);

                    if (sat > 0.9f) sat = 0.9f;
                    if (sat < 0.1f) sat = 0.1f;
                    if (val > 0.9f) val = 0.9f;
                    if (val < 0.1f) val = 0.1f;

                    shades[0] = Color.HSVToRGB(hue, sat - 0.1f, val - 0.1f);
                    shades[1] = Color.HSVToRGB(hue, sat, val);
                    shades[2] = Color.HSVToRGB(hue, sat + 0.1f, val + 0.1f);
                    if (leafMaterial != null && !reset)
                    {
                        if (!originalSaved[0])
                        {
                            originalShades[2] = leafMaterial.GetColor(LCT1);
                            originalShades[0] = leafMaterial.GetColor(LCB1);
                            originalSaved[0] = true;
                        }
                        leafMaterial.SetColor(LCT1, shades[2]);
                        leafMaterial.SetColor(LCB1, shades[0]);
                        if (sceneID == 3) leafMaterial.SetColor(LCT2, shades[2]);
                        if (sceneID != 2) leafMaterial.SetColor(LCB2, shades[0]);
                        // MelonLogger.Msg($"Resulting colour: {material.GetColor(LCT2)}");
                    }
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
            else if (sceneID != 0 && sceneID != 3)
            {
                MelonLogger.Warning("Leaf object not found!");
            }
        }
    }
}