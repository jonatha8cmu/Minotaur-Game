using UnityEngine;
using UnityEngine.SceneManagement;
using Core.Events;
using System.Collections;

// Note: Named SceneManager to match current project; consider renaming to avoid confusion with UnityEngine.SceneManagement.SceneManager.
// Lifecycle Order (Bootstrap):
// 1) SceneManager initiates scene load -> publishes SceneLoadStarted / SceneLoadProgress
// 2) On activation publishes SceneActivated (consumed by PlayerManager to spawn)
// 3) Later additive unloads publish SceneUnloadStarted / SceneUnloaded
public class SceneManager : MonoBehaviour
{
    public static SceneManager Instance { get; private set; }

    private string _activeSceneId = string.Empty;
    private bool _isLoading;
    private bool _isUnloading;

    private readonly System.Collections.Generic.HashSet<string> _loadedAdditive = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        EventRouter.Subscribe<SceneLoadRequested>(OnSceneLoadRequested);
        EventRouter.Subscribe<SceneUnloadRequested>(OnSceneUnloadRequested);
    }

    private void OnDisable()
    {
        EventRouter.Unsubscribe<SceneLoadRequested>(OnSceneLoadRequested);
        EventRouter.Unsubscribe<SceneUnloadRequested>(OnSceneUnloadRequested);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void OnSceneLoadRequested(SceneLoadRequested evt)
    {
        if (_isLoading) return;
        if (string.IsNullOrWhiteSpace(evt.SceneId)) return;
        StartCoroutine(LoadSceneRoutine(evt.SceneId));
    }

    private void OnSceneUnloadRequested(SceneUnloadRequested evt)
    {
        if (_isUnloading) return;
        if (string.IsNullOrWhiteSpace(evt.SceneId)) return;
        if (!_loadedAdditive.Contains(evt.SceneId)) return;
        StartCoroutine(UnloadSceneRoutine(evt.SceneId));
    }

    private IEnumerator LoadSceneRoutine(string sceneId)
    {
        _isLoading = true;
        EventRouter.Publish(new SceneLoadStarted { SceneId = sceneId });

        AsyncOperation op;
        try
        {
            op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneId, LoadSceneMode.Additive);
        }
        catch
        {
            _isLoading = false;
            yield break;
        }

        while (op != null && !op.isDone)
        {
            EventRouter.Publish(new SceneLoadProgress { SceneId = sceneId, Progress = op.progress });
            yield return null;
        }

        _loadedAdditive.Add(sceneId);
        _activeSceneId = sceneId;

        try
        {
            var loadedScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneId);
            if (loadedScene.IsValid())
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(loadedScene);
            }
        }
        catch { }

        EventRouter.Publish(new SceneActivated { SceneId = sceneId });
        _isLoading = false;
    }

    private IEnumerator UnloadSceneRoutine(string sceneId)
    {
        _isUnloading = true;
        EventRouter.Publish(new SceneUnloadStarted { SceneId = sceneId });

        AsyncOperation op;
        try
        {
            op = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(sceneId);
        }
        catch
        {
            _isUnloading = false;
            yield break;
        }

        while (op != null && !op.isDone)
        {
            yield return null;
        }

        _loadedAdditive.Remove(sceneId);
        if (_activeSceneId == sceneId)
        {
            _activeSceneId = string.Empty;
        }

        EventRouter.Publish(new SceneUnloaded { SceneId = sceneId });
        _isUnloading = false;
    }

    public void RequestLoadScene(string sceneId)
    {
        EventRouter.Publish(new SceneLoadRequested { SceneId = sceneId });
    }

    public void RequestUnloadScene(string sceneId)
    {
        EventRouter.Publish(new SceneUnloadRequested { SceneId = sceneId });
    }

    public string GetActiveSceneId() => _activeSceneId;
}
