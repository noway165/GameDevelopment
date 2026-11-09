using System.Collections.Generic;
using Metropolis.Core;
using UnityEngine;

namespace Metropolis.Game
{
    /// <summary>Shows the tool preview with pooled, tinted quads.</summary>
    public sealed class PreviewView
    {
        readonly ToolController _tools;
        readonly GridMapper _map;
        readonly GameObject _prefab;
        readonly Transform _parent;
        readonly List<GameObject> _pool = new List<GameObject>();
        readonly List<Renderer> _renderers = new List<Renderer>();
        readonly MaterialPropertyBlock _validTint = new MaterialPropertyBlock();
        readonly MaterialPropertyBlock _invalidTint = new MaterialPropertyBlock();
        int _shownVersion = -1;

        public PreviewView(ToolController tools, GridMapper map, GameObject prefab, Transform parent)
        {
            _tools = tools;
            _map = map;
            _prefab = prefab;
            _parent = parent;
            _validTint.SetColor("_BaseColor", new Color(0.2f, 0.9f, 0.3f, 1f));
            _invalidTint.SetColor("_BaseColor", Color.red);
        }

        public int ActiveCount { get; private set; }

        public void Refresh()
        {
            if (_tools.PreviewVersion == _shownVersion)
                return;
            _shownVersion = _tools.PreviewVersion;

            int used = 0;
            foreach (var cell in _tools.PreviewValid)
                Show(used++, cell, _validTint);
            foreach (var cell in _tools.PreviewInvalid)
                Show(used++, cell, _invalidTint);

            for (int i = used; i < ActiveCount; i++)
                _pool[i].SetActive(false);
            ActiveCount = used;
        }

        void Show(int index, Cell cell, MaterialPropertyBlock tint)
        {
            if (index == _pool.Count)
            {
                var created = Object.Instantiate(_prefab, _parent);
                _pool.Add(created);
                _renderers.Add(created.GetComponentInChildren<Renderer>());
            }

            _pool[index].transform.position = _map.CellCenter(cell);
            _pool[index].SetActive(true);
            _renderers[index].SetPropertyBlock(tint);
        }
    }
}
