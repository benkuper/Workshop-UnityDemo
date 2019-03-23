using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class CubeScene : DynamicScene
{
    public Transform cube;

    [OSCProperty]
    public Vector3 speed;

    [OSCProperty]
    public float scale;

    [OSCProperty]
    public Color color;

    public override void startScene()
    {
        cube.transform.localPosition = Vector3.left * 20;
        cube.transform.DOLocalMove(Vector3.zero, startTime).SetEase(Ease.OutBack);
    }

    public override void updateScene()
    {
        cube.transform.Rotate(speed);
        cube.transform.localScale = Vector3.one * scale;
        cube.GetComponent<Renderer>().material.SetColor("_BaseColor",color);
    }

    public override void endScene()
    {
        cube.transform.DOLocalMove(Vector3.right * 20, endTime).SetEase(Ease.InBack);
    }
}
