using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Samples
{
    [System.Serializable, ExecuteInEditMode]
    public class ClientVisualiser : MonoBehaviour
    {
        [SerializeField] private Renderer _renderer;
        [SerializeField] private TMPro.TMP_Text _usernameObject;
        [SerializeField] private Material _material;

        public void UpdateVisualiser(byte id, string username, Colour colour)
        {
            name = $"{id}#{username}";
            _usernameObject.text = username;
            _usernameObject.colour = colour;
            if (_material == null)
            {
                _renderer.material = Instantiate(_material);
            }
            _renderer.material.SetColor("_Color", colour);
        }
    }
}
