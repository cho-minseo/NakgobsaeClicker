using UnityEngine;
using UnityEngine.Pool;

namespace CookieClicker.FX
{
    /// <summary>
    /// UIParticle 프리팹을 미리 만들어 두고 돌려 쓰는 풀.
    /// 다 쓴 이펙트는 지우지 않고 꺼 두었다가 다음에 다시 켜서 쓴다 (Instantiate/Destroy와 GC를 줄이는 모바일 최적화).
    /// </summary>
    public class UIParticlePool
    {
        private readonly ObjectPool<UIParticle> pool;

        public UIParticlePool(UIParticle prefab, Transform parent, int prewarm, int maxSize)
        {
            pool = new ObjectPool<UIParticle>(
                createFunc: () =>
                {
                    var p = Object.Instantiate(prefab, parent);
                    p.Finished = Release;
                    p.gameObject.SetActive(false);
                    return p;
                },
                actionOnGet: p =>
                {
                    p.gameObject.SetActive(true);
                    p.transform.SetAsLastSibling();
                },
                actionOnRelease: p => p.gameObject.SetActive(false),
                actionOnDestroy: p => { if (p != null) Object.Destroy(p.gameObject); },
                collectionCheck: false, defaultCapacity: prewarm, maxSize: maxSize);

            // 처음 클릭할 때 한꺼번에 만들어지며 끊기지 않도록 시작할 때 미리 채워 둔다.
            var warm = new UIParticle[prewarm];
            for (int i = 0; i < prewarm; i++) warm[i] = pool.Get();
            for (int i = 0; i < prewarm; i++) pool.Release(warm[i]);
        }

        public UIParticle Get() => pool.Get();
        private void Release(UIParticle p) => pool.Release(p);
    }
}
