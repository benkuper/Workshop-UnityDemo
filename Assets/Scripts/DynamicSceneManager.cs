using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DynamicSceneManager : Controllable
{

    DynamicScene currentScene;
    GameObject nextScenePrefab;

    public List<GameObject> scenes;
    public bool waitForEndBeforeStart;


    public override void Awake()
    {
        TargetScript = this;
        base.Awake();
    }

    public override void Update()
    {
        base.Update();
        if (Input.GetKeyDown(KeyCode.C))
        {
            setScene(scenes[0]);
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            setScene(scenes[1]);
        }
    }

    [OSCMethod]
    public void setScene(string name)
    {
        if(name == "cube")
        {
            setScene(scenes[0]);
        }else if(name == "sphere")
        {
            setScene(scenes[1]);
        }
    }

    void setScene(GameObject s)
    {
        if (currentScene == s) return;

        Debug.Log("[SceneManager] Set scene : " + s);

        if (currentScene != null)
        {
            currentScene.handleEndScene();

            if(waitForEndBeforeStart)
            {
                nextScenePrefab = s;
                return;
            }
        }


        if (s != null)
        {
            currentScene = Instantiate(s).GetComponent<DynamicScene>();
            currentScene.transform.SetParent(transform, false);

            if (currentScene != null)
            {
                currentScene.handleStartScene();
                currentScene.sceneStateChanged += sceneStateChanged;
            }else
            {
                Debug.LogWarning("Loaded scene does not have a dynamicScene behavior in it !");
            }

            nextScenePrefab = null;
        }
    }


    void sceneStateChanged(DynamicScene s)
    {
        if(s.state == DynamicScene.SceneState.ENDED)
        {
            Destroy(s.gameObject);

            if (waitForEndBeforeStart)
            {
                currentScene = null;
                setScene(nextScenePrefab);
            }
        }
      
    }
    
}
