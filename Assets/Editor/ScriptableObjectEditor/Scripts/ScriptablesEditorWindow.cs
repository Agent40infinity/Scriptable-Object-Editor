using System.Linq;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Agent.Assembly;

namespace Agent.SOE
{
    public class ScriptablesEditorWindow : EditorWindow
    {
        public static ScriptablesEditorWindow window;

        protected GUISkin skin;

        protected SerializedObject serializedObject;
        protected SerializedProperty serializedProperty;

        protected Object selectedObject;
        protected string selectedName;
        protected ScriptableObject[] activeObjects;
        protected string selectedPropertyPach;
        protected string selectedProperty;

        Vector2 scrollPosition = Vector2.zero;
        Vector2 itemScrollPosition = Vector2.zero;
        readonly float sidebarWidth = 250f;

        protected string activePath = "Assets";
        protected System.Type activeType = typeof(ScriptableObject);

        protected string typeName = "Scriptable Types";

        protected string sortSearch = "";
        protected int stringMax = 27;

        protected Rect typeButton;

        [MenuItem("Tools/Scriptable Object Editor")]
        protected static void ShowWindow()
        {
            window = GetWindow<ScriptablesEditorWindow>("Scriptables Editor");
            window.UpdateObjets();
        }

        private void OnEnable()
        {
            skin = (GUISkin)Resources.Load("ScriptableEditorGUI");
            UpdateObjets();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical("box");

            HeaderTitle();
            HeaderNavigation();

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginHorizontal();

            SelectionNavigation();
            SelectableContents();

            EditorGUILayout.EndHorizontal();

            if (activeObjects.Length > 0 && serializedObject != null)
                Apply();
        }

        private void HeaderTitle()
        {
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Scriptables Editor", skin.customStyles.ToList().Find(x => x.name == "Header"));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        #region Navigation
        #region Header
        private void HeaderNavigation()
        {
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

            if (GUILayout.Button(new GUIContent("Folder", "Allows you to mask the search directory."), GUILayout.MaxWidth(150)))
            {
                string basePath = EditorUtility.OpenFolderPanel("Select folder to mask path.", activePath, "");

                if (basePath.Contains(Application.dataPath))
                {
                    basePath = basePath.Substring(basePath.LastIndexOf("Assets"));
                }

                if (!AssetDatabase.IsValidFolder(basePath))
                {
                    EditorUtility.DisplayDialog("Error: File Path Invalid", "Please make sure the path is contained with the project's assets folder", "Ok");
                }
                else
                {
                    activePath = basePath;
                    UpdateObjets();
                }

            }

            EditorGUILayout.LabelField(activePath);
            EditorGUILayout.EndHorizontal();
        }
        #endregion

        #region Sidebar
        private void SelectionNavigation()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.MaxWidth(sidebarWidth), GUILayout.ExpandHeight(true));

            if (EditorGUILayout.DropdownButton(new GUIContent(typeName, "Used to mask the type of ScriptableObject being searched for."), FocusType.Keyboard))
            {
                GenericMenu menu = new GenericMenu();

                var function = new GenericMenu.MenuFunction2((type) => { activeType = (System.Type)type; typeName = type.ToString(); if (activeType == typeof(ScriptableObject)) typeName = "All"; UpdateObjets(); });

                menu.AddItem(new GUIContent("All", "Display every type of ScriptableObject within the project."), AssemblyTypes.OfType(typeof(ScriptableObject), activeType), function, typeof(ScriptableObject));
                menu.AddSeparator("");

                foreach (var item in AssemblyTypes.GetAllTypes())
                {
                    menu.AddItem(new GUIContent(item.ToString()), AssemblyTypes.OfType(item, activeType), function, item);
                }
                menu.DropDown(typeButton);
            }
            if (Event.current.type == EventType.Repaint) typeButton = GUILayoutUtility.GetLastRect();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            sortSearch = EditorGUILayout.TextField(sortSearch, GUILayout.MaxWidth(240));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));

            if (activeObjects.Length > 0)
                DrawScriptables(activeObjects);

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("+", "Open the ScriptableObject creation menu."), GUILayout.Width(25)))
            {
                CreationEditorWindow window = GetWindow<CreationEditorWindow>();
                window.position = AssemblyTypes.CenterOnOriginWindow(window.position, position);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
        #endregion
        #endregion

        #region Display Selected Contents
        private void SelectableContents()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.ExpandHeight(true));
            itemScrollPosition = EditorGUILayout.BeginScrollView(itemScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
            switch (true)
            {
                case bool _ when serializedObject != null && selectedObject != null:
                    GUI.enabled = false;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Name"));
                    GUI.enabled = true;

                    serializedProperty = serializedObject.GetIterator();
                    serializedProperty.NextVisible(true);
                    DrawProperties(serializedProperty);

                    GUILayout.Space(15);

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(new GUIContent("Rename", "Use to rename the selected ScriptableObject.")))
                    {
                        RenamePopup(selectedObject);
                    }

                    var style = new GUIStyle(GUI.skin.button);
                    style.normal.textColor = Color.red;

                    if (GUILayout.Button(new GUIContent("Delete", "Use to delete the selected ScriptableObject."), style))
                    {
                        switch (EditorUtility.DisplayDialog("Delete " + selectedObject.name + "?", "Are you sure you want to delete '" + selectedObject.name + "'?", "Yes", "No"))
                        {
                            case true:
                                string path = AssetDatabase.GetAssetPath(selectedObject);
                                AssetDatabase.DeleteAsset(path);
                                serializedObject = null;
                                selectedObject = null;
                                UpdateObjets();
                                break;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    break;
                default:
                    EditorGUILayout.LabelField("No item selected, make sure you've selected an item.");
                    break;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        protected void RenamePopup(Object selected)
        {
            PopupWindow.Show(new Rect(), new NamingEditorWindow(selected, position));
        }
        #endregion

        protected void UpdateObjets()
        {
            activeObjects = AssemblyTypes.GetAllInstancesOfType(activePath, activeType);
        }

        protected void DrawProperties(SerializedProperty property)
        {
            if (property.displayName == "Script") { GUI.enabled = false; }
            EditorGUILayout.PropertyField(property, true);
            GUI.enabled = true;

            EditorGUILayout.Space(40);

            while (property.NextVisible(false))
            {
                EditorGUILayout.PropertyField(property, true);
            }
        }

        protected void DrawScriptables(ScriptableObject[] objects)
        {
            foreach (ScriptableObject item in objects)
            {
                if (item != null && item.name.IndexOf(sortSearch, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (GUILayout.Button(ShortenString(item.name), skin.button, GUILayout.ExpandWidth(true)))
                    {
                        if (Event.current.button == 1)
                        {
                            GenericMenu menu = new GenericMenu();

                            var function = new GenericMenu.MenuFunction2((name) => RenamePopup(AssemblyTypes.FindObject(activeObjects, item.name)));

                            menu.AddItem(new GUIContent("Rename"), false, function, item.name);
                            menu.ShowAsContext();
                        }
                        else
                        {
                            selectedPropertyPach = item.name;

                            if (!string.IsNullOrEmpty(selectedPropertyPach))
                            {
                                selectedProperty = selectedPropertyPach;
                                selectedObject = AssemblyTypes.FindObject(activeObjects, selectedProperty);
                            }

                            UpdateObjets();
                        }
                    }
                }
            }

            switch (true)
            {
                case bool _ when selectedObject != null:
                    serializedObject = new SerializedObject(selectedObject);
                    break;
            }
        }

        protected string ShortenString(string item)
        {
            switch (true)
            {
                case bool _ when item.Length >= stringMax:
                    return item.Substring(0, stringMax) + "...";
                default:
                    return item;
            }
        }

        protected void Apply()
        {
            serializedObject.ApplyModifiedProperties();
        }
    }
}