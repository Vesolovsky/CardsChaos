using System;
using UnityEngine;

namespace Vesolovsky.Core.Utils
{
    [Serializable]
    public struct Optional<T>
    {
        [SerializeField] private bool enabled;
        [SerializeField] private T value;

        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        public T Value => value;

        public Optional(T initialValue)
        {
            enabled = true;
            value = initialValue;
        }
    }
}