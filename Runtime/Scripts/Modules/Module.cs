using jKnepel.SimpleUnityNetworking.Managing;
using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace jKnepel.SimpleUnityNetworking.Modules
{
    public abstract class Module : IDisposable
    {
        public abstract string Name { get; }
        public ModuleConfiguration ModuleConfiguration { get; }

        protected INetworkManager NetworkManager { get; }

        protected Module(INetworkManager networkManager, ModuleConfiguration moduleConfig)
        {
            NetworkManager = networkManager;
            ModuleConfiguration = moduleConfig;
        }
        
        ~Module()
        {
            Dispose(false);
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected abstract void Dispose(bool disposing);
        
#if UNITY_EDITOR
        private bool _showModule;
        
        public void RenderModuleGUI(Action onRemoveModule)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.BeginHorizontal();
            _showModule = EditorGUILayout.Foldout(_showModule, Name, true);
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            if (GUILayout.Button("Remove Module"))
                onRemoveModule?.Invoke();
            EditorGUILayout.EndHorizontal();
            
            if (_showModule)
                ModuleGUI();
            
            EditorGUI.indentLevel--;
        }

        protected abstract void ModuleGUI();
#endif
    }
}
