using UnityEngine;

public class ButtonTransition : MonoBehaviour
{
    [SerializeField] private string gameplaySceneId;

    void Awake() => Debug.Log("ButtonTransition awake");

    // Hook this from a UI Button OnClick()
    public void SceneTransition()
    {
        //EventRouter.Publish(new GameStateEnterRequested { Target = GameState.Loading });
        //EventRouter.Publish(new SceneLoadRequested { SceneId = gameplaySceneId });
    }
}
