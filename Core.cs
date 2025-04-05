using MelonLoader;
using UnityEngine;
using UnityEngine.VFX;

[assembly: MelonInfo(typeof(RUMBLECherryBlossoms.Core), "CherryBlossoms", "1.0.0", "Orangenal", null)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]
[assembly: MelonColor(255, 219, 138, 230)]

namespace RUMBLECherryBlossoms
{
    public class Core : MelonMod
    {
        internal static Color[] shades = new Color[3];
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

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            LCT1 = Shader.PropertyToID("Color_133d236fee76457eb89bac53e692f8a3"); // Found in sharedassets3 path ID 2 (Material Root leave_Map0)
            LCB1 = Shader.PropertyToID("Color_6f942b46fda341409751e4e7f292de58"); // Ditto
            LCT2 = Shader.PropertyToID("Color_703ec75398a14bf19c79ac19fa909a6f"); // Ditto
            LCB2 = Shader.PropertyToID("Color_d75e4f72dbfb4e7187f1c2d80558c414"); // Ditto
            FLG = Shader.PropertyToID("Leaf Color Gradient"); // Found in sharedassets0 path ID 371 (VisualEffectAsset Falling leaves)

            leafObjects = new List<GameObject>();
            VFXsObject = null;

            if (sceneName == "Map0")
            {
                leafObjects.Add(GameObject.Find("Map0_production/Main static group/leave"));

                // MelonLogger.Msg($"Colour 3: {Shader.PropertyToID("Color_703ec75398a14bf19c79ac19fa909a6f")}"); // The alpha is 0??
                // MelonLogger.Msg($"Vector: {Shader.PropertyToID("Vector3_fa2c0c5c11884cdfbeb10e0460312f5c")}"); // idk what this is for but it's certainly there
            }
            else if (sceneName == "Map1")
            {
                leafObjects.Add(GameObject.Find("Map1_production/Main static group/Leaves_Map2"));
                VFXsObject = GameObject.Find("Lighting & Effects/Visual Effects/Falling Leaf VFXs");
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
            }
            else if (sceneName == "Park")
            {
                leafObjects.Add(GameObject.Find("________________SCENE_________________/Park/Main static group/Leaves/Leave_sphere_park"));
                leafObjects.Add(GameObject.Find("________________SCENE_________________/Park/Main static group/Leaves/Leave_sphere_park_001"));
                leafObjects.Add(GameObject.Find("________________SCENE_________________/Park/Main static group/Leaves/Leave_sphere_park_002"));
                leafObjects.Add(GameObject.Find("________________SCENE_________________/Park/Main static group/Leaves/Leave_sphere_park_003"));
                VFXsObject = GameObject.Find("Lighting and effects/Visual Effects/Falling Leaf VFXs");
            }
            else
            {
                // TODO: Implement Gym and Park leaves
                return;
            }

            if (leafObjects.Count != 0)
            {
                foreach (GameObject leafObject in leafObjects)
                {
                    renderer = leafObject.GetComponent<MeshRenderer>();
                    leafMaterial = renderer.material;
                    UpdateColours(sceneName, leafMaterial);
                }
            }
            else
            {
                MelonLogger.Warning("Leaf object not found!");
            }

            if (VFXsObject != null) UpdateColours(sceneName, VFXsObject: VFXsObject);

            else if (sceneName != "Map0")
            {
                MelonLogger.Warning("Leaf object not found!");
            }
        }

        private void UpdateColours(string sceneName, Material material = null, GameObject VFXsObject = null)
        {
            Color.RGBToHSV(cherryColour, out float hue, out float sat, out float val);

            if (sat > 0.9f) sat = 0.9f;
            if (sat < 0.1f) sat = 0.1f;
            if (val > 0.9f) val = 0.9f;
            if (val < 0.1f) val = 0.1f;

            shades[0] = Color.HSVToRGB(hue, sat - 0.1f, val - 0.1f);
            shades[1] = Color.HSVToRGB(hue, sat, val);
            shades[2] = Color.HSVToRGB(hue, sat + 0.1f, val + 0.1f);
            if (material != null)
            {
                material.SetColor(LCT1, shades[2]);
                material.SetColor(LCB1, shades[0]);
                if (sceneName == "Park") material.SetColor(LCT2, shades[2]);
                if (sceneName != "Gym") material.SetColor(LCB2, shades[0]);
                // MelonLogger.Msg($"Resulting colour: {material.GetColor(LCT2)}");
            }
            if (VFXsObject != null)
            {
                VisualEffect leafVFX;
                for (int i = 0; i < VFXsObject.transform.GetChildCount(); i++)
                {
                    leafVFX = VFXsObject.transform.GetChild(i).gameObject.GetComponent<VisualEffect>();
                    GradientColorKey[] keys = new GradientColorKey[2];
                    Gradient gradient = leafVFX.GetGradient(FLG);
                    keys[0].color = shades[1]; keys[1].color = shades[1];
                    gradient.colorKeys = keys;
                    leafVFX.SetGradient(FLG, gradient);
                }
            }
        }
    }
}