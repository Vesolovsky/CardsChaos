using Cysharp.Threading.Tasks;

namespace Vesolovsky.Core.Services.Save
{
    public interface ISaveService<out T> where T : IGameSave
    {
        public UniTask Save();
        public void ClearSave();
        public T CurrentSave { get; }
    }
}