using Harmony;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppRUMBLE.Combat.ShiftStones;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Pools;
using MelonLoader;
using RumbleModdingAPI;
using RumbleModdingAPI.RMAPI;
using RumbleModUI;
using System.Collections;
using System.Text.RegularExpressions;
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
        public const string Version = "2.4.0";
        public const string Name = "RumbleTrees";
        public const string Author = "Orangenal";
        public const string DownloadLink = "https://thunderstore.io/c/rumble/p/Orangenal/RumbleTrees/";
    }

    public class Validation : ValidationParameters
    {
        // TODO: make root leaves not error on pit
        // TODO: make random colours work for roots as well
        private string[] themes = ["cherry", "orange", "yellow", "red"];
        public static string[] stones = ["flow", "vigor", "volatile", "adamant", "charge", "guard", "stubborn", "surge"];
        public static string[] mats = ["flow", "vigor", "volatile", "adamant", "charge", "guard", "stubborn", "surge", "vanilla"];
        //public static string[] fruitMats = ["flow", "vigor", "volatile", "adamant", "charge", "guard", "stubborn", "surge", "vanilla"];
        public Validation(string type)
        {
            this.type = type;
        }
        private string type;
        public override bool DoValidation(string Input)
        {
            string rgbPattern = @"^(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\s(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\s(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)$";
            string hexPattern = @"^#?[0-9A-Fa-f]{6}$";

            if (Input.ToLower() == "random") return true;
            if (Input.ToLower() == "vanilla") return true;

            if (type.EndsWith("Mat"))
            {
                return mats.Contains(Input.ToLower());
            }
            //else if (type == "leafMat")
            //{
            //    return leafMats.Contains(Input.ToLower());
            //}

            if (Regex.IsMatch(Input, rgbPattern) || Regex.IsMatch(Input, hexPattern))
            {
                return true;
            }
            else
            {
                Input = Input.ToLower();
                if (Input == "rainbow") return true;
                if (themes.Contains(Input) && type == "leaf") return true;
                return false;
            }
        }
    }

    public class Core : MelonMod
    {
        private string currentScene = "Loader";
        private int sceneID = -1;
        private bool enabled = true;
        private Mod RumbleTrees = new Mod();

        Random rand = new();

        private List<GameObject> leafObjects = new List<GameObject>();
        private List<GameObject> fruitObjects = new List<GameObject>();
        private GameObject VFXsObject = null;
        private Color originalLeafColour = new Color();
        private Texture originalFruitTexture = null;
        private Material originalLeafMaterial = null;
        private Material originalFruitMaterial = null;
        private Il2CppStructArray<GradientColorKey> originalVFXColours = null;

        private string selectedLeafMaterial = "vanilla";
        private string selectedFruitMaterial = "vanilla";
        private Color selectedLeafColour = default;
        private string strSelectedLeafColour = "Cherry";
        private Color selectedFruitColour = Color.white;
        private string strSelectedFruitColour = "FFFFFF";
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
        ];

        // Presets
        private Color cherryColour = new Color(0.86f, 0.54f, 0.9f, 1f);
        private Color orangeColour = new Color(1.0f, 0.44f, 0.0f, 1f);
        private Color yellowColour = new Color(1.0f, 0.78f, 0.0f, 1f);
        private Color redColour = new Color(0.66f, 0.0f, 0.0f, 1f);

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
            RumbleTrees.AddDescription("Description", "", "Make them pretty!\n\nCurrent presets:\nCherry\nOrange\nYellow\nRed\nRainbow\nVanilla (literally does nothing)\nRandom", new Tags { IsSummary = true });

            RumbleTrees.AddToList("Enabled in Gym", true, 0, "Enables the mod in the gym", new Tags());
            RumbleTrees.AddToList("Enabled on Ring", true, 0, "Enables the mod on the ring map", new Tags());
            RumbleTrees.AddToList("Enabled in Parks", true, 0, "Enables the mod in parks", new Tags());

            RumbleTrees.AddToList("Enable Falling leaf VFXs", false, 0, "Re-enables the old falling leaf VFXs (May slightly impact performance)", new Tags());

            RumbleTrees.AddToList("Leaf colour", "Cherry", "Type in either a preset name or a custom colour in one of the supported formats: \n255 255 255\nFFFFFF", new Tags());
            RumbleTrees.AddToList("Fruit colour", "FFFFFF", "Type in either \"Rainbow,\" \"Vanilla,\" \"Random,\" or a custom colour in one of the supported formats: \n255 255 255\nFFFFFF", new Tags());
            RumbleTrees.AddToList("Leaf material", "vanilla", "Type in either \"vanilla,\" \"Random,\" a shiftstone, or \"roots\" to set the material of the leaves", new Tags());
            RumbleTrees.AddToList("Fruit material", "vanilla", "Type in either \"vanilla,\" \"Random,\" a shiftstone, or \"leaves\" to set the material of the fruits", new Tags());

            RumbleTrees.AddToList("Rainbow speed", 1, "The speed of rainbow leaves (if selected)", new Tags());

            RumbleTrees.AddValidation("Leaf colour", new Validation("leaf"));
            RumbleTrees.AddValidation("Fruit colour", new Validation("fruit"));
            RumbleTrees.AddValidation("Leaf material", new Validation("leafMat"));
            RumbleTrees.AddValidation("Fruit material", new Validation("fruitMat"));

            RumbleTrees.GetFromFile();

            // Assign settings to their respective variables
            strSelectedLeafColour = ((string)RumbleTrees.Settings[5].Value).ToLower();
            strSelectedFruitColour = ((string)RumbleTrees.Settings[6].Value).ToLower();
            selectedLeafMaterial = ((string)RumbleTrees.Settings[7].Value).ToLower();
            selectedFruitMaterial = ((string)RumbleTrees.Settings[8].Value).ToLower();

            if (strSelectedLeafColour != "vanilla" && strSelectedLeafColour != "rainbow") setSelectedLeafColour(strSelectedLeafColour);
            if (strSelectedFruitColour != "vanilla" && strSelectedFruitColour != "rainbow") setSelectedFruitColour(strSelectedFruitColour);

            RumbleTrees.ModSaved += OnSave;

            RumbleTrees.Settings[4].SavedValueChanged += OnToggleVFXs;

            RumbleTrees.Settings[5].SavedValueChanged += OnLeafColourChange;
            RumbleTrees.Settings[6].SavedValueChanged += OnFruitColourChange;

            RumbleTrees.Settings[7].SavedValueChanged += OnLeafMaterialChange;
            RumbleTrees.Settings[8].SavedValueChanged += OnFruitMaterialChange;

            UI.instance.UI_Initialized += OnUIInit;

            LoggerInstance.Msg("Initialised.");
        }

        public override void OnLateInitializeMelon()
        {
            Il2CppSystem.Collections.Generic.List<PooledMonoBehaviour> fruitPool = PoolManager.Instance.GetPool("Fruit").PooledObjects;

            foreach (PooledMonoBehaviour fruit in fruitPool)
            {
                fruitObjects.Add(fruit.gameObject);
            }

            if (selectedFruitMaterial.ToLower() != "vanilla")
                MelonCoroutines.Start(UpdateFruitMaterial(selectedFruitMaterial));
            else if (strSelectedFruitColour == "rainbow")
                rainbowFruitCoroutine = MelonCoroutines.Start(RAINBOWFRUIT());
            else if (strSelectedFruitColour != "vanilla")
                UpdateFruitColour(selectedFruitColour);
        }

        private void OnSave()
        {
            // If the trees in the current scene should be enabled / disabled, do that
            if ((bool)RumbleTrees.Settings[sceneID].SavedValue != enabled) {
                enabled = !enabled;
                if (enabled)
                {
                    if (strSelectedLeafColour != "vanilla") UpdateLeafColour(selectedLeafColour);
                    if (strSelectedFruitColour != "vanilla") UpdateFruitColour(selectedFruitColour);
                    if (selectedLeafMaterial != "vanilla") MelonCoroutines.Start(UpdateLeafMaterial(selectedLeafMaterial));
                    if (selectedFruitMaterial != "vanilla") MelonCoroutines.Start(UpdateFruitMaterial(selectedFruitMaterial));

                    if (strSelectedLeafColour == "rainbow")
                    {
                        rainbowLeafCoroutine = MelonCoroutines.Start(RAINBOWLEAVES());
                    }

                    if (strSelectedFruitColour == "rainbow")
                    {
                        rainbowFruitCoroutine = MelonCoroutines.Start(RAINBOWFRUIT());
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
        private void OnLeafColourChange(object sender = null, EventArgs e = null)
        {
            if (e != null)
            {
                if (strSelectedLeafColour == "vanilla")
                {
                    //InitLightmaps();
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

        private void OnToggleVFXs(object sender = null, EventArgs e = null)
        {
            ToggleVFXs(((ValueChange<bool>)e).Value);
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
        private void OnFruitColourChange(object sender = null, EventArgs e = null)
        {
            if (e != null)
            {
                ValueChange<string> valueChange = (ValueChange<string>)e;
                strSelectedFruitColour = valueChange.Value.ToLower();

                if (rainbowFruitCoroutine != null)
                {
                    MelonCoroutines.Stop(rainbowFruitCoroutine);
                    rainbowFruitCoroutine = null;
                }

                if (strSelectedFruitColour == "vanilla")
                {
                    ResetFruitColour();
                    return;
                }
                else if (strSelectedFruitColour == "rainbow")
                {
                    rainbowFruitCoroutine = MelonCoroutines.Start(RAINBOWFRUIT());
                    return;
                }

                setSelectedFruitColour(strSelectedFruitColour);
                UpdateFruitColour(selectedFruitColour);
            }
        }

        // Update roots on material change
        private void OnFruitMaterialChange(object sender = null, EventArgs e = null)
        {
            if (e != null)
            {
                ValueChange<string> valueChange = (ValueChange<string>)e;
                selectedFruitMaterial = valueChange.Value.ToLower();

                if (selectedFruitMaterial == "vanilla")
                {
                    ResetFruitMaterial();
                    return;
                }

                MelonCoroutines.Start(UpdateFruitMaterial(selectedFruitMaterial));
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

        public void OnUIInit()
        {
            UI.instance.AddMod(RumbleTrees);
        }

        //public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        //{
        //    ResetFruitMaterial();
        //    originalFruitMaterial = null;
        //}

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

            // Add the leaf and root objects
            if (sceneName == "Gym")
            {
                sceneID = 1;
                currentScene = sceneName;
                leafObjects.Add(GameObject.Find(GymTrees[0]));

                VFXsObject = GameObjects.Gym.SCENEVFXSFX.VisualEffects.FallingLeafVFXs.GetGameObject();
            }

            if (sceneName == "Map0")
            {
                sceneID = 2;
                currentScene = "Ring";
                GameObject leaves = GameObject.Find(RingTrees[0]);
                leafObjects.Add(leaves);
            }

            if (sceneName == "Park")
            {
                sceneID = 3;
                currentScene = "Park";
                GameObject leaves = GameObject.Find(ParkTrees[0]);
                leafObjects.Add(leaves);

                VFXsObject = GameObjects.Park.SCENEVFXSFX.VisualEffects.FallingLeafVFXs.GetGameObject();
            }

            if (sceneName == "Loader")
            {
                return;
            }

            // UPDATE EVERYTHING!!!
            enabled = (bool)RumbleTrees.Settings[sceneID].Value;
            selectedLeafMaterial = ((string) RumbleTrees.Settings[7].SavedValue).ToLower();
            if (enabled)
            {
                if (strSelectedLeafColour != "vanilla") UpdateLeafColour(selectedLeafColour);
                ToggleVFXs((bool)RumbleTrees.Settings[4].Value);
                if (selectedLeafMaterial != "vanilla") MelonCoroutines.Start(UpdateLeafMaterial(selectedLeafMaterial));
            }
            if (strSelectedLeafColour == "rainbow" && rainbowLeafCoroutine == null)
            {
                rainbowLeafCoroutine = MelonCoroutines.Start(RAINBOWLEAVES());
            }
        }

        Texture2D ConvertToTexture2D(Texture texture)
        {
            // Create a temporary RenderTexture
            RenderTexture rt = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB
            );

            // Copy texture to RenderTexture
            Graphics.Blit(texture, rt);

            // Backup the active RenderTexture
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            // Create a new Texture2D and read the RenderTexture into it
            Texture2D tex2D = new Texture2D(texture.width, texture.height, TextureFormat.ARGB32, false);
            tex2D.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex2D.Apply();

            // Restore the active RenderTexture
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            tex2D.filterMode = texture.filterMode;
            tex2D.wrapMode = texture.wrapMode;

            return tex2D;
        }

        // I'm not gonna comment on these functions cause it should be obvious from the name
        private void UpdateLeafColour(Color colour)
        {
            if (!enabled) return;

            if (strSelectedLeafColour == "random") colour = new Color((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
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

                    // If the material is different we need to use the correct property (but there's no roots on pit)
                    if (selectedLeafMaterial == "roots" && currentScene != "Pit")
                    {
                        //material.SetColor(RC1, shades[1]);
                        //material.SetColor(RC2, shades[0]);
                    }
                    else
                    {
                        material.SetColor("_Main_color", colour);
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
            if (selectedFruitMaterial.ToLower() != "vanilla") return;

            GameObject fruit = fruitObjects.First();
            MeshRenderer renderer = fruit.GetComponent<MeshRenderer>();

            if (originalFruitTexture == null)
                originalFruitTexture = renderer.material.GetTexture("_Albedo");

            Texture2D texture = ConvertToTexture2D(originalFruitTexture);
            Color32[] pixels = texture.GetPixels32();

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];

                if (p.b > 0x80)
                {
                    pixels[i] = colour;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            foreach (GameObject fruitObject in fruitObjects)
            {
                renderer = fruitObject.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material.SetTexture("_Albedo", texture);
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

                        if (selectedLeafMaterial == "roots" && (bool)RumbleTrees.Settings[sceneID].Value)
                        {
                            //material.SetColor(RC1, originalLeafColours[0]);
                            //material.SetColor(RC2, originalLeafColours[1]);
                        }
                        else
                        {
                            material.SetColor("_Main_color", originalLeafColour);
                        }

                        //MelonCoroutines.Start(SwapLightmap(renderer, currentScene, true));
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
            if (originalFruitTexture == null) return;
            foreach (GameObject fruit in fruitObjects)
            {
                MeshRenderer renderer = fruit.GetComponent<MeshRenderer>();

                if (renderer != null)
                {
                    renderer.material.SetTexture("_Albedo", originalFruitTexture);
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
                if (materialName == "random" && enabled) materialName = Validation.mats[rand.Next(Validation.mats.Length)];
                foreach (GameObject leafObject in leafObjects)
                {
                    MeshRenderer renderer = leafObject.GetComponent<MeshRenderer>();
                    
                    if (materialName == "vanilla")
                    {
                        ResetLeafMaterial();
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
                if (materialName == "random") materialName = Validation.mats[rand.Next(Validation.mats.Length)];

                foreach (GameObject rootObject in fruitObjects)
                {
                    MeshRenderer renderer = rootObject.GetComponent<MeshRenderer>();

                    if (materialName == "vanilla")
                    {
                        ResetFruitMaterial();
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
                if (enabled && strSelectedFruitColour != "vanilla")
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
                    rainbowHue += (int)RumbleTrees.Settings[9].SavedValue;
                    FrameCounter = 0;
                }
                FrameCounter++;
                yield return waitForFixedUpdate;
            }
        }

        IEnumerator RAINBOWFRUIT()
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
                    rainbowHue += (int)RumbleTrees.Settings[9].SavedValue;
                    FrameCounter = 0;
                }
                FrameCounter++;
                yield return waitForFixedUpdate;
            }
        }
    }
}