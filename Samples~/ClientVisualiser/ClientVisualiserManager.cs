using System;
using System.Collections.Generic;
using UnityEngine;
using jKnepel.SimpleUnityNetworking.Managing;
using jKnepel.SimpleUnityNetworking.Networking;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jKnepel.SimpleUnityNetworking.Samples
{
    public class ClientVisualiserManager : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private ClientVisualiser _visualiserPrefab;
        [SerializeField] private Transform _visualiserParent;
        [SerializeField] private float _visualiserUpdateDelay = 0.05f;

        private readonly Dictionary<byte, ClientVisualiser> _visualisers = new();

        private Vector3 _lastCameraPosition;
        private Quaternion _lastCameraRotation;
        private float _clientVisualiserDelay;
        private bool _isUpdating;

		#region lifecycle

		private void OnEnable()
		{
            if (_networkManager != null)
			{
                _networkManager.OnConnected += () => SetIsUpdating(true);
                _networkManager.OnDisconnected += () => SetIsUpdating(false);
                _networkManager.OnClientDisconnected += RemoveConnectedClient;
			}
        }

		private void OnDisable()
        {
            if (_networkManager != null)
            {
                _networkManager.OnConnected -= () => SetIsUpdating(true);
                _networkManager.OnDisconnected -= () => SetIsUpdating(false);
                _networkManager.OnClientDisconnected -= RemoveConnectedClient;
            }
        }

        private void Update()
        {
            _clientVisualiserDelay += Time.deltaTime;
#if UNITY_EDITOR
            if (SceneView.lastActiveSceneView.camera.transform.hasChanged)
                CurrentCameraMoved(SceneView.lastActiveSceneView.camera.transform);
#else
			if (Camera.current.transform && Camera.current.transform.hasChanged)
				CurrentCameraMoved(Camera.current.transform);
#endif
        }

		#endregion

		#region private methods

        private void RemoveConnectedClient(byte id)
        {
            if (!_visualisers.Remove(id, out ClientVisualiser visualiser))
                return;

            if (visualiser == null) return;
#if UNITY_EDITOR
            GameObject.DestroyImmediate(visualiser.gameObject);
#else
			GameObject.Destroy(visualiser.gameObject);
#endif
            visualiser = null;
        }

        private void SetIsUpdating(bool isUpdating)
		{
            _isUpdating = isUpdating;
            if (isUpdating)
                _networkManager.RegisterStructData<ClientVisualiserData>(OnReceiveData);
            else
                _networkManager.UnregisterStructData<ClientVisualiserData>(OnReceiveData);
        }

        private void OnReceiveData(byte sender, ClientVisualiserData data)
        {
            if (!_networkManager.ConnectedClients.TryGetValue(sender, out ClientInformation client))
                return;

            if (!_visualisers.TryGetValue(sender, out ClientVisualiser visualiser))
            {
                visualiser = GameObject.Instantiate(_visualiserPrefab, _visualiserParent);
                visualiser.UpdateVisualiser(client.ID, client.Username, client.Color);
                _visualisers.Add(sender, visualiser);
            }

            visualiser.transform.SetPositionAndRotation(data.Position, data.Rotation);

            if (!visualiser.gameObject.activeSelf)
                visualiser.gameObject.SetActive(true);
        }

        private void CurrentCameraMoved(Transform camera)
        {
            if (!_isUpdating)
                return;

            // limit camera syncs
            if (_clientVisualiserDelay < _visualiserUpdateDelay)
            {
                camera.hasChanged = false;
                return;
            }

            if (_lastCameraPosition != null
                && camera.position.Equals(_lastCameraPosition)
                && camera.rotation.Equals(_lastCameraRotation))
            {
                camera.hasChanged = false;
                return;
            }

            _networkManager.SendStructData(0, new ClientVisualiserData(camera.position, camera.rotation), ENetworkChannel.UnreliableOrdered);

            _lastCameraPosition = camera.position;
            _lastCameraRotation = camera.rotation;
            camera.hasChanged = false;
            _clientVisualiserDelay = 0;
        }


        #endregion
    }
}
