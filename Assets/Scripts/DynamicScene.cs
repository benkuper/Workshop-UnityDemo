using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class DynamicScene : Controllable
{
    public delegate void SceneEvent(DynamicScene scene);
    public SceneEvent sceneStateChanged;


    public enum SceneState {STARTING, RUNNING, ENDING, ENDED, NOTSET}

    [Header("Scene")]
    [HideInInspector]
    public SceneState state = SceneState.NOTSET;

    public float startTime = 1;
    public float endTime = 1;
    public bool alwaysUpdate;

    override public void Awake()
    {
        TargetScript = this;
        base.Awake();
    }

    void setState(SceneState newState)
    {
        if (state == newState) return;
        state = newState;

        Debug.Log("[Scene] State change " + state);
        sceneStateChanged?.Invoke(this);
    }

    override public void Update()
    {
        base.Update();
        if(state == SceneState.RUNNING || alwaysUpdate) updateScene();
    }


    public void handleStartScene()
    {
        setState(SceneState.STARTING);
        Invoke("finishStartScene", startTime);
        startScene();
    }

    public virtual void startScene()
    {
       //to override
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, startTime);
    }

    protected void finishStartScene()
    {
        setState(SceneState.RUNNING); //to override if we want a different behavior
    }

    public virtual void updateScene()
    {
        //to be overriden
    }

    public void handleEndScene()
    {
        setState(SceneState.ENDING);
        Invoke("finishEndScene", endTime);
        endScene();
    }

    public virtual void endScene()
    {
        // to override
        transform.DOScale(Vector3.zero, endTime);
    }
    
    protected void finishEndScene()
    {
        setState(SceneState.ENDED);
    }
}
