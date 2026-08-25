using Steamworks;
using UnityEngine;

public class SteamManager : MonoBehaviour
    {
        private static SteamManager instance;

        public bool IsInitialized { get; private set; }
        public SteamId SteamId { get; private set; }

        public static SteamManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("SteamManager");
                    instance = go.AddComponent<SteamManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            // Keep simulating and pumping Steam callbacks while unfocused — otherwise an
            // alt-tabbed host stops answering clients and solo runs stall entirely.
            Application.runInBackground = true;

            try
            {
                SteamClient.Init(2308170, asyncCallbacks: true);
                IsInitialized = true;
                SteamId = SteamClient.SteamId;
                Debug.Log($"[SteamManager] Initialized. SteamId: {SteamId}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SteamManager] Failed to initialize: {e.Message}");
            }
        }

        private void Update()
        {
            if (IsInitialized)
                SteamClient.RunCallbacks();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                if (IsInitialized)
                    SteamClient.Shutdown();
                instance = null;
            }
        }
    }
