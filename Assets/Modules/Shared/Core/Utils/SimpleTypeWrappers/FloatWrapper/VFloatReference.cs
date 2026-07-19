using System;

namespace Vesolovsky.Core.Utils
{
    //TODO: add to the core
    [Serializable]
    public class VFloatReference
    {
        public bool UseConstant = true;
        public float ConstantValue;
        public VFloatVariable Variable;

        public float Value => UseConstant ? ConstantValue : Variable.Value;
    }
}
