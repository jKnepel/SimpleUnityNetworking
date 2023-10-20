using System;
using System.Collections.Concurrent;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jKnepel.SimpleUnityNetworking.Utilities
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public static class MainThreadQueue
    {
        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new();

#if UNITY_EDITOR
        static MainThreadQueue()
        {
            Start();
            EditorApplication.update += Update;
        }
#else
        [RuntimeInitializeOnLoadMethod]
        private static void InitRuntime()
        {
            MainThreadQueueRuntime.Instance.OnUpdate += Update;
        }
#endif

        private static void Start()
        {

        }

        private static void Update()
        {
            while (_mainThreadQueue.Count > 0)
			{
                _mainThreadQueue.TryDequeue(out Action action);
                action?.Invoke();
			}
        }

        public static void Enqueue(Action action)
		{
            _mainThreadQueue.Enqueue(action);
		}
    }

    internal class MainThreadQueueRuntime : MonoBehaviour
    {
        public Action OnUpdate;

        private static MainThreadQueueRuntime _instance;
        public static MainThreadQueueRuntime Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = (MainThreadQueueRuntime)FindObjectOfType(typeof(MainThreadQueueRuntime));

                    if (_instance == null)
                    {
                        GameObject singletonObject = new();
                        singletonObject.hideFlags = HideFlags.HideInHierarchy;
                        DontDestroyOnLoad(singletonObject);
                        _instance = singletonObject.AddComponent<MainThreadQueueRuntime>();
                    }
                }

                return _instance;
            }
        }

        private void Update()
        {
            OnUpdate?.Invoke();
        }
    }
}
