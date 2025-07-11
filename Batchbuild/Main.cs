/*
 * x = EST/OUEST
 * y = altitude
 * z = NORD/SUD
 * 
 * id pos_x pos_y pos_z angle_x angle_y angle_z point_x point_y point_z axis_x axis_y axis_z angle
 * coords build
 * build helper
 * batch build
 * https://docs.unity3d.com/2019.4/Documentation/ScriptReference/Undo.html
 * Player.RemovePiece()
 * 
 * À FAIRE :
 * 
 * 
 */

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Diagnostics;
using xFunc.Maths.Results;
using static UnityEngine.GraphicsBuffer;

namespace Batchbuild
{
    [BepInPlugin("orax.batchbuild", ModName, Version)]
    [BepInProcess("valheim.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public static Harmony harmony = new Harmony("mod.batchbuild");
        private static readonly GUIStyle styleTooltip = new GUIStyle();
        private static Texture2D texture = new Texture2D(1, 1);

        // config
        public static ConfigEntry<KeyboardShortcut> configShowGUI;
        public static ConfigEntry<string> configCommand;

        public const string Version = "0.2.0.2";
        public const string ModName = "Batch build";
        public static ManualLogSource Log;

        public static bool show = false;
        public static string cameraTargetPath;
        public static string numberFormat = "0.######";
        public static Rect windowRect = new Rect(20, 120, 250, 10);
        public static GameObjectData targetGameObjectData;
        public static GameObjectData targetGameObjectOldData;

        public struct GameObjectData
        {
            public GameObject gameObject;
            public string x;
            public string y;
            public string z;
            public string angle_x;
            public string angle_y;
            public string angle_z;
            public string prefabName;
            public Vector3 position;
            public Quaternion rotation;
        }

        public void Awake()
        {
            string defaultConfigSection = "General";
            Log = Logger;

            harmony.PatchAll();
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            texture = new Texture2D(1, 1);
            texture.SetPixel(1, 1, new Color(0f, 0f, 0f, 0.5f));
            texture.Apply();
            styleTooltip.padding = new RectOffset(4, 4, 1, 1);
            styleTooltip.normal.textColor = Color.white;
            styleTooltip.normal.background = texture;

            // config
            configShowGUI = Config.Bind<KeyboardShortcut>(defaultConfigSection, "Show GUI", new KeyboardShortcut(KeyCode.F3));
            configCommand = Config.Bind<string>(defaultConfigSection, "Command", "build");
        }

        public void Update()
        {
            if (configShowGUI.Value.IsDown() && Player.m_localPlayer != null)
            {
                UpdateData(ref targetGameObjectData, GetTargetGameObject());
                UpdateData(ref targetGameObjectOldData, GetTargetGameObject());
                show = true;
            }
        }

        public void OnGUI()
        {
            if (!show || Player.m_localPlayer == null)
            {
                return;
            }

            Event e = Event.current;
            if (e.isKey && GUIUtility.keyboardControl > 0)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    UpdateGameObjectPosition(ref targetGameObjectData.gameObject, ref targetGameObjectData);
                }
            }

            GameObject target = GetTargetGameObject();
            if (target)
            {
                string prefabName = "";
                ZNetView view = target.GetComponent<ZNetView>();
                if (view)
                {
                    //prefabName = view.GetPrefabName();
                    prefabName=Utils.GetPrefabName(target);
                }

                string infoPosition =
                    "x=" + target.transform.position.x.ToString(numberFormat) + " " +
                    "y=" + target.transform.position.y.ToString(numberFormat) + " " +
                    "z=" + target.transform.position.z.ToString(numberFormat) + " " +
                    target.transform.rotation.eulerAngles.ToString(numberFormat);
                _ = GUILayout.TextField(prefabName + Environment.NewLine + infoPosition);
            }

            windowRect = GUILayout.Window(0, windowRect, CreateWindow, ModName);
        }

        public void OnDestroy()
        {
            harmony.UnpatchSelf();
        }

        public static string InterpolateString(string text)
        {

            text = text.Replace("{x}", Player.m_localPlayer.transform.position.x.ToString());
            text = text.Replace("{y}", Player.m_localPlayer.transform.position.y.ToString());
            text = text.Replace("{z}", Player.m_localPlayer.transform.position.z.ToString());

            return text;
        }

        public static void UpdateData(ref GameObjectData gameObjectData, GameObject newGameObject = null)
        {
            if (gameObjectData.gameObject == null && newGameObject == null)
            {
                gameObjectData.x = "";
                gameObjectData.y = "";
                gameObjectData.z = "";
                gameObjectData.angle_x = "";
                gameObjectData.angle_y = "";
                gameObjectData.angle_z = "";

                return;
            }

            if (newGameObject || gameObjectData.gameObject == null)
            {
                gameObjectData.gameObject = newGameObject;
            }
            gameObjectData.prefabName = "";
            ZNetView view = gameObjectData.gameObject.GetComponent<ZNetView>();
            if (view)
            {
                //gameObjectData.prefabName = view.GetPrefabName();
                gameObjectData.prefabName = Utils.GetPrefabName(gameObjectData.gameObject);

            }

            // position
            gameObjectData.x = gameObjectData.gameObject.transform.position.x.ToString(numberFormat);
            gameObjectData.y = gameObjectData.gameObject.transform.position.y.ToString(numberFormat);
            gameObjectData.z = gameObjectData.gameObject.transform.position.z.ToString(numberFormat);

            // angle rotation
            Vector3 angles = gameObjectData.gameObject.transform.localRotation.eulerAngles;
            gameObjectData.angle_x = angles.x.ToString(numberFormat);
            gameObjectData.angle_y = angles.y.ToString(numberFormat);
            gameObjectData.angle_z = angles.z.ToString(numberFormat);
        }

        public static void CreateWindow(int windowID)
        {
            int maxLength = 15;
            float width = 90f;
            bool applyModifications = false;

            GUI.DragWindow(new Rect(0, 0, windowRect.width, 20));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Player position");
            if (GUILayout.Button(new GUIContent("X", "Close this window."), GUILayout.ExpandWidth(false)))
            {
                show = false;

                return;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("x", GUILayout.ExpandWidth(false));
            GUILayout.TextField(Player.m_localPlayer.transform.position.x.ToString(numberFormat), maxLength, GUILayout.Width(70));
            GUILayout.Label("y", GUILayout.ExpandWidth(false));
            GUILayout.TextField(Player.m_localPlayer.transform.position.y.ToString(numberFormat), maxLength, GUILayout.Width(70));
            GUILayout.Label("z", GUILayout.ExpandWidth(false));
            GUILayout.TextField(Player.m_localPlayer.transform.position.z.ToString(numberFormat), maxLength, GUILayout.Width(70));
            GUILayout.EndHorizontal();

            GUILayout.Label("Target");

            GUILayout.BeginHorizontal();
            GUILayout.Label("id", GUILayout.ExpandWidth(false));
            GUILayout.TextField(targetGameObjectData.prefabName);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(); // BeginHorizontal
            GUILayout.BeginVertical(); // BeginVertical

            // gauche

            // position

            GUILayout.BeginHorizontal();
            GUILayout.Label("Position", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("x", GUILayout.ExpandWidth(false));
            targetGameObjectData.x = GUILayout.TextField(targetGameObjectData.x, maxLength, GUILayout.Width(width));
            if (GUILayout.Button(new GUIContent("R", "Rounds the value."), GUILayout.ExpandWidth(false)))
            {
                targetGameObjectData.x = Math.Round(float.Parse(targetGameObjectData.x)).ToString(numberFormat);
                applyModifications = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("y", GUILayout.ExpandWidth(false));
            targetGameObjectData.y = GUILayout.TextField(targetGameObjectData.y, maxLength, GUILayout.Width(width));
            if (GUILayout.Button(new GUIContent("R", "Rounds the value."), GUILayout.ExpandWidth(false)))
            {
                targetGameObjectData.y = Math.Round(float.Parse(targetGameObjectData.y)).ToString(numberFormat);
                applyModifications = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("z", GUILayout.ExpandWidth(false));
            targetGameObjectData.z = GUILayout.TextField(targetGameObjectData.z, maxLength, GUILayout.Width(width));
            if (GUILayout.Button(new GUIContent("R", "Rounds the value."), GUILayout.ExpandWidth(false)))
            {
                targetGameObjectData.z = Math.Round(float.Parse(targetGameObjectData.z)).ToString(numberFormat);
                applyModifications = true;
            }
            GUILayout.EndHorizontal();

            // rotation

            GUILayout.Label("Rotation", GUILayout.ExpandWidth(false));

            GUILayout.BeginHorizontal();
            GUILayout.Label("x", GUILayout.ExpandWidth(false));
            targetGameObjectData.angle_x = GUILayout.TextField(targetGameObjectData.angle_x, maxLength, GUILayout.Width(width));
            if (GUILayout.Button(new GUIContent("R", "Rounds the value."), GUILayout.ExpandWidth(false)))
            {
                targetGameObjectData.angle_x = Math.Round(float.Parse(targetGameObjectData.angle_x)).ToString(numberFormat);
                applyModifications = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("y", GUILayout.ExpandWidth(false));
            targetGameObjectData.angle_y = GUILayout.TextField(targetGameObjectData.angle_y, maxLength, GUILayout.Width(width));
            if (GUILayout.Button(new GUIContent("R", "Rounds the value."), GUILayout.ExpandWidth(false)))
            {
                targetGameObjectData.angle_y = Math.Round(float.Parse(targetGameObjectData.angle_y)).ToString(numberFormat);
                applyModifications = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("z", GUILayout.ExpandWidth(false));
            targetGameObjectData.angle_z = GUILayout.TextField(targetGameObjectData.angle_z, maxLength, GUILayout.Width(width));
            if (GUILayout.Button(new GUIContent("R", "Rounds the value."), GUILayout.ExpandWidth(false)))
            {
                targetGameObjectData.angle_z = Math.Round(float.Parse(targetGameObjectData.angle_z)).ToString(numberFormat);
                applyModifications = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Apply", "Click or press Enter in a input box to apply.")))
            {
                applyModifications = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical(); // EndVertical

            GUILayout.BeginVertical(); // EndVertical

            // droite

            GUILayout.BeginHorizontal();
            GUILayout.Label("Position (old)", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("x", GUILayout.ExpandWidth(false));
            GUILayout.TextField(targetGameObjectOldData.x, GUILayout.Width(width));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("y", GUILayout.ExpandWidth(false));
            GUILayout.TextField(targetGameObjectOldData.y, GUILayout.Width(width));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("z", GUILayout.ExpandWidth(false));
            GUILayout.TextField(targetGameObjectOldData.z, GUILayout.Width(width));
            GUILayout.EndHorizontal();

            // rotation

            GUILayout.Label("Rotation (old)", GUILayout.ExpandWidth(false));

            GUILayout.BeginHorizontal();
            GUILayout.Label("x", GUILayout.ExpandWidth(false));
            GUILayout.TextField(targetGameObjectOldData.angle_x, GUILayout.Width(width));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("y", GUILayout.ExpandWidth(false));
            GUILayout.TextField(targetGameObjectOldData.angle_y, GUILayout.Width(width));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("z", GUILayout.ExpandWidth(false));
            GUILayout.TextField(targetGameObjectOldData.angle_z, GUILayout.Width(width));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Undo"))
            {
                UpdateGameObjectPosition(ref targetGameObjectData.gameObject, ref targetGameObjectOldData);
                targetGameObjectData = targetGameObjectOldData;
                applyModifications = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical(); // EndVertical
            GUILayout.EndHorizontal(); // EndHorizontal

            if (applyModifications)
            {
                UpdateGameObjectPosition(ref targetGameObjectData.gameObject, ref targetGameObjectData);
            }

            GUILayout.Label(GUI.tooltip, styleTooltip);
        }

        public static void CreateTextField(string label, ref string text)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.ExpandWidth(false));
            text = GUILayout.TextField(text, 20);
            GUILayout.EndHorizontal();
        }

        public static AssetBundle LoadBundle(string bundle)
        {
            var execAssembly = Assembly.GetExecutingAssembly();
            var stream = execAssembly.GetManifestResourceStream(bundle);
            AssetBundle.UnloadAllAssetBundles(true);

            return AssetBundle.LoadFromStream(stream);
        }

        public static string GetPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        public static bool UpdateGameObjectPosition(ref GameObject gameObject, ref GameObjectData gameObjectData)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(gameObjectData.prefabName);
            if (!prefab)
            {
                Log.LogWarning("Missing object \"" + gameObjectData.prefabName + "\"");

                return false;
            }

            SetGameObjectPosition(ref prefab, ref gameObjectData);
            GameObject gameObject_new = UnityEngine.Object.Instantiate(prefab);
            Piece component = gameObject_new.GetComponent<Piece>();
            if ((bool)component)
            {
                component.SetCreator(Player.m_localPlayer.GetPlayerID());
            }

            RemoveObject(ref gameObject);
            UpdateData(ref gameObjectData, gameObject_new);

            return true;
        }

        public static float Parse(string s)
        {
            if (float.TryParse(s, out float result))
            {
                return result;
            }
            else
            {
                return Convert.ToSingle(Lib.processor.Solve<NumberResult>(s).Result);
            }
        }

        public static void SetGameObjectPosition(ref GameObject gameObject, ref GameObjectData gameObjectData)
        {
            float x = float.Parse(gameObjectData.x);
            float y = float.Parse(gameObjectData.y);
            float z = float.Parse(gameObjectData.z);

            gameObject.transform.position = new Vector3(x, y, z);

            float angle_x = float.Parse(gameObjectData.angle_x);
            float angle_y = float.Parse(gameObjectData.angle_y);
            float angle_z = float.Parse(gameObjectData.angle_z);

            gameObject.transform.rotation = Quaternion.Euler(angle_x, angle_y, angle_z);
        }

        public static void InstantiatePrefab(string[] args, Vector3 offset)
        {
            GameObject prefab;
            string prefabID = args[0];
            float pos_x, pos_y, pos_z;
            Vector3 originalScale;
            bool scaleChanged = false;

            prefab = ZNetScene.instance.GetPrefab(prefabID);
            if (!prefab)
            {
                Log.LogWarning("Missing object \"" + prefabID + "\"");

                return;
            }

            if (Player.m_localPlayer == null)
            {
                Log.LogWarning("Local player is null.");

                return;
            }

            originalScale = prefab.transform.localScale;

            // position
            if (args.Length < 4)
            {
                pos_x = Player.m_localPlayer.transform.position.x;
                pos_y = Player.m_localPlayer.transform.position.y;
                pos_z = Player.m_localPlayer.transform.position.z;
            }
            else
            {

                pos_x = Parse(args[1]);
                pos_y = Parse(args[2]);
                pos_z = Parse(args[3]);
            }

            pos_x += offset.x;
            pos_y += offset.y;
            pos_z += offset.z;

            prefab.transform.position = new Vector3(pos_x, pos_y, pos_z);
            prefab.transform.rotation = Quaternion.identity; ;

            // rotation
            if (args.Length >= 7)
            {
                float angle_x = Parse(args[4]);
                float angle_y = Parse(args[5]);
                float angle_z = Parse(args[6]);

                prefab.transform.rotation = Quaternion.Euler(angle_x, angle_y, angle_z);
            }

            // rotate around
            if (args.Length >= 14)
            {
                float point_x = Parse(args[7]);
                float point_y = Parse(args[8]);
                float point_z = Parse(args[9]);

                float axis_x = Parse(args[10]);
                float axis_y = Parse(args[11]);
                float axis_z = Parse(args[12]);

                float angle = Parse(args[13]);

                point_x += offset.x;
                point_y += offset.y;
                point_z += offset.z;

                prefab.transform.RotateAround(
                    new Vector3(point_x, point_y, point_z),
                    new Vector3(axis_x, axis_y, axis_z),
                    angle);
            }

            // scale
            /*
             * https://forums.nexusmods.com/index.php?showtopic=10501073/#entry99434368
             */
            if (args.Length >= 17)
            {
                float scale_x = Parse(args[14]);
                float scale_y = Parse(args[15]);
                float scale_z = Parse(args[16]);

                prefab.transform.localScale = new Vector3(scale_x, scale_y, scale_z);
                scaleChanged = true;
            }

            Log.LogDebug("Instantiate \"" + prefab.name + "\" " +
                prefab.transform.position.ToString() + " " +
                prefab.transform.rotation.ToString());
            GameObject gameObject = UnityEngine.Object.Instantiate(prefab);

            // restaure échelle originale
            if (scaleChanged)
            {
                prefab.transform.localScale = originalScale;
            }

            Piece component = gameObject.GetComponent<Piece>();
            if ((bool)component)
            {
                component.SetCreator(Player.m_localPlayer.GetPlayerID());
            }
        }

        public static bool RemoveObject(ref GameObject gameObject)
        {
            ZNetView nview = gameObject.GetComponent<ZNetView>();
            if (nview == null)
            {
                return false;
            }

            nview.ClaimOwnership();
            ZNetScene.instance.Destroy(gameObject);

            return true;
        }

        public static GameObject GetTargetGameObject()
        {
            GameObject gameObject = null;

            //int layerMask = Player.m_localPlayer.m_placeRayMask;
            int layerMask = LayerMask.GetMask("item", "piece", "piece_nonsolid", "Default", "static_solid", "Default_small", "vehicle");

            Ray ray = new Ray(GameCamera.instance.transform.position, GameCamera.instance.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit raycastHit, 500f, layerMask))
            {
                Transform transform = raycastHit.collider.transform.root;

                gameObject = transform.gameObject;
            }

            return gameObject;
        }
    }
}
