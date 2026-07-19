using TMPro;
using UniRx;

namespace Vesolovsky.Core.UISystem.UIComponents
{
    public class VText : TextMeshProUGUI
    {
        public void Bind(IReadOnlyReactiveProperty<int> property)
        {
            property.Subscribe(value => SetText(value.ToString())).AddTo(this);
        }

        //TODO: Add to the core
        public void Bind(IReadOnlyReactiveProperty<int> property, string prefixText)
        {
            property.Subscribe(value => SetText($"{prefixText}{value}")).AddTo(this);
        }

        public void Bind(IReadOnlyReactiveProperty<long> property)
        {
            property.Subscribe(value => SetText(value.ToString())).AddTo(this);
        }

        //TODO: Add to the core
        public void Bind(IReadOnlyReactiveProperty<string> property)
        {
            property.Subscribe(value => SetText(value)).AddTo(this);
        }
    }
}