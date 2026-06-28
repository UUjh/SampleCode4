using UnityEngine;

namespace SampleClient.Core
{
    /// <summary>
    /// 앱 생명주기 동안 유지되는 단일 MonoBehaviour 베이스.
    /// 샘플에서는 중복 생성 방지와 DontDestroyOnLoad 처리만 남겼다.
    /// </summary>
    public abstract class PersistentSingleton<T> : MonoBehaviour where T : PersistentSingleton<T>
    {
        private static T _instance;

        public static bool HasInstance => _instance != null;
        public static T CurrentInstance => _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<T>();
                }

                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = (T)this;
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                Dispose();
                _instance = null;
            }
        }

        protected virtual void Dispose()
        {
        }
    }
}
